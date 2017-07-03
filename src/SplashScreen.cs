using System.Collections.Generic;
using UnityEngine;

namespace BetterBurnTime
{
    [KSPAddon(KSPAddon.Startup.Instantly, false)]
    class SplashScreen : MonoBehaviour
    {
        private static float MAX_TIP_TIME = 4; // seconds

        private static readonly string[] NEW_TIPS =
        {
            "Calculating Better Burn Time...",
            "Predicting Time to Impact...",
            "Predicting Time to Closest Approach...",
            "Displaying Countdown Indicator..."
        };

        /// <summary>
        /// Snark's sneaky little way of thanking various users of this mod for helpful contributions.
        /// </summary>
        private static readonly string[] THANK_USERS =
        {
            "DMagic",               // bug report (with solution!)
            "FullMetalMachinist",   // for the countdown idea
            "Gen. Jack D. Ripper",  // suggestions around the countdown
            "NathanKell",           // just 'coz he's awesome :-)
            "Rodger",               // first bug report for 1.6.X01
            "sarbian",              // helpful responses to modding questions
            "SirDiazo",             // code example for calculating vessel height
            "smjjames",             // suggestion about tweaking time formats
            "linuxgurugamer"        // pointed out that I'm targeting the wrong .NET version
        };

        internal void Awake()
        {
            LoadingScreen.LoadingScreenState state = FindLoadingScreenState();
            if (state != null)
            {
                InsertTips(state);
                if (state.tipTime > MAX_TIP_TIME) state.tipTime = MAX_TIP_TIME;
            }
        }

        /// <summary>
        /// Finds the loading screen where we want to tinker with the tips,
        /// or null if there's no suitable candidate.
        /// </summary>
        /// <returns></returns>
        private static LoadingScreen.LoadingScreenState FindLoadingScreenState()
        {
            if (LoadingScreen.Instance == null) return null;
            List<LoadingScreen.LoadingScreenState> screens = LoadingScreen.Instance.Screens;
            if (screens == null) return null;
            for (int i = 0; i < screens.Count; ++i)
            {
                LoadingScreen.LoadingScreenState state = screens[i];
                if ((state != null) && (state.tips != null) && (state.tips.Length > 1)) return state;
            }
            return null;
        }

        /// <summary>
        /// Insert our list of tips into the specified loading screen state.
        /// </summary>
        /// <param name="state"></param>
        private static void InsertTips(LoadingScreen.LoadingScreenState state)
        {
            List<string> tipsList = new List<string>();
            tipsList.AddRange(state.tips);
            tipsList.AddRange(NEW_TIPS);
            int numThanks = 1 + (int)Mathf.Sqrt(THANK_USERS.Length);
            System.Random random = new System.Random(System.DateTime.UtcNow.Second);
            for (int i = 0; i < numThanks; ++i)
            {
                tipsList.Add(string.Format("Thanking {0}...", THANK_USERS[random.Next(THANK_USERS.Length)]));
            }
            state.tips = tipsList.ToArray();
        }
    }
}
