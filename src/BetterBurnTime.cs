using System;
using UnityEngine;
using KSP.IO;

namespace BetterBurnTime
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BetterBurnTime : MonoBehaviour
    {
        // Treat any acceleration smaller than this as zero.
        private static readonly double ACCELERATION_EPSILON = 0.000001;

        // Time thresholds for using various string formats
        private static readonly int THRESHOLD_SECONDS = 120;
        private static readonly int THRESHOLD_MINUTES_SECONDS = 120 * 60;
        private static readonly int THRESHOLD_HOURS_MINUTES_SECONDS = 6 * 60 * 60;
        private static readonly int THRESHOLD_HOURS_MINUTES = 4 * THRESHOLD_HOURS_MINUTES_SECONDS;

        // Kerbin gravity, needed for working with Isp
        private static readonly double KERBIN_GRAVITY = 9.81;

        private static readonly string ESTIMATED_BURN_LABEL = "Est. Burn: ";

        private bool useSimpleAcceleration = false;
        private bool wasFuelCheat = false;
        private int lastEngineCount;
        private ShipState vessel;
        private string lastUpdateText;
        private int lastBurnTime;
        private Tally propellantsConsumed;

        public void Start()
        {
            PluginConfiguration config = PluginConfiguration.CreateForType<BetterBurnTime>();
            config.load();
            useSimpleAcceleration = config.GetValue<bool>("UseSimpleAcceleration", useSimpleAcceleration);
            ImpactTracker.displayEnabled = config.GetValue<bool>("ShowImpactTracker", ImpactTracker.displayEnabled);
            ImpactTracker.maxTimeToImpact = config.GetValue<double>("MaxTimeToImpact", ImpactTracker.maxTimeToImpact);
            ClosestApproachTracker.displayEnabled = config.GetValue<bool>("ShowClosestApproachTracker", ClosestApproachTracker.displayEnabled);
            ClosestApproachTracker.maxTimeUntilEncounter = config.GetValue<double>("MaxTimeUntilEncounter", ClosestApproachTracker.maxTimeUntilEncounter);
            ClosestApproachTracker.maxClosestApproachDistanceKm = config.GetValue<double>("MaxClosestApproachDistanceKm", ClosestApproachTracker.maxClosestApproachDistanceKm);
            ClosestApproachTracker.minTargetDistance = config.GetValue<double>("MinTargetDistanceMeters", ClosestApproachTracker.minTargetDistance);
            config.save();
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
                if (double.IsNaN(dVrequired))
                {
                    // No impact info is available. Do we have closest-approach info?
                    dVrequired = ClosestApproachTracker.Velocity;
                    if (double.IsNaN(dVrequired))
                    {
                        // No closest-approach info available either, use the maneuver dV remaining.
                        dVrequired = BurnInfo.DvRemaining;
                    }
                    else
                    {
                        // We have closest-approach info, use the description from that.
                        customDescription = ClosestApproachTracker.Description;
                    }
                }
                else
                {
                    // We have impact info, use the description from that.
                    customDescription = ImpactTracker.Description;
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
                    string burnLabel = FormatBurnTime(burnSeconds);
                    lastUpdateText = ESTIMATED_BURN_LABEL + FormatBurnTime(burnSeconds);
                    if (isInsufficientFuel)
                    {
                        lastUpdateText = ESTIMATED_BURN_LABEL + "(~" + burnLabel + ")";
                    } else
                    {
                        lastUpdateText = ESTIMATED_BURN_LABEL + burnLabel;
                    }
                }
                BurnInfo.Duration = lastUpdateText;
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
        /// Given a burn time in seconds, get a string representation.
        /// </summary>
        /// <param name="totalSeconds"></param>
        /// <returns></returns>
        private static string FormatBurnTime(int totalSeconds)
        {
            if (totalSeconds < 0)
            {
                return "N/A";
            }
            if (totalSeconds == 0)
            {
                return "<1s";
            }
            if (totalSeconds <= THRESHOLD_SECONDS)
            {
                return totalSeconds.ToString("#s");
            }
            else if (totalSeconds <= THRESHOLD_MINUTES_SECONDS)
            {
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                return string.Format("{0}m{1}s", minutes, seconds);
            }
            else if (totalSeconds <= THRESHOLD_HOURS_MINUTES_SECONDS)
            {
                int hours = totalSeconds / 3600;
                int minutes = (totalSeconds % 3600) / 60;
                int seconds = totalSeconds % 60;
                return string.Format("{0}h{1}m{2}s", hours, minutes, seconds);
            }
            else if (totalSeconds <= THRESHOLD_HOURS_MINUTES)
            {
                int hours = totalSeconds / 3600;
                int minutes = (totalSeconds % 3600) / 60;
                return string.Format("{0}h{1}m", hours, minutes);
            }
            else
            {
                int hours = THRESHOLD_HOURS_MINUTES / 3600;
                return hours.ToString(">#h");
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
                        double engineTotalFuelConsumption = engineKilonewtons / (KERBIN_GRAVITY * engine.realIsp); // tons/sec
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
    }
}
