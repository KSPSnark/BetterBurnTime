using UnityEngine;

namespace BetterBurnTime
{
    /// <summary>
    /// This PartModule can be used to decorate SRBs. It causes the right-click menu
    /// (both in the vehicle editor and in flight) to show the remaining burn time
    /// for the part.
    class ModuleEngineBurnTime : PartModule, IModuleInfo
    {
        private const string ZERO_SECONDS = "0:00";
        private const string UNDER_ONE_SECOND = "< 1s";
        private const string NOT_APPLICABLE = "n/a";
        private ModuleEngines engineModule = null;
        private PartResource fuel = null;
        private State state = State.NOT_APPLICABLE;
        private int burnSeconds = -1;
        private double vacuumIsp = double.NaN;

        /// <summary>
        /// Text to display in right-click menu for burn time.
        /// </summary>
        [KSPField(guiName = "Burn Time", guiActive = true, guiActiveEditor = true)]
        public string burnTimeDisplay = NOT_APPLICABLE;
        private BaseField burnTimeDisplayField { get { return Fields["burnTimeDisplay"]; } }

        /// <summary>
        /// Called when the module is starting up.
        /// </summary>
        /// <param name="state"></param>
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Initialize();
        }

        /// <summary>
        /// Called on every frame.
        /// </summary>
        void Update()
        {
            if (engineModule == null) return; // nothing to do

            // What's the current calculated burn time?
            double newBurnTime = CalculateBurnTime();

            // See whether we need to update the display.
            State newState = StateOf(newBurnTime);
            int newBurnSeconds = double.IsNaN(newBurnTime) ? -1 : (int)newBurnTime;
            if (newState == state)
            {
                if ((state != State.MULTIPLE_SECONDS) || (newBurnSeconds == burnSeconds))
                {
                    // Nope, no change, nothing to do
                    return;
                }
            }

            // Okay, it's changed-- we need to update the display.
            switch (newState)
            {
                case State.ZERO:
                    burnTimeDisplay = ZERO_SECONDS;
                    break;
                case State.UNDER_ONE_SECOND:
                    burnTimeDisplay = UNDER_ONE_SECOND;
                    break;
                case State.MULTIPLE_SECONDS:
                    burnTimeDisplay = TimeFormatter.Default.format(newBurnSeconds);
                    break;
                default:
                    burnTimeDisplay = NOT_APPLICABLE;
                    break;
            }
            burnSeconds = newBurnSeconds;
            state = newState;
        }

        private void Initialize()
        {
            if (part == null) return; // nothing to do, if there's no part!
            if (engineModule != null) return; // already initialized

            engineModule = FindEngineModule(part);
            if (engineModule == null)
            {
                burnTimeDisplayField.guiActive = false;
                burnTimeDisplayField.guiActiveEditor = false;
            }
            else
            {
                fuel = part.Resources[engineModule.propellants[0].name];
                vacuumIsp = engineModule.atmosphereCurve.Evaluate(0);
            }
        }

        /// <summary>
        /// Gets the part's burn time, in seconds.
        /// </summary>
        /// <returns></returns>
        private double CalculateBurnTime()
        {
            if (engineModule == null) return double.NaN;
            if (engineModule.thrustPercentage < 0.01F) return double.PositiveInfinity;

            // work out the fuel consumption rate in tons/sec
            double engineKilonewtons = engineModule.ThrustLimit();
            double engineFuelConsumption = engineKilonewtons / (BetterBurnTime.KERBIN_GRAVITY * vacuumIsp); // tons/sec

            // how many tons of resource do we have?
            double tonsFuel = fuel.amount * fuel.info.density;

            return tonsFuel / engineFuelConsumption;
        }

