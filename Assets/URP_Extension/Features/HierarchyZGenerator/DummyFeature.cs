using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace URP_Extension.Features.HierarchyZGenerator
{
    public class DummyFeature : ScriptableRendererFeature
    {
        HierarchyZPass pass;

        public override void Create()
        {
            pass = new HierarchyZPass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents
            };
        }


        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(pass);
        }
    }
}