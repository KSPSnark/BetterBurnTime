using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterBurnTime
{
    /// <summary>
    /// Keeps track of engines and propellant tanks on the active ship.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class Propulsion : MonoBehaviour
    {
        private static Propulsion instance = null;

        private Guid lastVesselId;
        private int lastVesselPartCount;
        private List<Part> engines;
        private List<Part> tanks;

        public void Start()
        {
            instance = this;
            lastVesselId = Guid.Empty;
            lastVesselPartCount = -1;
            engines = new List<Part>();
            tanks = new List<Part>();
        }

        /// <summary>
        /// Called on every frame.
        /// </summary>
        public void Update()
        {
            // Track whether the vessel has changed since last update
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null) return;
            bool needRefresh = false;
            if (vessel.id != lastVesselId)
            {
                lastVesselId = vessel.id;
                lastVesselPartCount = vessel.parts.Count;
                needRefresh = true;
            }
            else if (vessel.parts.Count != lastVesselPartCount)
            {
                lastVesselPartCount = vessel.parts.Count;
                needRefresh = true;
            }

            if (needRefresh)
            {
                // Yes, it's changed. Update our status.
                ListEngines(vessel);
                ListFuelTanks(vessel);
            }
        }

        public static bool ShouldIgnorePropellant(string propellantName)
        {
            return "ElectricCharge".Equals(propellantName);
        }

        /// <summary>
        /// Gets all engines on the current ship (regardless of whether they're active
        /// or have available fuel).
        /// </summary>
        public static List<Part> Engines
        {
            get { return instance.engines; }
        }

        /// <summary>
        /// Gets all fuel tanks on the current ship (regardless of whether they are
        /// active or contain any fuel) that are capable of containing any of the
        /// fuel that the ship's engines use.
        /// </summary>
        public static List<Part> Tanks
        {
            get { return instance.tanks; }
        }

        /// <summary>
        /// Build a list of all engines on the vessel.
        /// </summary>
        /// <param name="vessel"></param>
        private void ListEngines(Vessel vessel)
        {
            engines.Clear();
            for (int index = 0; index < vessel.parts.Count; ++index)
            {
                Part part = vessel.parts[index];
                if (part.HasModule<ModuleEngines>())
                {
                    engines.Add(part);
                }
            }
        }

        /// <summary>
        /// Build a list of all fuel tanks that could potentially supply our engines.
        /// </summary>
        /// <param name="vessel"></param>
        private void ListFuelTanks(Vessel vessel)
        {
            // Build a list of all tanks that contain any resources of nonzero mass
            tanks.Clear();
            for (int index = 0; index < vessel.parts.Count; ++index)
            {
                Part part = vessel.parts[index];
                if (HasAnyResources(part))
                {
                    tanks.Add(part);
                }
            }
        }

        /// <summary>
        /// Returns true if the part contains any resources of nonzero mass.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="propellants"></param>
        /// <returns></returns>
        private static bool HasAnyResources(Part part)
        {
            for (int index = 0; index < part.Resources.Count; ++index)
            {
                if (part.Resources[index].info.density > 0) return true;
            }
            return false;
        }
    }
}
