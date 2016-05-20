using System.Collections.Generic;

namespace BetterBurnTime
{
    /// <summary>
    /// Serves up strings to use as a countdown indicator.
    /// </summary>
    static class Countdown
    {
        // Thanks to KSP forum members for helpful suggestions, including:
        //
        // FullMetalMachinist, for the idea of showing a row of dots for countdown, with logarithmic falloff
        // Gen. Jack D. Ripper, for requesting a countdown in the first place, and feedback on visual appearance

        // Threshold levels in seconds for displaying countdown levels
        private static int MAX_LEVELS = 25;
        private static readonly int[] COUNTDOWN_TIMES = Configuration.countdownTimes; // exponentially doubles after that
        private static readonly string[] items;

        static Countdown()
        {
            string text = Configuration.countdownText;
            List<string> itemsList = new List<string>();
            int pos = 0;
            while (pos < text.Length)
            {
                int lastPos = pos;
                while ((pos < text.Length) && !char.IsWhiteSpace(text[pos])) ++pos;
                if (pos > lastPos) itemsList.Add(text.Substring(0, pos));
                if (pos >= text.Length) break;
                if (itemsList.Count >= MAX_LEVELS) break;
                while ((pos < text.Length) && char.IsWhiteSpace(text[pos])) ++pos;
            }
            items = itemsList.ToArray();
        }

        /// <summary>
        /// Gets whether countdown is enabled.
        /// </summary>
        public static bool IsEnabled { get { return items.Length > 0; } }

        /// <summary>
        /// Gets a countdown string to display for a given number of seconds.
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public static string ForSeconds(int seconds)
        {
            if (Configuration.isNumericCountdown) return NumericCountdown(seconds);
            int time;
            for (int level = 0; level < COUNTDOWN_TIMES.Length; ++level)
            {
                time = COUNTDOWN_TIMES[level];
                if (time >= seconds) return AtIndex(level);
            }
            time = COUNTDOWN_TIMES[COUNTDOWN_TIMES.Length - 1];
            for (int level = COUNTDOWN_TIMES.Length; level < MAX_LEVELS; ++level)
            {
                time *= 2;
                if (time >= seconds) return AtIndex(level);
            }
            return AtIndex(MAX_LEVELS);
        }

        /// <summary>
        /// Gets a string representing a countdown value for the given index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private static string AtIndex(int index)
        {
            if (index <= 0) return string.Empty;
            if (items.Length == 0) return string.Empty;
            if (index >= items.Length) return items[items.Length - 1];
            return items[index - 1];
        }

        private static string NumericCountdown(int seconds)
        {
            return (seconds > 0)
                ? string.Format(Configuration.countdownText, TimeFormatter.Default.format(seconds))
                : string.Empty;
        }
    }
}
