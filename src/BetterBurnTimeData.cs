using System;

namespace BetterBurnTime
{
    /// <summary>
    /// This class is intended as the "public API" of the BetterBurnTime mod.  It exposes
    /// the various numbers that BetterBurnTime produces, in a way that's programmatically
    /// accessible to other mods. Since it's considered to be a public API, the author will
    /// try to ensure, where possible, that this API will stay consistent even when other
    /// internals of BetterBurnTime may change around. This is so that mods which may
    /// depend on this API won't break.
    ///
    /// Since this is a VesselModule, KSP will automatically add an instance of it to every
    /// ship in the game. The default state for a freshly-created BetterBurnTimeData is
    /// for it to contain no data (e.g. burn type will be None, values will be placeholders
    /// such as NaN, -1, etc.).  Consumers of the data here need to check for validity
    /// (e.g. "is it NaN?") whenever reading a value, and handle the not-valid case appropriately.
    ///
    /// Note that the BetterBurnTime mod only actually tracks the currently piloted vessel.
    /// The BetterBurnTimeData object for the current vessel will be continuously updated
    /// by the mod.  The BetterBurnTimeData objects for all other vessels (i.e. those that
    /// aren't currently being piloted) won't be updated, they'll just sit there with obsolete
    /// data.  Therefore, you should only ever use the BetterBurnTimeData object for the
    /// currently piloted vessel.  You can access this via the static Current property
    /// on the class, which will return null if there's not any current vessel.
    ///
    /// How you consume this API depends on whether your mod has a "hard" or a "soft" depedency
    /// on BetterBurnTime.
    ///
    /// If you have a "hard" dependency-- i.e. if your mod's assembly has an actual reference
    /// to BetterBurnTime's assembly, and won't load unless BetterBurnTime is present-- then
    /// you can just call the Current property, and (if it's not null) access the data members
    /// on it.
    ///
    /// If you have a "soft" dependency-- i.e. no assembly reference, and you want your mod
    /// to be able to run regardless of whether BetterBurnTime is present-- then you can
    /// ask the currently-piloted vessel for its VesselModule list, iterate the list until
    /// you find one whose class name is BetterBurnTimeData, then access the properties
    /// by name via the Fields property on it.
    ///
    /// For an example of code that talks to this API via a soft dependency, see the
    /// BetterBurnTimeDataExample.cs file.
    /// </summary>
    public class BetterBurnTimeData : VesselModule
    {
        private static Guid currentVesselId = Guid.Empty;
        private static BetterBurnTimeData currentData = null;

        /// <summary>
        /// Indicates which type of data is currently being reported (maneuver, rendezvous,
        /// or impact). Will be None if no data is available.
        /// </summary>
        [KSPField]
        public BurnType burnType = BurnType.None;

        /// <summary>
        /// Gets the estimated burn time (for maneuver node, target rendezvous, or
        /// surface impact, whichever is currently relevant).
        ///
        /// Will be NaN if not valid (e.g. there's no maneuver node, no imminent
        /// target rendezvous, no imminent impact).
        ///
        /// Will be PositiveInfinity if the data is valid but it's physically
        /// impossible to do the burn (e.g. you're out of fuel, or have no active
        /// engines).
        /// </summary>
        [KSPField]
        public double burnTime = double.NaN;

        /// <summary>
        /// Gets the dV, in meters per second, needed for the burn.
        ///
        /// Will be NaN if not valid (e.g. there's no maneuver node, no imminent
        /// target rendezvous, no imminent impact).
        /// </summary>
        [KSPField]
        public double dV = double.NaN;

        /// <summary>
        /// The time remaining, in seconds, until the event currently being tracked (maneuver
        /// node, rendezvous, or impact). Will be NaN if no data is currently available.
        /// </summary>
        [KSPField]
        public double timeUntil = double.NaN;

        /// <summary>
        /// Indicates whether we actually have enough fuel to do the burn currently
        /// indicated by the burnTime field. This is useful for displaying warning
        /// indicators (e.g. "you can't actually handle the current maneuver" or
        /// the like).  If there's not currently any data (e.g. if burnType is
        /// currently None), then this value will always be false.
        /// </summary>
        [KSPField]
        public bool isInsufficientFuel = false;

        /// <summary>
        /// Gets whether this structure is currently valid and contains data.
        /// </summary>
        public bool IsValid
        {
            get { return burnType != BurnType.None; }
        }

        /// <summary>
        /// Indicates what type of burn the data is currently indicating.
        /// </summary>
        public enum BurnType
        {
            /// <summary>
            /// No data is currently relevant; all values will be NaN.
            /// </summary>
            None,

            /// <summary>
            /// The data is for an upcoming maneuver node.
            /// </summary>
            Maneuver,

            /// <summary>
            /// The data is for an upcoming target rendezvous.
            /// </summary>
            Rendezvous,

            /// <summary>
            /// The data is for an upcoming surface impact.
            /// </summary>
            Impact
        }

        /// <summary>
        /// Get the BetterBurnTimeData object for the currently piloted vessel. Returns null
        /// if that's not possible (for example, if you're in the vehicle editor).
        /// </summary>
        public static BetterBurnTimeData Current
        {
            get
            {
                // is current vessel even available?
                if (!FlightGlobals.ready) return null;
                Vessel currentVessel = FlightGlobals.ready ? FlightGlobals.ActiveVessel : null;
                if (currentVessel == null)
                {
                    currentVesselId = Guid.Empty;
                    currentData = null;
                    return null;
                }

                // Do we already have the necessary reference cached?
                if ((currentVessel.id != currentVesselId) || (currentData == null))
                {
                    // Either this is the first time this property has been accessed, or the
                    // current vessel has changed since last time.  Find the updated version.
                    for (int i = 0; i < currentVessel.vesselModules.Count; ++i)
                    {
                        currentData = currentVessel.vesselModules[i] as BetterBurnTimeData;
                        if (currentData != null)
                        {
                            // found it!
                            currentVesselId = currentVessel.id;
                            break;
                        }
                    }
                }
                return currentData;
            }
        }

        /// <summary>
        /// Reset all fields to uninitialized state.
        /// </summary>
        internal void Reset()
        {
            burnType = BurnType.None;
            burnTime = double.NaN;
            dV = double.NaN;
            timeUntil = double.NaN;
            isInsufficientFuel = false;
        }
    }
}
