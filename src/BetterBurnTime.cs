using System;
using UnityEngine;

namespace BetterBurnTime
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BetterBurnTime : MonoBehaviour
    {
        // Treat any acceleration smaller than this as zero.
        private static readonly double ACCELERATION_EPSILON = 0.000001;

        // Kerbin gravity, needed for working with Isp
        private static readonly double KERBIN_GRAVITY = 9.81;

        private static readonly string ESTIMATED_BURN_LABEL = "Est. Burn: ";

        // Configurable values
        private static readonly bool useSimpleAcceleration = Configuration.useSimpleAcceleration;

        // Other private data
        private bool wasFuelCheat = false;
        private int lastEngineCount;
        private ShipState vessel;
        private string lastUpdateText;
        private int lastBurnTime;
        private Tally propellantsConsumed;

        public void Start()
        {
            wasFuelCheat = CheatOptions.InfiniteFuel;
            if (useSimpleAcceleration)
            {
                Logging.Log("Using simple acceleration model");
            }
            else
            {
                Logging.Log("Using complex acceleration model");
                if (wasFuelCheat)
                {
                    Logging.Log("Infinite fuel cheat is turned on, using simple acceleration model");
                }
                else
                {
                    Logging.Log("Using complex acceleration model");
                }
            }
            lastEngineCount = -1;

            vessel = new ShipState();
            lastUpdateText = null;
            lastBurnTime = int.MinValue;
        }

        public void LateUpdate()
        {
            logFuelCheatActivation();
            try
            {
                if (!BurnInfo.IsInitialized) return; // can't do anything

                string customDescription = null;
                double dVrequired = ImpactTracker.ImpactSpeed;
                int timeUntil = -1;
                if (double.IsNaN(dVrequired))
                {
                    // No impact info is available. Do we have closest-approach info?
                    dVrequired = ClosestApproachTracker.Velocity;
                    if (double.IsNaN(dVrequired))
                    {
                        // No closest-approach info available either, use the maneuver dV remaining.
                        dVrequired = BurnInfo.DvRemaining;
                        timeUntil = SecondsUntilNode();
                    }
                    else
                    {
                        // We have closest-approach info, use the description from that.
                        customDescription = ClosestApproachTracker.Description;
                        timeUntil = ClosestApproachTracker.TimeUntil;
                    }
                }
                else
                {
                    // We have impact info, use the description from that.
                    customDescription = ImpactTracker.Description;
                    // TODO: enable countdown to retro-burn, not doing it now 'coz it needs more math & logic
                    // timeUntil = ImpactTracker.TimeUntil;
                }

                // At this point, either we have a dVrequired or not. If we have one, we might
                // have a description (meaning it's one of our custom trackers from this mod)
                // or we might not (meaning "leave it alone at let the stock game decide what to say").

                if (double.IsNaN(dVrequired)) return;

                vessel.Refresh();
                propellantsConsumed = new Tally();
                bool isInsufficientFuel;
                double floatBurnSeconds = GetBurnTime(dVrequired, out isInsufficientFuel);
                int burnSeconds = double.IsInfinity(floatBurnSeconds) ? -1 : (int)(0.5 + floatBurnSeconds);
                if (burnSeconds != lastBurnTime)
                {
                    lastBurnTime = burnSeconds;
                    if (isInsufficientFuel)
                    {
                        lastUpdateText = ESTIMATED_BURN_LABEL + TimeFormatter.Default.warn(burnSeconds);
                    }
                    else
                    {
                        lastUpdateText = ESTIMATED_BURN_LABEL + TimeFormatter.Default.format(burnSeconds);
                    }
                }
                BurnInfo.Duration = lastUpdateText;
                BurnInfo.Countdown = Countdown.ForSeconds(timeUntil - burnSeconds / 2);
                if (customDescription == null)
                {
                    // No custom description available, turn off the alternate display
                    BurnInfo.AlternateDisplayEnabled = false;
                }
                else
                {
                    // We have alternate info to show
                    BurnInfo.TimeUntil = customDescription;
                    BurnInfo.AlternateDisplayEnabled = true;
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
                BurnInfo.Duration = e.GetType().Name + ": " + e.Message + " -> " + e.StackTrace;
            }
        }

        /// <summary>
        /// Log a message every time the fuel-cheat toggle is switched.
        /// </summary>
        private void logFuelCheatActivation()
        {
            if (CheatOptions.InfiniteFuel != wasFuelCheat)
            {
                wasFuelCheat = CheatOptions.InfiniteFuel;
                if (!useSimpleAcceleration)
                {
                    if (wasFuelCheat)
                    {
                        Logging.Log("Infinite fuel cheat activated. Will use simple acceleration model.");
                    }
                    else
                    {
                        Logging.Log("Infinite fuel cheat deactivated. Will use complex acceleration model.");
                    }
                }
            }
        }

        /// <summary>
        /// Calculate the number of seconds required to burn.
        /// </summary>
        private double GetBurnTime(double dVremaining, out bool isInsufficientFuel)
        {
            isInsufficientFuel = false;

            // How thirsty are we?
            double totalThrust; // kilonewtons
            GetThrustInfo(propellantsConsumed, out totalThrust);
            if (totalThrust < ACCELERATION_EPSILON)
            {
                // Can't thrust, will take forever.
                return double.PositiveInfinity;
            }

            // If infinite fuel is turned on, or if the "use simple acceleration" config
            // option is set, just do a simple dV calculation.
            if (CheatOptions.InfiniteFuel || useSimpleAcceleration)
            {
                return dVremaining * vessel.TotalMass / totalThrust;
            }

            // How long can we burn until we run out of fuel?
            double maxBurnTime = CalculateMaxBurnTime(vessel, propellantsConsumed);

            // How much fuel do we need to burn to get the dV we want?
            double totalConsumption = propellantsConsumed.Sum; // tons/second
            double exhaustVelocity = totalThrust / totalConsumption; // meters/second
            double massRatio = Math.Exp(dVremaining / exhaustVelocity);
            double currentTotalShipMass = vessel.TotalMass;
            double fuelMass = currentTotalShipMass * (1.0 - 1.0 / massRatio);
            double burnTimeNeeded = fuelMass / totalConsumption;
            if (burnTimeNeeded < maxBurnTime)
            {
                // We can burn that long! We're done.
                return burnTimeNeeded;
            }

            // Uh oh.  There's not enough fuel to get that much dV.  Here's what we'll do:
            // Take the amount of burn time that we can actually handle, and do that.
            // Then assume that we'll do constant acceleration at that speed for the
            // remainder of the dV.
            double fuelBurned = totalConsumption * maxBurnTime; // tons
            double emptyMass = currentTotalShipMass - fuelBurned;
            double realdV = (totalThrust / totalConsumption) * Math.Log(currentTotalShipMass / emptyMass);
            double highestAcceleration = totalThrust / emptyMass;
            double overflowdV = dVremaining - realdV;
            isInsufficientFuel = true;
            return maxBurnTime + overflowdV / highestAcceleration;
        }

        /// <summary>
        /// Get the vessel's acceleration ability, in m/s2
        /// </summary>
        /// <param name="propellantsConsumed">All the propellants consumed, in tons/second for each one</param>
        /// <param name="totalThrust">The total thrust produced, in kilonewtons</param>
        private void GetThrustInfo(
            Tally propellantsConsumed,
            out double totalThrust)
        {
            // Add up all the thrust for all the active engines on the vessel.
            // We do this as a vector because the engines might not be parallel to each other.
            Vector3 totalThrustVector = Vector3.zero;
            totalThrust = 0.0F;
            propellantsConsumed.Zero();
            Tally availableResources = vessel.AvailableResources;
            int engineCount = 0;
            foreach (ModuleEngines engine in vessel.ActiveEngines)
            {
                if (engine.thrustPercentage > 0)
                {
                    double engineKilonewtons = engine.maxThrust * engine.thrustPercentage * 0.01;
                    if (!CheatOptions.InfiniteFuel)
                    {
                        // Get the vacuum Isp this way, rather than ask for engine.realIsp, because
                        // there are mods that tinker with the atmosphere curve, which changes the
                        // actual Isp that the game uses for vacuum without updating engine.realIsp.
                        // Thanks to smjjames in the KSP forums for pointing out this bug.
                        double engineIsp = engine.atmosphereCurve.Evaluate(0);

                        double engineTotalFuelConsumption = engineKilonewtons / (KERBIN_GRAVITY * engineIsp); // tons/sec
                        double ratioSum = 0.0;
                        bool isStarved = false;
                        foreach (Propellant propellant in engine.propellants)
                        {
                            if (!ShouldIgnore(propellant.name))
                            {
                                if (!availableResources.Has(propellant.name))
                                {
                                    isStarved = true;
                                    break;
                                }
                                ratioSum += propellant.ratio;
                            }
                        }
                        if (isStarved) continue;
                        if (ratioSum > 0)
                        {
                            double ratio = 1.0 / ratioSum;
                            foreach (Propellant propellant in engine.propellants)
                            {
                                if (!ShouldIgnore(propellant.name))
                                {
                                    double consumptionRate = ratio * propellant.ratio * engineTotalFuelConsumption; // tons/sec
                                    propellantsConsumed.Add(propellant.name, consumptionRate);
                                }
                            }
                        }
                    } // if we need to worry about fuel
                    ++engineCount;
                    totalThrustVector += Propulsion.ForwardOf(engine) * (float)engineKilonewtons;
                } // if the engine is operational
            } // for each engine module on the part
            totalThrust = totalThrustVector.magnitude;
            if (engineCount != lastEngineCount)
            {
                lastEngineCount = engineCount;
                Logging.Log(engineCount.ToString("Active engines: ##0"));
            }
        }

        /// <summary>
        /// Calculate how long we can burn at full throttle until something important runs out.
        /// </summary>
        /// <param name="vessel"></param>
        /// <param name="propellantsConsumed"></param>
        /// <param name="propellantsAvailable"></param>
        /// <param name="maxBurnTime"></param>
        private static double CalculateMaxBurnTime(ShipState vessel, Tally propellantsConsumed)
        {
            double maxBurnTime = double.PositiveInfinity;
            Tally availableResources = vessel.AvailableResources;
            foreach (string resourceName in propellantsConsumed.Keys)
            {
                if (ShouldIgnore(resourceName))
                {
                    // ignore this for burn time, it's replenishable
                    continue;
                }
                if (!availableResources.Has(resourceName))
                {
                    // we're all out!
                    return 0.0;
                }
                double availableAmount = availableResources[resourceName];
                double rate = propellantsConsumed[resourceName];
                double burnTime = availableAmount / rate;
                if (burnTime < maxBurnTime) maxBurnTime = burnTime;
            }
            return maxBurnTime;
        }

        private static bool ShouldIgnore(string propellantName)
        {
            return "ElectricCharge".Equals(propellantName);
        }

        /// <summary>
        /// Gets the time until the next maneuver node, in seconds. -1 if none.
        /// </summary>
        /// <returns></returns>
        private static int SecondsUntilNode()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null) return -1;
            PatchedConicSolver solver = vessel.patchedConicSolver;
            if (solver == null) return -1;
            if ((solver.maneuverNodes == null) || (solver.maneuverNodes.Count == 0)) return -1;
            ManeuverNode node = solver.maneuverNodes[0];
            if (node == null) return -1;
            double timeUntil = node.UT - Planetarium.GetUniversalTime();
            if (timeUntil < 0) return -1;
            return (int)timeUntil;
        }
    }
}
