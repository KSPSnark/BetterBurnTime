using System;
using System.Text;
using UnityEngine;

namespace BetterBurnTime
{
    static class Logging
    {
        public static void Log(object message)
        {
            Debug.Log("[BetterBurnTime] " + message);
        }

        public static void Warn(object message)
        {
            Debug.LogWarning("[BetterBurnTime] " + message);
        }

        public static void Error(object message)
        {
            Debug.LogError("[BetterBurnTime] " + message);
        }

        public static void Exception(string message, Exception e)
        {
            Error(message + " (" + e.GetType().Name + ") " + e.Message + ": " + e.StackTrace);
        }

        public static void Exception(Exception e)
        {
            Error("(" + e.GetType().Name + ") " + e.Message + ": " + e.StackTrace);
        }

        public static string ToString(Part part)
        {
            return part.partInfo.title;
        }
    }
}
