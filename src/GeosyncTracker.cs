using System;
using UnityEngine;

namespace BetterBurnTime
{
    /// <summary>
    /// When a ship is close to being in a geosynchronous orbit, show a +/- indicator to provide
    /// fine-tuning guidance on how far off it is from perfect geosynchronicity.
    ///
    /// When the override key is active, this forces the display to appear even if it otherwise
    /// wouldn't.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class GeosyncTracker : MonoBehaviour
    {
        private static GeosyncTracker instance = null;

        // How many seconds the ship must be idle (i.e. no throttle or control input)
        // before hiding the geosync display. (Not relevant if the override key is active,
        // since that forces display.)
        private static readonly TimeSpan IDLE_TIMEOUT = Configuration.geosyncIdleTimeout;

        // How close the ship's orbit needs to be to geosynchronous before the geosync display
        // automatically becomes active.  This is expressed as a fraction, indicating the
        // difference between current orbital period and planet's rotation period, divided
        // by the planet's rotation period, e.g. a value of 0.05 would mean "orbital period
        // has to be within 5% of planet's rotation period". (Not relevant if the override
        // key is active, since that forces display.)
        private static readonly double PRECISION_LIMIT = Configuration.geosyncPrecisionLimit;

        // The base label to use when the display is active, e.g. "gsync".
        private static readonly string GEOSYNC_LABEL = Configuration.geosyncLabel;

        // The base label to use when the display is active for a *target* rather than a
        // celestial body, e.g. "tsync"
        private static readonly string TARGETSYNC_LABEL = Configuration.targetsyncLabel;

        // The number of seconds offset, below which the delta is displayed in milliseconds
        // rather than hours / minutes / seconds.
        private static readonly double SECONDS_TRANSITION = Configuration.geosyncSecondsTransition;

        // The display description to use if a geosynchronous orbit *is* possible for the
        // current celestial body, but displaying the offset is not possible because the
        // ship is not currently in a stable orbit (e.g. escaping, suborbital, etc.) Note
        // that this is only ever displayed if the override key is toggled on, because
        // otherwise, a ship in such a situation simply wouldn't display any geosync info.
        private const string NOT_APPLICABLE_SUFFIX = " ~";
        private static readonly string NOT_APPLICABLE_GSYNC = GEOSYNC_LABEL + NOT_APPLICABLE_SUFFIX;
        private static readonly string NOT_APPLICABLE_TSYNC = TARGETSYNC_LABEL + NOT_APPLICABLE_SUFFIX;

        // The display description to use if a geosynchronous orbit is impossible for the
        // current celestial body, because either it's not rotating or else it's rotating
        // so slowly and its SoI is so small that geosync altitude is higher than the SoI
        // can reach. Note that this is only ever displayed if the override key is toggled
        // on, because otherwise, a ship in such a situation simply wouldn't display any
        // geosync info.
        private const string IMPOSSIBLE_SUFFIX = " X";
        private static readonly string GEOSYNC_IMPOSSIBLE = GEOSYNC_LABEL + IMPOSSIBLE_SUFFIX;
        private static readonly string TARGETSYNC_IMPOSSIBLE = TARGETSYNC_LABEL + IMPOSSIBLE_SUFFIX;

        // How close to idle a ship's control input needs to be in order to count as "active".
        private const double CONTROL_INPUT_THRESHOLD = 0.15;

        private static readonly TimeSpan UPDATE_INTERVAL = new TimeSpan(0, 0, 0, 0, 270);

        // We don't want to show this tracker *all* the time a craft is close to geosync, because
        // it would be there permanently and never go away-- it would be extraneous screen clutter.
        // So, what we do is only show the tracker if it's been less than TIME_SPAN since the last
        // time the craft was "touched" (defined as "had a nonzero throttle, or received any
        // control input from the player"). This field keeps track of when that timeout expires.
        // If the current time is later than expireTime, don't show the tracker.
        private DateTime expireTime;

        private DateTime nextUpdate;

        private CelestialBody currentCelestialBody = null;
        private double bodyRotationPeriod = double.NaN;
        private double targetOrbitalPeriod = double.NaN;
        private double orbitalPeriod = double.NaN;
        private long lastSecondsOffset = 0;
        private long lastMillisecondsOffset = 0;

        // The text description to display. Will be null if it shouldn't be displayed.
        private string geosyncDescription;
        private string targetsyncDescription;

        /// <summary>
        /// Here when the add-on loads upon flight start.
        /// </summary>
        public void Start()
        {
            instance = this;
            expireTime = nextUpdate = DateTime.Now;
        }

        /// <summary>
        /// Called on each frame.
        /// </summary>
        public void LateUpdate()
        {
            if (PRECISION_LIMIT == 0) return; // this deactivates the feature
            try
            {
                if (!CheckExpiry())
                {
                    geosyncDescription = null;
                    targetsyncDescription = null;
                    return;
                }

                Refresh();
                SetOrbitalPeriod();
                UpdateDescription(GEOSYNC_LABEL, bodyRotationPeriod, ref geosyncDescription);
                UpdateDescription(TARGETSYNC_LABEL, targetOrbitalPeriod, ref targetsyncDescription);
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        /// <summary>
        /// The description to display for this tracker, next to the navball. Returns null
        /// if no description should be shown.
        /// </summary>
        public static string Description
        {
            get
            {
                if (instance == null) return null;
                if (instance.targetsyncDescription != null) return instance.targetsyncDescription;
                if (instance.geosyncDescription != null) return instance.geosyncDescription;
                if (PRECISION_LIMIT == 0) return null;
                if (!OverrideKey.IsActive) return null;
                if (FlightGlobals.ActiveVessel.targetObject == null)
                {
                    return double.IsNaN(instance.bodyRotationPeriod) ? GEOSYNC_IMPOSSIBLE : NOT_APPLICABLE_GSYNC;
                }
                else
                {
                    return double.IsNaN(instance.targetOrbitalPeriod) ? TARGETSYNC_IMPOSSIBLE : NOT_APPLICABLE_TSYNC;
                }
            }
        }

        /// <summary>
        /// Update periodically-updated stuff.
        /// </summary>
        private void Refresh()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null) return;
            DateTime now = DateTime.Now;
            if (now < nextUpdate) return;
            nextUpdate = now + UPDATE_INTERVAL;

            bool wasTarget = !double.IsNaN(targetOrbitalPeriod);
            RefreshCelestialBody(vessel);
            RefreshTarget(vessel);
            bool isTarget = !double.IsNaN(targetOrbitalPeriod);
            if (wasTarget != isTarget)
            {
                lastMillisecondsOffset = 0;
                lastSecondsOffset = 0;
            }
        }

        /// <summary>
        /// Update the current celestial body.
        /// </summary>
        /// <returns></returns>
        private void RefreshCelestialBody(Vessel vessel)
        {
            // Has the current celestial body changed since the last time we checked?
            CelestialBody body = vessel.mainBody;
            if (body == currentCelestialBody) return;

            // It changed, set the rotation period.
            currentCelestialBody = body;
            Logging.Log("Current celestial body set to " + currentCelestialBody.name);
            if (!body.rotates)
            {
                Logging.Log(currentCelestialBody.name + " doesn't rotate, no geosynchronous orbit is possible");
                bodyRotationPeriod = double.NaN;
                return;
            }

            // If geosync altitude is bigger than SoI size, it means that no geosync
            // orbit is possible.
            double angularVelocity = 2.0 * Math.PI / body.rotationPeriod;
            double geosyncRadius = Math.Pow(
                body.gravParameter / (angularVelocity * angularVelocity),
                1.0 / 3.0);
            if (body.sphereOfInfluence <= geosyncRadius)
            {
                Logging.Log(
                    currentCelestialBody.name + " geosync radius of " + geosyncRadius
                    + " meters exceeds SoI size of " + body.sphereOfInfluence
                    + " meters, no geosynchronous orbit is possible");
                bodyRotationPeriod = double.NaN;
                return;
            }
            double geosyncAltitudeKm = (geosyncRadius - body.Radius) / 1000.0;
            Logging.Log(string.Format("Geosync altitude: {0:0.###} km", geosyncAltitudeKm));
            bodyRotationPeriod = body.rotationPeriod;
        }

        /// <summary>
        /// Update the current target.
        /// </summary>
        /// <param name="vessel"></param>
        private void RefreshTarget(Vessel vessel)
        {
            // Do we have a target?
            ITargetable target = vessel.targetObject;
            if (target == null)
            {
                targetOrbitalPeriod = double.NaN;
                return;
            }

            // We have a target. If it's a vessel, is it non-orbiting?
            Vessel targetVessel = target.GetVessel();
            if ((targetVessel != null) && (targetVessel.situation != Vessel.Situations.ORBITING))
            {
                targetOrbitalPeriod = double.NaN;
                return;
            }

            // It's something else.  Could be an orbiting vessel, could be a celestial body,
            // could be some other ITargetable (which I believe doesn't exist in the stock
            // game, but mods could introduce other types).  Does it have an orbit?
            Orbit orbit = target.GetOrbit();
            targetOrbitalPeriod = (orbit == null) ? double.NaN : orbit.period;
        }

        private void SetOrbitalPeriod()
        {
            orbitalPeriod = double.NaN;
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null) return;
            if (vessel.situation != Vessel.Situations.ORBITING) return;
            orbitalPeriod = vessel.orbit.period;
        }

