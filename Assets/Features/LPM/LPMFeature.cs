using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LPMFeature : ScriptableRendererFeature
{
    class LumaPreservingMapperPass : ScriptableRenderPass
    {
        private const string lpmShaderName = "LPM";

        [SerializeField] private Material material;

        static class ShaderConstants
        {
            public static readonly int _SoftGap = Shader.PropertyToID("_SoftGap");
            public static readonly int _HdrMax = Shader.PropertyToID("_HdrMax");
            public static readonly int _Exposure = Shader.PropertyToID("_Exposure");
            public static readonly int _Contrast = Shader.PropertyToID("_Contrast");
            public static readonly int _ShoulderContrast = Shader.PropertyToID("_ShoulderContrast");
            public static readonly int _Saturation = Shader.PropertyToID("_Saturation");
            public static readonly int _Crosstalk = Shader.PropertyToID("_Crosstalk");
        }

        private Material lpmMaterial
        {
            get
            {
                if (material == null)
                {
                    material = new Material(Shader.Find(lpmShaderName));
                }

                return material;
            }
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("Luma Preserving Mapping");
            lpmMaterial.SetFloat(ShaderConstants._SoftGap, 1.0f / 16.0f);
            lpmMaterial.SetFloat(ShaderConstants._HdrMax, 16.0f);
            lpmMaterial.SetFloat(ShaderConstants._Exposure, 4.0f);
            lpmMaterial.SetFloat(ShaderConstants._Contrast, 0f);
            lpmMaterial.SetFloat(ShaderConstants._ShoulderContrast, 1.0f);

            lpmMaterial.SetVector(ShaderConstants._Saturation, Vector3.one);
            lpmMaterial.SetVector(ShaderConstants._Crosstalk, new Vector3(1.0f, 0.5f, 1.0f / 32.0f));
            Blit(cmd, ref renderingData, lpmMaterial);
            // Blit(cmd, ref renderingData, lpmMaterial);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }
    }

    LumaPreservingMapperPass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new LumaPreservingMapperPass();

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRendering;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
}