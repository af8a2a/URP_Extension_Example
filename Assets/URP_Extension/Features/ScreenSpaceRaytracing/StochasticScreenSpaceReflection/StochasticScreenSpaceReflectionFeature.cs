using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using URP_Extension.Features.HierarchyZGenerator;

namespace URP_Extension.Features.ScreenSpaceRaytracing.StochasticScreenSpaceReflection
{
    public class StochasticScreenSpaceReflectionFeature : ScriptableRendererFeature
    {
        private ForwardGBufferPass m_GBufferPass;
        private HierarchyZPass m_HierarchyZPass;
        private StochasticScreenSpaceReflectionIntersectionPass m_intersectionPass;

        // private StochasticScreenSpaceReflectionClassifyTilesPass ClassifyTilesPass;
        private readonly string[] m_GBufferPassNames = new string[] { "UniversalGBuffer" };
        [SerializeField] Cubemap cubemap = null;

        public override void Create()
        {
            m_GBufferPass = new ForwardGBufferPass(m_GBufferPassNames);
            m_HierarchyZPass = new HierarchyZPass();
            m_intersectionPass = new StochasticScreenSpaceReflectionIntersectionPass();
            // ClassifyTilesPass = new StochasticScreenSpaceReflectionClassifyTilesPass();
            m_GBufferPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            m_HierarchyZPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            m_intersectionPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            // ClassifyTilesPass.Setup(cubemap);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_GBufferPass);
            renderer.EnqueuePass(m_HierarchyZPass);
            renderer.EnqueuePass(m_intersectionPass);
        }
    }
}