using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterBurnTime
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ImpactTracker : MonoBehaviour
    {
        /// <summary>
        /// The minimum fall speed, in m/s, for tracking impact. If the ship is falling slower
        /// than this (or rising), don't track impact.
        /// </summary>
        private static readonly double MIN_FALL_SPEED = 2.0;

        private static readonly TimeSpan UPDATE_INTERVAL = new TimeSpan(0, 0, 0, 0, 250);

        private static ImpactTracker instance = null;
        private DateTime lastVesselHeightUpdate;
        private double lastVesselHeight;
        private NavBallBurnVector burnVector = null;
        private ScreenSafeGUIText burnTimeText = null;
        private ScreenSafeGUIText impactTimeText = null;
        private int secondsUntilImpact = -1;
        private string impactVerb;
        private double impactSpeed;

        /// <summary>
        /// Global setting for whether impact tracking is enabled.
        /// </summary>
        public static bool displayEnabled = true;

        /// <summary>
        /// If calculated time to impact is greater than this many seconds, don't track.
        /// </summary>
        public static double maxTimeToImpact = 120.0;

        /// <summary>
        /// Here when the add-on loads upon flight start.
        /// </summary>
        public void Start()
        {
            try
            {
                instance = this;
                lastVesselHeight = double.NaN;
                lastVesselHeightUpdate = DateTime.Now;
                burnVector = GameObject.FindObjectOfType<NavBallBurnVector>();
                burnTimeText = CloneBehaviour(burnVector.ebtText);
                impactTimeText = CloneBehaviour(burnVector.TdnText);
                impactVerb = "N/A";
                impactSpeed = double.NaN;
                secondsUntilImpact = -1;
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        /// <summary>
        /// Called on each frame.
        /// </summary>
        public void LateUpdate()
        {
            try
            {
                bool shouldDisplay = ShouldDisplay;
                double calculatedTimeToImpact = double.PositiveInfinity;
                if (shouldDisplay)
                {
                    calculatedTimeToImpact = CalculateTimeToImpact(FlightGlobals.ActiveVessel, out impactVerb, out impactSpeed);
                    if (calculatedTimeToImpact > maxTimeToImpact) shouldDisplay = false;
                }
                if (shouldDisplay)
                {
                    UpdateContent(calculatedTimeToImpact);
                    burnTimeText.enabled = impactTimeText.enabled = true;
                }
                else
                {
                    burnTimeText.enabled = impactTimeText.enabled = false;
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        /// <summary>
        /// Gets the text object for displaying burn time (null if none).
        /// </summary>
        public static ScreenSafeGUIText BurnTimeText
        {
            get
            {
                return (instance == null) ? null : (IsDisplayed ? instance.burnTimeText : null);
            }
        }

        /// <summary>
        /// Gets the projected surface impact speed. Returns NaN if there is none.
        /// </summary>
        public static double ImpactSpeed
        {
            get
            {
                return (instance == null) ? double.NaN : instance.impactSpeed;
            }
        }

        /// <summary>
        /// Gets whether the impact tracker is currently displayed.
        /// </summary>
        public static bool IsDisplayed
        {
            get
            {
                return (instance == null) ? false : (instance.burnTimeText.enabled && instance.impactTimeText.enabled);
            }
        }

        /// <summary>
        /// Here when we need to update the content of the text display. Called on every frame.
        /// </summary>
        private void UpdateContent(double calculatedTimeToImpact)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;

            // A possible interesting future enhancement: use vessel.terrainNormal to deal better
            // with landing on sloping terrain. Right now we're making the simplifying assumption
            // that ground is flat, and the distance to fall is simply the ship's current height
            // above the ground. The vector vessel.terrainNormal gives the vessel's orientation
            // relative to the ground (i.e. <0,1,0> means "pointing straight away from the ground").
            // We could use that to compensate for sloping ground, so that if we're traveling
            // sideways and the ground is rising or falling beneath us, compensate for the
            // estimated time of impact.
            
            int remainingSeconds = (int)calculatedTimeToImpact;
            if (remainingSeconds != secondsUntilImpact)
            {
                secondsUntilImpact = remainingSeconds;
                impactTimeText.text = string.Format("{0} in {1} s", impactVerb, secondsUntilImpact);
            }
        }

        private double GetVesselHeight()
        {
            DateTime now = DateTime.Now;
            if (double.IsNaN(lastVesselHeight) || (now - lastVesselHeightUpdate > UPDATE_INTERVAL))
            {
                lastVesselHeightUpdate = now;
                lastVesselHeight = CalculateVesselHeight(FlightGlobals.ActiveVessel);
            }
            return lastVesselHeight;
        }

        /// <summary>
        /// Clones a behaviour.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="initialText"></param>
        /// <returns></returns>
        private static T CloneBehaviour<T>(T source) where T : Behaviour
        {
            GameObject clonedObject = UnityEngine.Object.Instantiate(
                source.gameObject,
                source.transform.position,
                source.transform.rotation) as GameObject;
            clonedObject.transform.parent = source.gameObject.transform.parent;
            T clonedBehaviour = clonedObject.GetComponent<T>();

            clonedBehaviour.enabled = false;
            return clonedBehaviour;
        }

        /// <summary>
        /// Determines whether the impact tracker should display.
        /// </summary>
        private bool ShouldDisplay
        {
            get
            {
				if (!displayEnabled) return false;
				// NullPointer check
				if (burnVector != null) {
					if (burnVector.ebtText.enabled) return false;
					if (burnVector.TdnText.enabled) return false;
				}
                
                // Don't display for bodies with atmospheres. (Kind of a bummer, but acceleration
                // when someone pops a parachute gets stupidly noisy and makes for a bad experience.
                // Just turn it off until I figure out a better solution. This can wait for
                // a future update.
                if (FlightGlobals.currentMainBody.atmosphere) return false;

                return true;
            }
        }

        private static bool IsTimeWarp
        {
            get
            {
                return (TimeWarp.WarpMode == TimeWarp.Modes.HIGH) || (TimeWarp.CurrentRate > 1.0);
            }
        }

        private static string ToString(Vector3d vector)
        {
            if (vector == null) return "null";
            return String.Format(
                "<{0:####0.000},{1:####0.000},{2:####0.000}> ({3:####0.000})", 
                vector.x,
                vector.y,
                vector.z,
                vector.magnitude);
        }

        /// <summary>
        /// This method calculates the height in meters from the ship's "current point" to its lowest
        /// extent. Subtracting this number from vessel.altitude gives the altitude of the bottom
        /// of the ship, in the ship's current orientation.
        /// 
        /// Thank you to SirDiazo's mod Landing Height for code that inspired this section.
        /// Landing Height: https://kerbalstuff.com/mod/458/Landing%20Height
        /// 
        /// It's notable that this code is somewhat simplified from SirDiazo's because it's solving
        /// a simpler problem (i.e. it's saving computation by giving more of an approximation).
        /// 
        /// This is a moderately expensive calculation to perform, so use sparingly (i.e. not on
        /// every frame).
        /// </summary>
        /// <returns></returns>
        private static double CalculateVesselHeight(Vessel vessel)
        {
            if (IsTimeWarp) return 0.0; // vessel is packed, can't do anything
            List<Part> parts = ChooseParts(vessel);
            double minPartPosition = double.PositiveInfinity;
            Part lowestPart = null;
            foreach (Part part in parts)
            {
                if (!part.collider.enabled) continue;
                double partPosition = Vector3.Distance(
                    part.collider.ClosestPointOnBounds(vessel.mainBody.position),
                    vessel.mainBody.position);
                if (partPosition < minPartPosition)
                {
                    minPartPosition = partPosition;
                    lowestPart = part;
                }
            }
            double vesselPosition = Vector3.Distance(
                //vessel.GetReferenceTransformPart().transform.position,
                vessel.transform.position,
                vessel.mainBody.position);
            return vesselPosition - minPartPosition;
        }

        private class PartAltitude
        {
            public Part p;
            public double alt;
        }

        private static List<Part> ChooseParts(Vessel vessel)
        {
            // If the vessel's not too big, use all the parts.
            if (vessel.parts.Count < 50) return vessel.parts;

            // Pick the 30 lowest-altitude parts and use those.
            List<PartAltitude> partAltitudes = new List<PartAltitude>(vessel.parts.Count);
            foreach (Part part in vessel.parts)
            {
                if (part.collider.enabled) partAltitudes.Add(
                    new PartAltitude() { p = part, alt = Vector3.Distance(part.transform.position, vessel.mainBody.position) });
            }
            partAltitudes.Sort((p1, p2) => p1.alt.CompareTo(p2.alt));
            int numParts = (partAltitudes.Count > 30) ? 30 : partAltitudes.Count;
            List<Part> parts = new List<Part>(numParts);
            for (int index = 0; index < numParts; ++index)
            {
                parts.Add(partAltitudes[index].p);
            }
            return parts;
        }

        /// <summary>
        /// Gets the time in seconds until the vessel will impact the surface. Returns
        /// positive infinity if it's not applicable.
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        private double CalculateTimeToImpact(Vessel vessel, out string verb, out double impactSpeed)
        {
            verb = "N/A";
            impactSpeed = double.NaN;

            // If we're already landed, or not falling, there's no upcoming impact.
            if (vessel.LandedOrSplashed) return double.PositiveInfinity;
            double fallSpeed = -vessel.verticalSpeed;
            if (fallSpeed < MIN_FALL_SPEED) return double.PositiveInfinity;

            // Are we in orbit (i.e. impact is "never")?
            Orbit orbit = vessel.patchedConicSolver.orbit;
            double periapsis = orbit.semiMajorAxis * (1.0 - orbit.eccentricity);
            float lowestWarpAltitude = vessel.mainBody.timeWarpAltitudeLimits[1]; // lowest altitude at which warp is possible
            if (periapsis > (vessel.mainBody.Radius + lowestWarpAltitude)) return double.PositiveInfinity;

            // How far do we need to fall to hit ground?
            double clearance = vessel.altitude - vessel.pqsAltitude - GetVesselHeight();
            verb = "Impact";

            // If we're over water, use water surface instead of ocean floor.
            if (vessel.mainBody.ocean && (clearance > vessel.altitude))
            {
                clearance = vessel.altitude;
                verb = "Splash";
            }

            if (clearance <= 0) return double.PositiveInfinity;

            // Work out centripetal acceleration (i.e. an upward acceleration component
            // due to the planet curving away from us)
            Vector3 shipPosition = vessel.transform.position - vessel.mainBody.position;
            double lateralSpeed = Vector3.Cross(shipPosition.normalized, vessel.obt_velocity).magnitude;
            double centripetalAcceleration = lateralSpeed * lateralSpeed / shipPosition.magnitude;

            // We now know how far we have to fall.  What's our downward acceleration? (might be negative)
            // Note that we deliberately exclude the ship's own propulsion. Including that causes a confusing
            // UI experience.
            double downwardAcceleration = vessel.gForce.magnitude - centripetalAcceleration;
            // TODO: If we eventually allow impact-tracking while in atmosphere, we need to do something more
            // complex to allow for atmospheric forces.

            if (Math.Abs(downwardAcceleration) < 0.01)
            {
                // Just about zero acceleration, use a simple velocity calculation.
                return clearance / fallSpeed;
            }

            // If our downward acceleration is negative (i.e. net acceleration is upward), there's
            // a chance we may not be due to hit the ground at all. Check for that.
            if (downwardAcceleration < 0)
            {
                double maxFallDistance = -(fallSpeed * fallSpeed) / (2.0 * downwardAcceleration);
                if (maxFallDistance < (clearance + 1.0)) return double.PositiveInfinity;
            }

            // solve the quadratic equation
            double secondsUntilImpact = (-fallSpeed + Math.Sqrt(fallSpeed * fallSpeed + 2.0 * downwardAcceleration * clearance)) / downwardAcceleration;

            double verticalSpeedAtImpact = fallSpeed + secondsUntilImpact * downwardAcceleration;
            impactSpeed = Math.Sqrt(verticalSpeedAtImpact * verticalSpeedAtImpact + vessel.horizontalSrfSpeed * vessel.horizontalSrfSpeed);

            return secondsUntilImpact;
        }
    }
}
