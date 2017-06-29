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
            if (part == null) return false;
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
            if ((engine == null) || (engine.thrustTransforms.Count == 0)) return sum;
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
            if (engine == null) return 0.0;
            return engine.minThrust + (engine.maxThrust - engine.minThrust) * engine.thrustPercentage * 0.01;
        }

        /// <summary>
        /// Determines whether the specified vessel is a kerbal on EVA.
        /// </summary>
        public static bool IsEvaKerbal(this Vessel vessel)
        {
            if (vessel == null) return false;
            if (vessel.parts.Count != 1) return false;
            return vessel.parts[0].HasModule<KerbalEVA>();
        }

        /// <summary>
        /// Gets whether the vessel is currently in atmosphere.
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        public static bool IsInAtmosphere(this Vessel vessel)
        {
            return vessel.orbit.referenceBody.atmosphere
                && (vessel.altitude < vessel.orbit.referenceBody.atmosphereDepth);
        }

        /// <summary>
        /// Gets whether the orbit is closed (i.e. not an escape trajectory).
        /// </summary>
        /// <param name="orbit"></param>
        /// <returns></returns>
        public static bool IsClosed(this Orbit orbit)
        {
            return (orbit.eccentricity < 1.0) && (orbit.ApR < orbit.referenceBody.sphereOfInfluence);
        }

        /// <summary>
        /// Gets the radius of the location in the specified orbit at the specified time.
        /// </summary>
        /// <param name="orbit"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public static double GetRadiusAtUT(this Orbit orbit, double time)
        {
            return orbit.getRelativePositionAtUT(time).magnitude;
        }

    }
}
