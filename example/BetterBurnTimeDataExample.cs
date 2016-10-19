using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterBurnTimeDataExample
{
    /// <summary>
    /// Sample class demonstrating how to access the BetterBurnTime API as a "soft" dependency.
    ///
    /// It just checks the API every few seconds and logs the information it sees.
    ///
    /// Note that if your mod has a "hard" dependency on BetterBurnTime (i.e. has an actual assembly
    /// reference, so that your mod won't even load unless BetterBurnTime is present), then you
    /// don't have to go through all this rigamarole; you can just use the BetterBurnTimeData
    /// class directly, and call its Current property to get the data for the current vessel.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class BetterBurnTimeDataExample : MonoBehaviour
    {
        // These are the important strings you'll need to use to access the BetterBurnTime API
        // dynamically at run time.
        private const string API_MODULE_CLASS = "BetterBurnTimeData";
        private const string BURN_TYPE_FIELD = "burnType";
        private const string BURN_TIME_FIELD = "burnTime";
        private const string DV_FIELD = "dV";
        private const string TIME_UNTIL_FIELD = "timeUntil";
        private const string INSUFFICIENT_FUEL_FIELD = "isInsufficientFuel";
        private const string NO_DATA = "None";

        // Stuff specific to this example.
        private static readonly TimeSpan UPDATE_INTERVAL = TimeSpan.FromSeconds(2);
        private DateTime nextUpdate = DateTime.MinValue;

        /// <summary>
        /// This gets called on every update cycle.
        /// </summary>
        public void Update()
        {
            // Don't do anything until it's time for the next log message.
            DateTime now = DateTime.Now;
            if (now < nextUpdate) return;
            nextUpdate = now + UPDATE_INTERVAL;

            // Do we have data available?
            if (!FlightGlobals.ready || (FlightGlobals.ActiveVessel == null)) return;

            // Find the VesselModule we want.
            List<VesselModule> modules = FlightGlobals.ActiveVessel.vesselModules;
            VesselModule data = null;
            for (int i = 0; i < modules.Count; ++i)
            {
                if (API_MODULE_CLASS == modules[i].GetType().Name)
                {
                    data = modules[i];
                    break;
                }
            }
            if (data == null)
            {
                // No data is available. For example, this will happen if we're
                // not in the flight scene, since BetterBurnTime only runs then.
                return;
            }
            else
            {
                DoStuffWith(data);
            }
        }

        /// <summary>
        /// This is called when we want to do something with the API data (log a message,
        /// in this example).
        /// </summary>
        /// <param name="data"></param>
        private void DoStuffWith(VesselModule data)
        {
            // Get the information about it.
            string burnType = data.Fields[BURN_TYPE_FIELD].GetValue(data).ToString();
            double burnTime = data.Fields[BURN_TIME_FIELD].GetValue<double>(data);
            double dV = data.Fields[DV_FIELD].GetValue<double>(data);
            double timeUntil = data.Fields[TIME_UNTIL_FIELD].GetValue<double>(data);
            bool isInsufficientFuel = data.Fields[INSUFFICIENT_FUEL_FIELD].GetValue<bool>(data);

            // Check to make sure we actually have data.
            if (NO_DATA == burnType)
            {
                Debug.Log("[Example] No burn info is available.");
                return;
            }

            // Okay, we have data, do with it as we will.
            string message = string.Format(
                "[Example] {0} of {1:0.0} m/s {2}: {3}",
                burnType,
                dV,
                (double.IsNaN(timeUntil)) ? "(overdue)" : string.Format("in {0:0.0} seconds", timeUntil),
                double.IsInfinity(burnTime) ? "N/A" : string.Format("{0:0.0} seconds to burn", burnTime));
            if (isInsufficientFuel)
            {
                message += " (Warning! Not enough fuel)!";
            }
            Debug.Log(message);
        }
    }
}
