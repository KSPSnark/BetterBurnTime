using UnityEngine;

namespace BetterBurnTime
{
    /// <summary>
    /// Various utility extension methods.
    /// </summary>
    static class Extensions
    {
        /// <summary>
        /// Returns true if the part has a module of the specified class (or any
        /// subclass thereof).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="part"></param>
        /// <returns></returns>
        public static bool HasModule<T> (this Part part) where T : PartModule
        {
            for (int index = 0; index < part.Modules.Count; ++index)
            {
                if (part.Modules[index] is T) return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a unit vector pointing in the engine's "forward" direction
        /// (opposite its thrust direction).
        /// </summary>
        /// <param name="engine"></param>
        /// <returns></returns>
        public static Vector3 Forward(this ModuleEngines engine)
        {
            Vector3 sum = Vector3.zero;
            if (engine.thrustTransforms.Count == 0) return sum;
            for (int index = 0; index < engine.thrustTransforms.Count; ++index)
            {
                sum += engine.thrustTransforms[index].forward;
            }
            return sum.normalized;
        }

        /// <summary>
        /// Gets the current max thrust of the engine in kilonewtons, taking thrust limiter into account.
        /// </summary>
        /// <param name="engine"></param>
        /// <returns></returns>
        public static double ThrustLimit(this ModuleEngines engine)
        {
            return engine.minThrust + (engine.maxThrust - engine.minThrust) * engine.thrustPercentage * 0.01;
        }
    }
}
