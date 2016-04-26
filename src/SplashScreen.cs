using System.Collections.Generic;
using UnityEngine;

namespace BetterBurnTime
{
    [KSPAddon(KSPAddon.Startup.Instantly, false)]
    class SplashScreen : MonoBehaviour
    {
        private static readonly string[] NEW_TIPS =
        {
            "Calculating Better Burn Time...",
            "Predicting Time to Impact...",
            "Predicting Time to Closest Approach...",
            "Displaying Countdown Indicator..."
        };

        internal void Awake()
        {
            LoadingScreen.LoadingScreenState state = FindLoadingScreenState();
            if (state != null) InsertTips(state);
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
            string[] newTips = new string[state.tips.Length + NEW_TIPS.Length];
            for (int i = 0; i < state.tips.Length; ++i)
            {
                newTips[i] = state.tips[i];
            }
            for (int i = 0; i < NEW_TIPS.Length; ++i)
            {
                newTips[state.tips.Length + i] = NEW_TIPS[i];
            }
            state.tips = newTips;
        }
    }
}
