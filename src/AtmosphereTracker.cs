using System;
using UnityEngine;

namespace BetterBurnTime
{
    /// <summary>
    /// Tracks how long until the ship will enter (or leave) atmosphere. Doesn't give any
    /// burn time information or countdown indicator.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class AtmosphereTracker : MonoBehaviour
    {
        private static AtmosphereTracker instance = null;

        private static readonly TimeSpan UPDATE_INTERVAL = new TimeSpan(0, 0, 0, 0, 250);

        // The next time we're due to update our calculations. Used with UPDATE_INTERVAL
        // to prevent spamming excessive calculations.
        private DateTime nextUpdate;

        /// <summary>
        /// Global setting for whether atmosphere tracking is enabled.
        /// </summary>
        private static readonly bool displayEnabled = Configuration.showAtmosphere;

        /// <summary>
        /// If calculated time to atmosphere exit is greater than this many seconds, don't track.
        /// </summary>
        private static readonly double maxTimeToExit = Configuration.atmosphereMaxTimeUntilExit;
        private static readonly double maxTimeToEntry = Configuration.atmosphereMaxTimeUntilEntry;

        // Results of calculations
        private double transitionTimeUT = double.NaN;
        private int secondsUntilTransition = -1;
        private string transitionVerb = null;
        private string transitionDescription = null;

        /// <summary>
        /// Here when the add-on loads upon flight start.
        /// </summary>
        public void Start()
        {
            try
            {
                instance = this;
                Reset();
                nextUpdate = DateTime.Now;
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
                }
                else
                {
                    if (!Recalculate()) Reset();
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        /// <summary>
        /// Gets the description to show for atmosphere transition (including time-until).
        /// Null if not available.
        /// </summary>
        public static string Description
        {
            get { return (instance == null) ? null : instance.transitionDescription; }
        }

        /// <summary>
        /// Gets the time until atmosphere transition, in seconds.  NaN if not available.
        /// </summary>
        public static double TimeUntil
        {
            get
            {
                if (instance == null) {
                    return double.NaN;
                }
                else
                {
                    return double.IsNaN(instance.transitionTimeUT) ? double.NaN : (instance.transitionTimeUT - Planetarium.GetUniversalTime());
                }
            }
        }

        /// <summary>
        /// Do necessary calculations around atmosphere tracking. Returns true if there's anything to display.
        /// It's okay to call frequently, since it uses an update timer to avoid spamming the CPU.
        /// </summary>
        /// <returns></returns>
        private bool Recalculate()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null) return false;

            bool shouldDisplay = displayEnabled;

            // Only display for bodies with atmospheres.
            shouldDisplay &= (FlightGlobals.currentMainBody != null) && FlightGlobals.currentMainBody.atmosphere;

            double timeUntil = double.NaN;
            if (shouldDisplay)
            {
                DateTime now = DateTime.Now;
                if (now > nextUpdate)
                {
                    nextUpdate = now + UPDATE_INTERVAL;
                    transitionTimeUT = CalculateTimeAtTransition(vessel, out transitionVerb);
                }
                if (double.IsNaN(transitionTimeUT))
                {
                    timeUntil = double.NaN;
                    shouldDisplay = false;
                }
                else
                {
                    timeUntil = transitionTimeUT - Planetarium.GetUniversalTime();
                }
            }

            if (shouldDisplay)
            {
                int remainingSeconds = ImpactTracker.AsInteger(timeUntil);
                if (remainingSeconds != secondsUntilTransition)
                {
                    secondsUntilTransition = remainingSeconds;
                    transitionDescription = string.Format("{0} in {1}", transitionVerb, TimeFormatter.Default.format(ImpactTracker.AsInteger(secondsUntilTransition)));
                }
            }
            else
            {
                Reset();
            }
            return shouldDisplay;
        }

        /// <summary>
        /// Gets the UT time when the vessel will enter or exit atmosphere. Returns
        /// NaN if it's not applicable.  This is a somewhat computationally expensive
        /// call, since it does iterative calculations, so it shouldn't be called on
        /// every single frame.
        /// </summary>
        /// <param name="vessel"></param>
        /// <param name="verb"></param>
        /// <returns></returns>
        private double CalculateTimeAtTransition(Vessel vessel, out string verb)
        {
            // Note that this function only gets called if we've already determined that the
            // current celestial body has an atmosphere.

            // If we're landed, there's nothing to do.
            if (vessel.LandedOrSplashed)
            {
                verb = "N/A";
                return double.NaN;
            }

            return vessel.IsInAtmosphere() ? CalculateExitTime(vessel, out verb) : CalculateEntryTime(vessel, out verb);
        }

        /// <summary>
        /// Gets the UT time when the vessel will exit the atmosphere. Returns
        /// NaN if it's not applicable.
        /// </summary>
        /// <param name="vessel"></param>
        /// <param name="verb"></param>
        /// <returns></returns>
        private double CalculateExitTime(Vessel vessel, out string verb)
        {
            verb = "N/A";
            if (vessel.patchedConicSolver == null) return double.NaN;
            Orbit orbit = vessel.patchedConicSolver.orbit;
            if (orbit == null) return double.NaN;

            // Note that this function only gets called if we're already inside the atmosphere.
            double atmosphereHeight = FlightGlobals.currentMainBody.atmosphereDepth;

            // Does our orbit leave the atmosphere at all?
            double apoapsis = (orbit.eccentricity < 1.0) ? orbit.ApA : double.PositiveInfinity;
            if (apoapsis < atmosphereHeight) return double.NaN;

            // Even if it does:  If we're traveling downwards, we'd better have a periapsis
            // above ground level or we will not go to space today.
            if ((vessel.verticalSpeed < 0.0) && (orbit.PeA <= 0.0)) return double.NaN;

            // Okay, we're theoretically leaving atmosphere at some point (even
            // though drag might stop us before we do, in reality). Figure out
            // when, ignoring drag.
            verb = "Exit atm";
            double exitTime = CalculateTimeAtAltitude(vessel, atmosphereHeight, maxTimeToExit);
            return (exitTime < Planetarium.GetUniversalTime() + maxTimeToExit) ? exitTime : double.NaN;
        }

        /// <summary>
        /// Gets the UT time when the vessel will enter the atmosphere. Returns
        /// NaN if it's not applicable.
        /// </summary>
        /// <param name="vessel"></param>
        /// <param name="verb"></param>
        /// <returns></returns>
        private double CalculateEntryTime(Vessel vessel, out string verb)
        {
            verb = "N/A";
            if (vessel.patchedConicSolver == null) return double.NaN;
            Orbit orbit = vessel.patchedConicSolver.orbit;
            if (orbit == null) return double.NaN;

            // Note that this function only gets called if we're already outside the atmosphere.
            double atmosphereHeight = FlightGlobals.currentMainBody.atmosphereDepth;

            // Does our orbit intersect atmosphere at all?
            double periapsis = orbit.PeA;
            if (periapsis > atmosphereHeight) return double.NaN;

            // Even if it does:  if we're on an escape trajectory and climbing,
            // we'll never hit atmosphere.
            if (!orbit.IsClosed() && (vessel.verticalSpeed > 0.0)) return double.NaN;

            // Okay, we're going to enter atmosphere at some point.  Figure out when.
            verb = "Reentry";
            double entryTime = CalculateTimeAtAltitude(vessel, atmosphereHeight, maxTimeToEntry);
            return (entryTime < Planetarium.GetUniversalTime() + maxTimeToEntry) ? entryTime : double.NaN;
        }

        /// <summary>
        /// Calculate the UT time when the vessel reaches the specified altitude. Returns
        /// positive infinity if that won't happen before we reach the max allowed calculation time.
        /// </summary>
        /// <param name="vessel"></param>
        /// <param name="targetAltitude"></param>
        /// <returns></returns>
        private double CalculateTimeAtAltitude(Vessel vessel, double targetAltitude, double maxTimeToTransition)
        {
            Orbit orbit = vessel.patchedConicSolver.orbit;
            double targetRadius = targetAltitude + orbit.referenceBody.Radius;
            double now = Planetarium.GetUniversalTime();
            double maxTimeLimit = now + maxTimeToTransition;
            double lastTime = now;
            double lastRadius = orbit.GetRadiusAtUT(now);
            int initialSide = Math.Sign(lastRadius - targetRadius);

            // Skip ahead 30 seconds at a time, until we go overtime or go past transition
            double currentTime = lastTime;
            double currentRadius = lastRadius;
            while (true)
            {
                currentTime = lastTime + 30; // skip ahead half a minute
                currentRadius = orbit.GetRadiusAtUT(currentTime);
                if (currentRadius < orbit.referenceBody.Radius) return double.PositiveInfinity; // lithobraking
                if (Math.Sign(currentRadius - targetRadius) != initialSide) break; // crossed the boundary
                if (currentTime > maxTimeLimit) return double.PositiveInfinity; // boundary crossing is too far into future to care about
                lastTime = currentTime;
                lastRadius = currentRadius;
            }

            // Okay, so now we have two points in time, 30 seconds apart, that are located
            // on either side of the transition. Do a binary search to zero in on exactly
            // when the transition happens.
            double lastDelta = Math.Abs(lastRadius - targetRadius);
            double currentDelta = Math.Abs(currentRadius - targetRadius);
            for (int i = 0; i < 10; i++)
            {
                double middleTime = 0.5 * (lastTime + currentTime);
                double middleRadius = orbit.GetRadiusAtUT(middleTime);
                double middleDelta = Math.Abs(middleRadius - targetRadius);
                if (lastDelta < currentDelta)
                {
                    currentTime = middleTime;
                    currentRadius = middleRadius;
                    currentDelta = middleDelta;
                }
                else
                {
                    lastTime = middleTime;
                    lastRadius = middleRadius;
                    lastDelta = middleDelta;
                }
            }

            // That's good enough.
            return currentTime;
        }

        private void Reset()
        {
            transitionTimeUT = double.NaN;
            secondsUntilTransition = -1;
            transitionVerb = null;
            transitionDescription = null;
        }

        private bool HasInfo
        {
            get { return !double.IsNaN(transitionTimeUT); }
        }
    }
}
