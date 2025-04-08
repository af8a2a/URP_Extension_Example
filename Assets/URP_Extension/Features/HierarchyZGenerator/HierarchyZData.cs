using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace URP_Extension.Features.HierarchyZGenerator
{
    public class HierarchyZData : ContextItem
    {
        public TextureHandle HizTexture = TextureHandle.nullHandle;

        public override void Reset()
        {
            HizTexture = TextureHandle.nullHandle;
        }
    }
}