        private void UpdateDescription(string label, double desiredPeriod, ref string description)
        {
            // Do we have info for stable orbit duration and planet rotation time?
            if (double.IsNaN(desiredPeriod) || double.IsNaN(orbitalPeriod))
            {
                description = null;
                return;
            }

            // How far off being geosynchronous are we?
            double offset = orbitalPeriod - desiredPeriod;

            // Is it close enough to perfect geosynchrony to show the display?
            double fraction = Math.Abs(offset / desiredPeriod);
            if (!OverrideKey.IsActive && (fraction > PRECISION_LIMIT))
            {
                description = null;
                return;
            }
            if (Math.Abs(offset) > long.MaxValue)
            {
                description = null;
                return;
            }

            // Should we display in milliseconds, or hours / minutes / seconds?
            if (Math.Abs(offset) < SECONDS_TRANSITION)
            {
                double doubleOffsetMs = 1000.0 * offset;
                if (Math.Abs(doubleOffsetMs) > long.MaxValue)
                {
                    description = null;
                    return;
                }
                long millisecondsOffset = (long)(doubleOffsetMs);
                if ((description == null) || (millisecondsOffset != lastMillisecondsOffset)) SetMillisecondsDescription(label, millisecondsOffset, ref description);
                lastSecondsOffset = (long)offset;
                lastMillisecondsOffset = millisecondsOffset;
            }
            else
            {
                long secondsOffset = (long)offset;
                if ((description == null) || (secondsOffset != lastSecondsOffset)) SetSecondsDescription(label, secondsOffset, ref description);
                lastSecondsOffset = secondsOffset;
                lastMillisecondsOffset = long.MinValue;
            }
        }

