using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace URP_Extension.Features.HierarchyZGenerator
{
    public class DummyFeature : ScriptableRendererFeature
    {
        [SerializeField] private ComputeShader cs;
        HierarchyZPass pass;

        public override void Create()
        {
            pass = new HierarchyZPass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques
            };
        }


        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            pass.Setup(cs);
            renderer.EnqueuePass(pass);
        }
    }
}