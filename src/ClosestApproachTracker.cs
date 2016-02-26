using System;
using UnityEngine;

namespace BetterBurnTime
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class ClosestApproachTracker : MonoBehaviour
    {
        private static ClosestApproachTracker instance = null;

        private static readonly TimeSpan UPDATE_INTERVAL = new TimeSpan(0, 0, 0, 0, 250);

        // The last time we updated our calculations. Used with UPDATE_INTERVAL
        // to prevent spamming excessive calculations.
        private DateTime lastUpdate;

        // The result of calculations.
        private double closestApproachTime;
        private double closestApproachDistance;
        private double closestApproachRelativeVelocity;

        // "Presentation" data (based on calculations) used for display purposes.
        private int secondsUntilClosestApproach;
        private int hundredsMetersDistance;
        private string approachDescription;

        /// <summary>
        /// Global setting for whether closest approach tracking is enabled.
        /// </summary>
        private static readonly bool displayEnabled = Configuration.showClosestApproach;

        /// <summary>
        /// If calculated closest-approach distance is greater than this, don't track.
        /// </summary>
        private static readonly double maxClosestApproachDistanceKm = Configuration.closestApproachMaxDistanceKm;

        /// <summary>
        /// If calculated closest-approach distance is greater than this, don't track.
        /// </summary>
        private static readonly double maxTimeUntilEncounter = Configuration.closestApproachMaxTimeUntilEncounter;

        /// <summary>
        /// If the target is closer than this many meters, don't track.
        /// </summary>
        private static readonly double minTargetDistance = Configuration.closestApproachMinTargetDistance;

        /// <summary>
        /// Here when the add-on loads upon flight start.
        /// </summary>
        public void Start()
        {
            try
            {
                instance = this;
                Reset();
                lastUpdate = DateTime.Now;
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
                if (BurnInfo.OriginalDisplayEnabled)
                {
                    if (HasInfo) Reset();
                    return;
                }
                if (Recalculate())
                {
                    int timeUntil = (int)(closestApproachTime - Now);
                    int distance100 = (int)((closestApproachDistance * 0.01) + 0.5);
                    bool isChanged = (timeUntil != secondsUntilClosestApproach) || (distance100 != hundredsMetersDistance);
                    if (isChanged)
                    {
                        secondsUntilClosestApproach = timeUntil;
                        hundredsMetersDistance = distance100;
                        approachDescription = string.Format(
                            "Target@{0:F1}km in {1}",
                            0.1 * (double)hundredsMetersDistance,
                            TimeFormatter.Default.format(secondsUntilClosestApproach));
                    }
                } else
                {
                    Reset();
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        /// <summary>
        /// Gets the relative velocity to target at closest approach, in m/s.
        /// NaN if not available.
        /// </summary>
        public static double Velocity
        {
            get
            {
                if (instance == null) return double.NaN;

                if (instance.HasInfo
                    && (instance.closestApproachDistance < maxClosestApproachDistanceKm * 1000.0)
                    && (instance.closestApproachTime - Now < maxTimeUntilEncounter))
                {
                    return instance.closestApproachRelativeVelocity;
                } else
                {
                    return double.NaN;
                }
            }
        }

        /// <summary>
        /// Gets the description to show for closest approach (including time-until).
        /// Null if not available.
        /// </summary>
        public static string Description
        {
            get { return (instance == null) ? null : instance.approachDescription; }
        }

        /// <summary>
        /// Gets the time until closest approach, in seconds.  -1 if not available.
        /// </summary>
        public static int TimeUntil
        {
            get { return (instance == null) ? -1 : instance.secondsUntilClosestApproach; }
        }

        /// <summary>
        /// Reset the tracker and clear out all info.
        /// </summary>
        private void Reset()
        {
            closestApproachTime = double.NaN;
            closestApproachDistance = double.NaN;
            closestApproachRelativeVelocity = double.NaN;
            secondsUntilClosestApproach = -1;
            hundredsMetersDistance = -1;
            approachDescription = null;
        }

        /// <summary>
        /// Gets whether the tracker has info available to display.
        /// </summary>
        private bool HasInfo
        {
            get { return !double.IsNaN(closestApproachTime); }
        }

        /// <summary>
        /// Tries to find the vessel's closest encounter with its target, if any. Returns
        /// true if there's anything to display.
        /// </summary>
        /// <returns></returns>
        private bool Recalculate() {
            if (!displayEnabled) return false;

            // Is it even possible to do a calculation?
            Vessel vessel = FlightGlobals.ActiveVessel;
            Vessel targetVessel = TargetVesselOf(vessel);
            if (targetVessel == null) return false;

            // Turn off display for target vessel very close.
            double targetDistance = (vessel.GetWorldPos3D() - targetVessel.GetWorldPos3D()).magnitude;
            if (targetDistance < 2.0 * minTargetDistance)
            {
                double targetRelativeVelocity = (vessel.GetObtVelocity() - targetVessel.GetObtVelocity()).magnitude;
                if (targetRelativeVelocity < 1.0) return false;
                if ((targetDistance < minTargetDistance) && (targetRelativeVelocity < 10.0)) return false;
            }

            // If we already have info and it's fresh enough, no need to recalculate.
            DateTime now = DateTime.Now;
            if (((now - lastUpdate) < UPDATE_INTERVAL) && HasInfo) return true;
            lastUpdate = now;

            FindClosestApproach(
                vessel.orbit,
                targetVessel.orbit,
                Now,
                out closestApproachTime,
                out closestApproachDistance);

            Vector3d vesselVelocity = vessel.orbit.getOrbitalVelocityAtUT(closestApproachTime);
            Vector3d targetVelocity = targetVessel.orbit.getOrbitalVelocityAtUT(closestApproachTime);
            closestApproachRelativeVelocity = (vesselVelocity - targetVelocity).magnitude;

            return true;
        }

        /// <summary>
        /// Gets the target vessel of the specified vessel, or none if there isn't
        /// one or it's not usable for some reason.
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        private static Vessel TargetVesselOf(Vessel vessel)
        {
            if (vessel == null) return null;
            if (vessel.targetObject == null) return null;
            if (vessel.Landed) return null;
            Vessel targetVessel = vessel.targetObject as Vessel;
            if (targetVessel == null) return null;
            if (targetVessel.Landed) return null;
            return targetVessel;
        }

        /// <summary>
        /// Find the UT time at which body A comes closest to body B.
        /// Thanks to sarbian for the code that inspired this function:
        /// https://github.com/MuMech/MechJeb2/blob/master/MechJeb2/OrbitExtensions.cs#L123-L158
        /// </summary>
        /// <param name="sourceOrbit">The "source" orbit, e.g. of the player's ship.</param>
        /// <param name="targetOrbit">The "target" orbit.</param>
        /// <param name="currentTime">The current UT from which we start looking for a solution.</param>
        /// <param name="closestApproachTime">The UT at which the source orbit is closest to the target.</param>
        /// <param name="closestApproachDistance">The closest approach distance in meters.</param>
        private static void FindClosestApproach(
            Orbit sourceOrbit,
            Orbit targetOrbit,
            double currentTime,
            out double closestApproachTime,
            out double closestApproachDistance)
        {
            // We'll divide the orbit into this many divisions (i.e. drop this
            // many points along the orbit) and find which one's the closest.
            // Then we'll pick the interval containing the closest approach,
            // and subdivide there again.
            const int numDivisions = 20;

            // We keep iterating this many times, until we've zeroed in to a very close solution.
            const int numIterations = 8;

            // Our initial search interval will be one orbit of the source vessel.
            double interval = TrackingInterval(sourceOrbit);
            double intervalStartTime = currentTime;
            double intervalEndTime = currentTime + interval;

            // Keep track of the time & distance of closest approach.
            closestApproachTime = currentTime;
            closestApproachDistance = Double.MaxValue;

            // Now iterate.
            for (int iteration =  0; iteration < numIterations; ++iteration)
            {
                double segmentDuration = (intervalEndTime - intervalStartTime) / numDivisions;
                for (int divisionIndex = 0; divisionIndex < numDivisions; ++divisionIndex)
                {
                    double segmentStartTime = intervalStartTime + divisionIndex * segmentDuration;
                    double distance = SeparationAt(sourceOrbit, targetOrbit, segmentStartTime);
                    if (distance < closestApproachDistance)
                    {
                        closestApproachDistance = distance;
                        closestApproachTime = segmentStartTime;
                    }
                }
                intervalStartTime = Clamp(closestApproachTime - segmentDuration, currentTime, currentTime + interval);
                intervalEndTime = Clamp(closestApproachTime + segmentDuration, currentTime, currentTime + interval);
            } // for each iteration
        }

        /// <summary>
        /// Get the length of time to examine for a closest approach.  For elliptic
        /// orbits, this will be one orbit.
        /// </summary>
        /// <param name="orbit"></param>
        /// <returns></returns>
        private static double TrackingInterval(Orbit orbit)
        {
            if (orbit.eccentricity < 1.0)
            {
                // It's an elliptic orbit, just use the period of the orbit.
                return orbit.period;
            } else
            {
                // It's a hyperbolic orbit.

                // Calculate the rate of increase in the mean anomaly, in radians per second.
                double meanMotion = Math.Sqrt(orbit.referenceBody.gravParameter / Math.Abs(Math.Pow(orbit.semiMajorAxis, 3)));

                // We'll use 100 units of that, which should cover a large fraction of arc.
                return 100 / meanMotion;
            }
        }

        /// <summary>
        /// Gets the separation in meters of the two orbits at the specified time.
        /// </summary>
        /// <param name="orbit1"></param>
        /// <param name="orbit2"></param>
        /// <param name="time">UT</param>
        /// <returns></returns>
        private static double SeparationAt(Orbit orbit1, Orbit orbit2, double time)
        {
            return (AbsolutePositionAt(orbit1, time) - AbsolutePositionAt(orbit2, time)).magnitude;
        }

        /// <summary>
        /// Gets the position of the orbit at the specified time, in world coordinates.
        /// </summary>
        /// <param name="orbit"></param>
        /// <param name="time">UT</param>
        /// <returns></returns>
        private static Vector3d AbsolutePositionAt(Orbit orbit, double time)
        {
            // We use .xzy because Orbit class functions appear to use a coordinate
            // system in which Y and Z axes are swapped.  Thanks to sarbian for pointing
            // this out, here:
            // https://github.com/MuMech/MechJeb2/blob/master/MechJeb2/OrbitExtensions.cs#L18-L20
            return orbit.referenceBody.position + orbit.getRelativePositionAtUT(time).xzy;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Get the current UT.
        /// </summary>
        private static double Now
        {
            get { return Planetarium.GetUniversalTime(); }
        }
    }
}