        /// <summary>
        /// Configure the geosync description for a given number of milliseconds.
        /// </summary>
        /// <param name="label"></param>
        /// <param name="millisecondsOffset"></param>
        /// <param name="description"></param>
        private void SetMillisecondsDescription(string label, long millisecondsOffset, ref string description)
        {
            description = string.Format(
                "{0} {1}{2}ms",
                label,
                SignOf(millisecondsOffset),
                Math.Abs(millisecondsOffset));
        }

        /// <summary>
        /// Configure the geosync description for a given number of seconds.
        /// </summary>
        /// <param name="label"></param>
        /// <param name="secondsOffset"></param>
        /// <param name="description"></param>
        private void SetSecondsDescription(string label, long secondsOffset, ref string description)
        {
            description = string.Format(
                "{0} {1}{2}",
                label,
                SignOf(secondsOffset),
                TimeFormatter.Default.format((int)Math.Abs(secondsOffset)));
        }

        private static char SignOf(long value)
        {
            return (value < 0) ? '-' : '+';
        }

        /// <summary>
        /// Determines whether the "active" timeout has expired. Returns true if
        /// it's expired, false if not.
        /// </summary>
        /// <returns></returns>
        private bool CheckExpiry()
        {
            DateTime now = DateTime.Now;
            if (HasInput())
            {
                expireTime = now + IDLE_TIMEOUT;
                return true;
            }
            else
            {
                return now < expireTime;
            }
        }

        /// <summary>
        /// Returns true if the ship is receiving "input" (i.e. either a control input
        /// from the player, or a nonzero throttle). This is used for resetting the
        /// control expiry timer.
        /// </summary>
        /// <returns></returns>
        private static bool HasInput()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null) return false;
            FlightCtrlState ctrlState = vessel.ctrlState;

            // Throttle > 0 counts as input.
            if (ctrlState.mainThrottle != 0) return true;

            // Pitch or yaw trim counts as input.
            if ((ctrlState.pitchTrim != 0) || (ctrlState.yawTrim != 0)) return true;

            // If there's significant control input, that counts. We use a nonzero
            // threshold because we only want to trigger when the *player* is doing something.
            // When SAS is active, there will pretty much *always* be nonzero control input
            // (SAS is always fiddling, even microscopically); keyboard input will be +1 or -1.
            // So we use a reasonable threshold that won't get surpassed when SAS is active on
            // an "idle" ship.
            if ((Math.Abs(ctrlState.pitch) > CONTROL_INPUT_THRESHOLD)
                || (Math.Abs(ctrlState.yaw) > CONTROL_INPUT_THRESHOLD)
                || (Math.Abs(ctrlState.roll) > CONTROL_INPUT_THRESHOLD)) return true;

            // The override key also counts.
            if (OverrideKey.IsActive) return true;

            // Nope, there's no input.
            return false;
        }
    }
}
