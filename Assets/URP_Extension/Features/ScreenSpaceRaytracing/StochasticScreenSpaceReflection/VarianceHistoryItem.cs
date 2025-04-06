using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace URP_Extension.Features.ScreenSpaceRaytracing.StochasticScreenSpaceReflection
{
    internal class VarianceHistoryItem: ContextItem
    {
        // The texture reference variable.
        public TextureHandle texture = TextureHandle.nullHandle;

        // Reset function required by ContextItem. It should reset all variables not carried
        // over to next frame.
        public override void Reset()
        {
            // We should always reset texture handles since they are only vaild for the current frame.
            texture = TextureHandle.nullHandle;
        }

    }
}