using KSP.IO;
using System;
using System.Collections.Generic;

namespace BetterBurnTime
{
    static class Configuration
    {
        // General program usage
        public static readonly bool useSimpleAcceleration;
        public static readonly string overrideKey;
        public static readonly bool stickyOverride;

        // Impact tracker
        public static readonly bool showImpact;
        public static readonly double impactMaxTimeUntil;

        // Closest approach tracker
        public static readonly bool showClosestApproach;
        public static readonly double closestApproachMaxTimeUntilEncounter;
        public static readonly double closestApproachMaxDistanceKm;
        public static readonly double closestApproachMinTargetDistance;

        // Atmosphere tracker
        public static readonly bool showAtmosphere;
        public static readonly double atmosphereMaxTimeUntilExit;
        public static readonly double atmosphereMaxTimeUntilEntry;

        // Geosync tracker
        public static readonly double geosyncPrecisionLimit;
        public static readonly TimeSpan geosyncIdleTimeout;
        public static readonly string geosyncLabel;
        public static readonly string targetsyncLabel;
        public static readonly double geosyncSecondsTransition;

        // Display string for countdown indicator
        public static readonly string countdownText;
        public static readonly bool isNumericCountdown;
        public static readonly int[] countdownTimes;

        // Time formats
        public static readonly string timeFormatSeconds;
        public static readonly string timeFormatMinutesSeconds;
        public static readonly string timeFormatHoursMinutesSeconds;
        public static readonly string timeFormatHoursMinutes;
        public static readonly string timeFormatHours;
        public static readonly string timeFormatWarning;

        static Configuration()
        {
            PluginConfiguration config = PluginConfiguration.CreateForType<BetterBurnTime>();
            config.load();

            // General program usage
            useSimpleAcceleration = config.GetValue("UseSimpleAcceleration", false);
            // Unity key names (for override key) are documented here:
            // https://docs.unity3d.com/Manual/ConventionalGameInput.html
            overrideKey = config.GetValue("OverrideKey", "right ctrl");
            stickyOverride = config.GetValue("StickyOverride", false);

            // Impact tracker
            showImpact = config.GetValue("ShowImpactTracker", true);
            impactMaxTimeUntil = config.GetValue("MaxTimeToImpact", 120.0);

            // Closest approach
            showClosestApproach                  = config.GetValue("ShowClosestApproachTracker",   true);
            closestApproachMaxTimeUntilEncounter = config.GetValue("MaxTimeUntilEncounter",        900.0); // seconds
            closestApproachMaxDistanceKm         = config.GetValue("MaxClosestApproachDistanceKm", 10.0);
            closestApproachMinTargetDistance     = config.GetValue("MinTargetDistanceMeters",      200.0);

            // Atmosphere transition
            showAtmosphere              = config.GetValue("ShowAtmosphereTransition", true);
            atmosphereMaxTimeUntilExit  = config.GetValue("MaxTimeToAtmosphereExit", 300);
            atmosphereMaxTimeUntilEntry = config.GetValue("MaxTimeToAtmosphereEntry", 900);

            // Geosync tracker
            geosyncPrecisionLimit = config.GetValue("GeosyncPrecisionLimit", 0.05);
            int geosyncIdleTimeoutSeconds = config.GetValue("GeosyncIdleTimeout", 10);
            geosyncIdleTimeout = new TimeSpan(0, 0, geosyncIdleTimeoutSeconds);
            geosyncLabel = config.GetValue("GeosyncLabel", "gsync");
            targetsyncLabel = config.GetValue("TargetsyncLabel", "tsync");
            geosyncSecondsTransition = config.GetValue("GeosyncSecondsTransition", 10.0);


            // N items, separated by whitespace. Note that if countdown text contains "{0}", it's
            // interpreted as a format string for displaying a numeric time.
            // Some options:  ·•▪●■
            //
            // UPDATE (KSP 1.3):  Looks like they broke the fonts. The original countdownText default,
            // "● ● ● • • • • · · · · ·", includes the "●" character, which no longer renders in-game
            // (just shows up as a bunch of hollow square boxes).  So I'm now using "ʘ" instead, which
            // is a bit bigger and gaudier than I would prefer, but at least renders correctly.
            countdownText = config.GetValue("CountdownText", "ʘ ʘ ʘ • • • • · · · · ·").Trim();
            countdownTimes = ParseCountdownTimes(config.GetValue("CountdownTimes", "1, 2, 3, 5, 10, 15"));
            isNumericCountdown = countdownText.Contains("{0}");

            // For details on how format strings work, see:
            // https://msdn.microsoft.com/en-us/library/0c899ak8.aspx
            timeFormatSeconds             = config.GetValue("FormatSeconds",             "{0}s")        .Trim();
            timeFormatMinutesSeconds      = config.GetValue("FormatMinutesSeconds",      "{0}m{1}s")    .Trim();
            timeFormatHoursMinutesSeconds = config.GetValue("FormatHoursMinutesSeconds", "{0}h{1}m{2}s").Trim();
            timeFormatHoursMinutes        = config.GetValue("FormatHoursMinutes",        "{0}h{1}m")    .Trim();
            timeFormatHours               = config.GetValue("FormatHours",               "{0}h")        .Trim();
            timeFormatWarning             = config.GetValue("FormatWarning",             "(~{0})")      .Trim();

            config.save();
        }

        /// <summary>
        /// Given a set of times as comma-delimited text, return a sorted array of coundown times to use.
        /// Will always have at least two elements, and the first element will always be 0.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        private static int[] ParseCountdownTimes(string config)
        {
            HashSet<int> timeSet = new HashSet<int>();
            timeSet.Add(0);
            string[] tokens = config.Split(',');
            foreach (string token in tokens)
            {
                int time;
                int.TryParse(token.Trim(), out time);
                timeSet.Add(time);
            }
            if (timeSet.Count < 2) timeSet.Add(1);
            int[] times = new int[timeSet.Count];
            int index = 0;
            foreach (int time in timeSet)
            {
                times[index++] = time;
            }
            Array.Sort<int>(times);
            return times;
        }
    }
}
