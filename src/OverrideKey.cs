using UnityEngine;

namespace BetterBurnTime
{
    /// <summary>
    /// Tracks the on/off status of the BetterBurnTime override key (this is the right Ctrl
    /// key by default, but can be changed via config).
    ///
    /// The default behavior is "push and hold", i.e. it's active while the key is down
    /// and inactive when the key is released.  If the "sticky override" flag is turned
    /// on in config, though, then it's "press to toggle", i.e. press and release the
    /// key once to activate the override, press and release again to deactivate.
    ///
    /// Any trackers in the mod that care about the on-off status can check it via
    /// OverrideKey.IsActive.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class OverrideKey : MonoBehaviour
    {
        private static readonly string OVERRIDE_KEY = Configuration.overrideKey;
        private static readonly bool STICKY_OVERRIDE = Configuration.stickyOverride;

        private static OverrideKey instance;
        private bool isActive = false;

        /// <summary>
        /// Here when the add-on loads upon flight start.
        /// </summary>
        public void Start()
        {
            instance = this;
        }

        /// <summary>
        /// Called on each frame.
        /// </summary>
        public void LateUpdate()
        {
            if (Input.GetKeyDown(OVERRIDE_KEY))
            {
                SetActive(STICKY_OVERRIDE ? !isActive : true);
            }
            else if (!STICKY_OVERRIDE && Input.GetKeyUp(OVERRIDE_KEY))
            {
                SetActive(false);
            }
        }

        /// <summary>
        /// Gets whether the override key is currently active or not.
        /// </summary>
        public static bool IsActive
        {
            get { return (instance == null) ? false : instance.isActive; }
        }

        private void SetActive(bool newValue)
        {
            Logging.Log(newValue ? "Override activated" : "Override deactivated");
            instance.isActive = newValue;
        }
    }
}
