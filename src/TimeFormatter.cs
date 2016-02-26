namespace BetterBurnTime
{
    /// <summary>
    /// Utility class for dealing with time formats.
    ///
    /// Thanks to smjjames from the KSP forums for the suggestion to tweak time formats.
    /// </summary>
    class TimeFormatter
    {
        // Time thresholds for using various string formats
        private static readonly int DEFAULT_THRESHOLD_SECONDS = 120;
        private static readonly int DEFAULT_THRESHOLD_MINUTES_SECONDS = 120 * 60;
        private static readonly int DEFAULT_THRESHOLD_HOURS_MINUTES_SECONDS = 6 * 60 * 60;
        private static readonly int DEFAULT_THRESHOLD_HOURS_MINUTES = 4 * DEFAULT_THRESHOLD_HOURS_MINUTES_SECONDS;

        private static readonly string formatSeconds = Configuration.timeFormatSeconds;
        private static readonly string formatMinutesSeconds = Configuration.timeFormatMinutesSeconds;
        private static readonly string formatHoursMinutesSeconds = Configuration.timeFormatHoursMinutesSeconds;
        private static readonly string formatHoursMinutes = Configuration.timeFormatHoursMinutes;
        private static readonly string formatHours = Configuration.timeFormatHours;
        private static readonly string formatWarning = Configuration.timeFormatWarning;

        private readonly int thresholdSeconds;
        private readonly int thresholdMinutesSeconds;
        private readonly int thresholdHoursMinutesSeconds;
        private readonly int thresholdHoursMinutes;

        /// <summary>
        /// The default time formatter to use.
        /// </summary>
        public static readonly TimeFormatter Default = new TimeFormatter(
            DEFAULT_THRESHOLD_SECONDS,
            DEFAULT_THRESHOLD_MINUTES_SECONDS,
            DEFAULT_THRESHOLD_HOURS_MINUTES_SECONDS,
            DEFAULT_THRESHOLD_HOURS_MINUTES);

        public TimeFormatter(
            int thresholdSeconds,
            int thresholdMinutesSeconds,
            int thresholdHoursMinutesSeconds,
            int thresholdHoursMinutes)
        {
            this.thresholdSeconds = thresholdSeconds;
            this.thresholdMinutesSeconds = thresholdMinutesSeconds;
            this.thresholdHoursMinutesSeconds = thresholdHoursMinutesSeconds;
            this.thresholdHoursMinutes = thresholdHoursMinutes;
        }

        /// <summary>
        /// Given a duration in seconds, get a string representation.
        /// </summary>
        /// <param name="totalSeconds"></param>
        /// <returns></returns>
        public string format(int totalSeconds)
        {
            if (totalSeconds < 0)
            {
                return "N/A";
            }
            if (totalSeconds == 0)
            {
                return "< " + string.Format(formatSeconds, 1);
            }
            if (totalSeconds <= thresholdSeconds)
            {
                return string.Format(formatSeconds, totalSeconds);
            }
            else if (totalSeconds <= thresholdMinutesSeconds)
            {
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                return string.Format(formatMinutesSeconds, minutes, seconds);
            }
            else if (totalSeconds <= thresholdHoursMinutesSeconds)
            {
                int hours = totalSeconds / 3600;
                int minutes = (totalSeconds % 3600) / 60;
                int seconds = totalSeconds % 60;
                return string.Format(formatHoursMinutesSeconds, hours, minutes, seconds);
            }
            else if (totalSeconds <= thresholdHoursMinutes)
            {
                int hours = totalSeconds / 3600;
                int minutes = (totalSeconds % 3600) / 60;
                return string.Format(formatHoursMinutes, hours, minutes);
            }
            else
            {
                int hours = thresholdHoursMinutes / 3600;
                return "> " + string.Format(formatHours, hours);
            }
        }

        /// <summary>
        /// Given a duration in seconds, get a string repesentation, formatted as a warning.
        /// </summary>
        /// <param name="totalSeconds"></param>
        /// <returns></returns>
        public string warn(int totalSeconds)
        {
            return string.Format(formatWarning, format(totalSeconds));
        }
    }
}
