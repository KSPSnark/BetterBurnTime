using KSP.IO;

namespace BetterBurnTime
{
    static class Configuration
    {
        // General program usage
        public static readonly bool useSimpleAcceleration;

        // Impact tracker
        public static readonly bool showImpact;
        public static readonly double impactMaxTimeUntil;

        // Closest approach tracker
        public static readonly bool showClosestApproach;
        public static readonly double closestApproachMaxTimeUntilEncounter;
        public static readonly double closestApproachMaxDistanceKm;
        public static readonly double closestApproachMinTargetDistance;

        // Display string for countdown indicator
        public static readonly string countdownText;

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

            // Impact tracker
            showImpact = config.GetValue("ShowImpactTracker", true);
            impactMaxTimeUntil = config.GetValue("MaxTimeToImpact", 120.0);

            // Closest approach
            showClosestApproach                  = config.GetValue("ShowClosestApproachTracker",   true);
            closestApproachMaxTimeUntilEncounter = config.GetValue("MaxTimeUntilEncounter",        900.0); // seconds
            closestApproachMaxDistanceKm         = config.GetValue("MaxClosestApproachDistanceKm", 10.0);
            closestApproachMinTargetDistance     = config.GetValue("MinTargetDistanceMeters",      200.0);


            // N items, separated by whitespace
            // Some options:  ·•▪●■
            countdownText = config.GetValue("CountdownText", "● ● ● • • • • · · · · ·").Trim();

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
    }
}
