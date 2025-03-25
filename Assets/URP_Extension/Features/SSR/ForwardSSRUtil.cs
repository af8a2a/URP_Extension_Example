using UnityEngine;

namespace ForwardSSR
{
    public class ForwardSSRUtil
    {
        public enum Resolution
        {
            [InspectorName("100%")] [Tooltip("Do ray marching at 100% resolution.")]
            Full = 4,

            [InspectorName("75%")] [Tooltip("Do ray marching at 75% resolution.")]
            ThreeQuarters = 3,

            [InspectorName("50%")] [Tooltip("Do ray marching at 50% resolution.")]
            Half = 2,

            [InspectorName("25%")] [Tooltip("Do ray marching at 25% resolution.")]
            Quarter = 1
        }

        public enum MipmapsMode
        {
            [Tooltip("Disable rough reflections in approximation mode.")]
            None = 0,

            [Tooltip("Use trilinear mipmaps to compute rough reflections in approximation mode.")]
            Trilinear = 1
        }
    }
}