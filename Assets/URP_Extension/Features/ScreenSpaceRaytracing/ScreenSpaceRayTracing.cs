using UnityEngine.Rendering.Universal;

namespace URP_Extension.Features.ScreenSpaceRaytracing
{
    public class ScreenSpaceRayTracing: ScriptableRendererFeature
    {
        ForwardGBufferPass m_GBufferPass;
        private readonly string[] m_GBufferPassNames = new string[] { "UniversalGBuffer" };

        public override void Create()
        {
            m_GBufferPass = new ForwardGBufferPass(m_GBufferPassNames);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_GBufferPass);
        }
    }
}