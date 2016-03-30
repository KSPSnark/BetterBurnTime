using System;
using System.Collections.Generic;

namespace BetterBurnTime
{
    /// <summary>
    /// This class holds state information about the ship. It may be moderately expensive
    /// to collate, in particular could be O(N) with the number of parts on the ship. Therefore
    /// such information is segregated into this class, which keeps a cached copy and updates
    /// it moderately frequently.
    /// 
    /// Anything that needs to be very responsive (i.e. high framerate) shouldn't be cached here.
    /// </summary>
    class ShipState
    {
        // Refresh interval for updating the information contained herein.
        private static readonly TimeSpan updateInterval = new TimeSpan(0, 0, 0, 0, 250);

        Guid vesselId = Guid.Empty;
        int vesselPartCount = 0;
        private DateTime lastUpdateTime = DateTime.MinValue;
        private double totalMass;
        private Tally availableResources = null;
        private List<ModuleEngines> activeEngines = new List<ModuleEngines>();

        public ShipState()
        {
        }

        /// <summary>
        /// Refresh cached information.
        /// </summary>
        public void Refresh()
        {
           if (!NeedUpdate())
            {
                return;
            }
            lastUpdateTime = DateTime.Now;
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null) return;
            vesselId = vessel.id;
            vesselPartCount = vessel.parts.Count;
            totalMass = vessel.GetTotalMass();
            activeEngines.Clear();
            for (int engineIndex = 0; engineIndex < Propulsion.Engines.Count; ++engineIndex)
            {
                Part part = Propulsion.Engines[engineIndex];
                for (int moduleIndex = 0; moduleIndex < part.Modules.Count; ++moduleIndex)
                {
                    ModuleEngines engine = part.Modules[moduleIndex] as ModuleEngines;
                    if (engine == null) continue;
                    if (!engine.isOperational) continue;
                    if (!CheatOptions.InfinitePropellant)
                    {
                        bool isDeprived = false;
                        for (int propellantIndex = 0; propellantIndex < engine.propellants.Count; ++propellantIndex)
                        {
                            Propellant propellant = engine.propellants[propellantIndex];
                            if (propellant.isDeprived && !propellant.ignoreForIsp)
                            {
                                isDeprived = true; // out of fuel!
                                break;
                            }
                        }
                        if (isDeprived) continue; // skip this engine
                    }
                    activeEngines.Add(engine);
                }
            }

            availableResources = new Tally();
            for (int tankIndex = 0; tankIndex < Propulsion.Tanks.Count; ++tankIndex)
            {
                Part part = Propulsion.Tanks[tankIndex];
                for (int resourceIndex = 0; resourceIndex < part.Resources.Count; ++resourceIndex)
                {
                    PartResource resource = part.Resources[resourceIndex];
                    if (resource.flowState)
                    {
                        availableResources.Add(resource.resourceName, resource.amount * resource.info.density);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the total mass of the ship.
        /// </summary>
        public double TotalMass
        {
            get { return totalMass; }
        }

        /// <summary>
        /// Gets the amount of available resources on the ship, in tons.
        /// </summary>
        public Tally AvailableResources
        {
            get { return availableResources; }
        }

        /// <summary>
        /// Gets all engines on the ship that are active and not fuel-starved.
        /// </summary>
        public List<ModuleEngines> ActiveEngines
        {
            get { return activeEngines; }
        }

        private bool NeedUpdate()
        {
            DateTime now = DateTime.Now;
            if (now - lastUpdateTime > updateInterval) return true;

            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null) return false;
            return (vessel.id != vesselId) || (vessel.parts.Count != vesselPartCount);
        }
    }
}
