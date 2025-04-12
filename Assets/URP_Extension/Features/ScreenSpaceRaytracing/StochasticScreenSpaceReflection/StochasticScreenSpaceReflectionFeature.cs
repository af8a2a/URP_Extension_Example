using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using URP_Extension.Features.HierarchyZGenerator;

namespace URP_Extension.Features.ScreenSpaceRaytracing.StochasticScreenSpaceReflection
{
    public class StochasticScreenSpaceReflectionFeature : ScriptableRendererFeature
    {
        private ForwardGBufferPass m_GBufferPass;
        private HierarchyZPass m_HierarchyZPass;
        private StochasticScreenSpaceReflectionPass _mPass;

        // private StochasticScreenSpaceReflectionClassifyTilesPass ClassifyTilesPass;
        private readonly string[] m_GBufferPassNames = new string[] { "UniversalGBuffer" };
        [SerializeField] Cubemap cubemap = null;

        public override void Create()
        {
            m_GBufferPass = new ForwardGBufferPass(m_GBufferPassNames);
            m_HierarchyZPass = new HierarchyZPass();
            _mPass = new StochasticScreenSpaceReflectionPass(RenderPassEvent.BeforeRenderingPostProcessing);
            // ClassifyTilesPass = new StochasticScreenSpaceReflectionClassifyTilesPass();
            m_GBufferPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            m_HierarchyZPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }


        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var sssr = VolumeManager.instance.stack.GetComponent<StochasticScreenSpaceReflection>();
            _mPass.Setup(sssr);

            renderer.EnqueuePass(m_GBufferPass);
            renderer.EnqueuePass(m_HierarchyZPass);
            renderer.EnqueuePass(_mPass);
        }
    }
}