        /// <summary>
        /// Find the first ModuleEngines on the part, or null if not present.
        /// </summary>
        /// <returns></returns>
        private static ModuleEngines FindEngineModule(Part part)
        {
            // First, try to find any ModuleEngines
            ModuleEngines engineModule = null;
            for (int i = 0; i < part.Modules.Count; ++i)
            {
                engineModule = part.Modules[i] as ModuleEngines;
                if (engineModule != null) break;
            }
            if (engineModule == null)
            {
                Logging.Error("ModuleEngineBurnTime is inactive for " + part.name + " (no ModuleEngines found on part)");
                return null;
            }
            // Make sure it has a locked throttle (otherwise we can't predict its burn time)
            if (!engineModule.throttleLocked)
            {
                Logging.Error("ModuleEngineBurnTime is inactive for " + part.name + " (only works for locked-throttle engines)");
                return null;
            }
            // Has to have propellants and resources
            if ((engineModule.propellants == null) || (engineModule.propellants.Count < 1) || (part.Resources == null))
            {
                Logging.Error("ModuleEngineBurnTime is inactive for " + part.name + " (must have propellants & resources)");
                return null;
            }
            // Has to be a single-propellant engine
            if (engineModule.propellants.Count > 1)
            {
                Logging.Error("ModuleEngineBurnTime is inactive for " + part.name + " (multi-propellant engines are not supported)");
                return null;
            }
            // Make sure the propellant is usable for this module
            Propellant propellant = engineModule.propellants[0];
            PartResource resource = engineModule.part.Resources[propellant.name];
            if (resource == null)
            {
                Logging.Error("ModuleEngineBurnTime is inactive for " + part.name + " (missing " + propellant.name + ")");
                return null;
            }
            if (resource.info.resourceFlowMode != ResourceFlowMode.NO_FLOW)
            {
                Logging.Error("ModuleEngineBurnTime is inactive for " + part.name + " (" + propellant.name + "is flowable)");
                return null;
            }

            // Looks good!
            return engineModule;
        }

        private static State StateOf(double time)
        {
            if (double.IsNaN(time) || double.IsInfinity(time)) return State.NOT_APPLICABLE;
            if (time == 0) return State.ZERO;
            if (time < 1.0) return State.UNDER_ONE_SECOND;
            return State.MULTIPLE_SECONDS;
        }

        private enum State
        {
            ZERO,
            UNDER_ONE_SECOND,
            MULTIPLE_SECONDS,
            NOT_APPLICABLE
        }

        //------------------------ IModuleInfo implementation -------------------------------
        /// <summary>
        /// Information to display in the part's tooltip in the parts list in the editor.
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
            return GetPrimaryField();
        }

        /// <summary>
        /// Gets the title to display on the info panel that shows over on the
        /// right-hand-side of the part's tooltip window in the editor.
        /// </summary>
        /// <returns></returns>
        public string GetModuleTitle()
        {
            // I actually would prefer not to *have* any panel over there, but
            // I haven't been able to figure out any way to do that while still
            // keeping the "primary field" info displayed.
            //
            // Given that I have to have a panel, I'd like to keep it minimalist.
            // I'd prefer to just return null here-- which causes no title to be
            // displayed, thus reducing visual clutter-- but although the stock
            // game is just fine with that, it turns out that B9PartSwitch (a
            // popular and very common mod) causes KSP to barf and hang on the
            // loading screen. Therefore, provide a title here... under duress.
            return "SRB burn time";
        }

        public Callback<Rect> GetDrawModulePanelCallback()
        {
            // Just return null because we're not drawing any custom panel.
            return null;
        }

        /// <summary>
        /// Gets the info to display in the "main" part of the part's tooltip
        /// window, along with all the other stats.
        /// </summary>
        /// <returns></returns>
        public string GetPrimaryField()
        {
            // This gets called during the part-loading phase when KSP is starting up,
            // so OnStart() hasn't been called. Therefore we have to force it to initialize
            // (and update, once) here, so that the relevant info will be populated.
            Initialize();
            Update();

            return string.Format("<b>Burn time:</b> {0}", burnTimeDisplay);
        }
    }
}
