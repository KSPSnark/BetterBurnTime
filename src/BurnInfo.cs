using System;
using UnityEngine;
using UnityEngine.UI;
using BurnVector = KSP.UI.Screens.Flight.NavBallBurnVector;

namespace BetterBurnTime
{
    /// <summary>
    /// Provides access to the display of "estimated burn time" and "time remaining until burn".
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class BurnInfo : MonoBehaviour
    {
        private static readonly TimeSpan UPDATE_INTERVAL = new TimeSpan(0, 0, 0, 0, 250);

        // the global instance of the object
        private static BurnInfo instance = null;

        // for tracking whether we're initialized
        private bool isInitialized = false;
        private DateTime lastUpdate = DateTime.MinValue;

        // things that get set when we're initialized
        private BurnVector burnVector = null;
        private Text originalDurationText = null;
        private Text originalTimeUntilText = null;
        private Text alternateDurationText = null;
        private Text alternateTimeUntilText = null;
        private Text countdownText = null;

        /// <summary>
        /// Here when the add-on loads upon flight start.
        /// </summary>
        public void Start()
        {
            instance = this;
            isInitialized = false;
            AttemptInitialize();
        }

        /// <summary>
        /// Sets the text displayed for burn duration.
        /// </summary>
        public static string Duration
        {
            set
            {
                if (instance == null) return;
                if (!instance.AttemptInitialize()) return;
                if (instance.originalDurationText.enabled)
                {
                    instance.originalDurationText.text = value;
                }
                else
                {
                    instance.alternateDurationText.text = value;
                }
            }
        }

        /// <summary>
        /// Sets the text displayed for time until burn.
        /// </summary>
        public static string TimeUntil
        {
            set
            {
                if (instance == null) return;
                if (!instance.AttemptInitialize()) return;
                if (instance.originalTimeUntilText.enabled)
                {
                    instance.originalTimeUntilText.text = value;
                }
                else
                {
                    instance.alternateTimeUntilText.text = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the countdown level of the indicator (the number of dots displayed).
        /// </summary>
        public static string Countdown
        {
            set
            {
                if (instance == null) return;
                if (!instance.AttemptInitialize()) return;
                instance.countdownText.text = value;
                instance.countdownText.enabled = value.Length > 0;
            }
        }

        /// <summary>
        /// Gets whether burn information is initialized and usable.
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                return (instance != null) && instance.isInitialized;
            }
        }

        /// <summary>
        /// Gets the remaining dV, in meters per second. Returns NaN if not applicable.
        /// </summary>
        public static double DvRemaining
        {
            get
            {
                if (instance == null) return double.NaN;
                if (!instance.AttemptInitialize()) return double.NaN;
                return instance.burnVector.dVremaining;
            }
        }

        /// <summary>
        /// Gets whether the original display text is enabled. (This is get-only, since it's determined
        /// by whether the game wants to display the original maneuver DV display.)
        /// </summary>
        public static bool OriginalDisplayEnabled
        {
            get
            {
                if (instance == null) return false;
                if (!instance.AttemptInitialize()) return false;
                return instance.originalDurationText.enabled && instance.originalTimeUntilText.enabled;
            }
        }

        /// <summary>
        /// Gets or sets whether the alternate display text is enabled.
        /// </summary>
        public static bool AlternateDisplayEnabled
        {
            get
            {
                if (instance == null) return false;
                if (!instance.AttemptInitialize()) return false;
                return instance.alternateDurationText.enabled && instance.alternateTimeUntilText.enabled;
            }
            set
            {
                if (instance == null) return;
                if (!instance.AttemptInitialize()) return;
                instance.alternateDurationText.enabled = instance.alternateTimeUntilText.enabled = value;
            }
        }

        /// <summary>
        /// Try to initialize the needed components. Returns true if initialized, false if not.
        /// If this function returns true, you're guaranteed that burn vector and the needed GUI
        /// text objects are available and non-null.
        /// </summary>
        /// <returns></returns>
        private bool AttemptInitialize()
        {
            if (isInitialized) return true; // already initialized
            DateTime now = DateTime.Now;
            if (lastUpdate + UPDATE_INTERVAL > now) return false; // too soon to try again
            lastUpdate = now;

            // Try to get the navball's burn vector.  This check is needed because it turns
            // out that the timing of when this object becomes available isn't super reliable,
            // so various MonoBehaviour implementations in the mod can't just initialize at
            // Start() time and use it.
            BurnVector theBurnVector = GameObject.FindObjectOfType<BurnVector>();
            if (theBurnVector == null) return false; // nope, couldn't get it yet!

            // Make sure the burn vector components that we need are there
            if (theBurnVector.ebtText == null) return false;
            if (theBurnVector.TdnText == null) return false;

            Text theClonedDurationText = CloneBehaviour(theBurnVector.ebtText);
            if (theClonedDurationText == null) return false;

            Text theClonedTimeUntilText = CloneBehaviour(theBurnVector.TdnText);
            if (theClonedTimeUntilText == null) return false;

            Text theCountdownText = CloneBehaviour(theBurnVector.ebtText);
            if (theCountdownText == null) return false;
            theCountdownText.enabled = false;
            theCountdownText.transform.position = Interpolate(
                theBurnVector.TdnText.transform.position,
                theBurnVector.ebtText.transform.position,
                2.0F);

            // Got everything we need!
            burnVector = theBurnVector;
            originalDurationText = burnVector.ebtText;
            originalTimeUntilText = burnVector.TdnText;
            alternateDurationText = theClonedDurationText;
            alternateTimeUntilText = theClonedTimeUntilText;
            countdownText = theCountdownText;
            isInitialized = true;

            return true;
        }

        /// <summary>
        /// Clones a behaviour.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="initialText"></param>
        /// <returns></returns>
        private static T CloneBehaviour<T>(T source) where T : Behaviour
        {
            GameObject clonedObject = UnityEngine.Object.Instantiate(
                source.gameObject,
                source.transform.position,
                source.transform.rotation) as GameObject;
            clonedObject.transform.parent = source.gameObject.transform.parent;
            T clonedBehaviour = clonedObject.GetComponent<T>();

            clonedBehaviour.enabled = false;
            return clonedBehaviour;
        }

        /// <summary>
        /// Provide a vector in between two others.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount">In the range 0 to 1.  0 = function returns "from", 1 = function returns "to", 0.5 = function returns midpoint</param>
        /// <returns></returns>
        private static Vector3 Interpolate(Vector3 from, Vector3 to, float amount)
        {
            float remainder = 1.0F - amount;
            float x = (from.x * amount) + (to.x * remainder);
            float y = (from.y * amount) + (to.y * remainder);
            float z = (from.z * amount) + (to.z * remainder);
            return new Vector3(x, y, z);
        }
    }
}
