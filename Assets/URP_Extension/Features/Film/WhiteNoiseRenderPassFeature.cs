using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class WhiteNoiseRenderPassFeature : ScriptableRendererFeature
{
    private const string ShaderName = "WhiteNoise";

    
    class CustomRenderPass : ScriptableRenderPass
    {
        [SerializeField] private Material whiteNoiseMaterial;

        public Material WhiteNoiseMaterial
        {
            get
            {
                if (whiteNoiseMaterial == null)
                {
                    whiteNoiseMaterial = new Material(Shader.Find(ShaderName));
                }

                return whiteNoiseMaterial;
            }
        }

        static class ShaderConstants
        {
            public static readonly int _Intensity = Shader.PropertyToID("_Intensity");
            
        }



        
        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var volume = VolumeManager.instance.stack.GetComponent<WhiteNoiseVolume>();
            if (volume != null && volume.IsActive())
            {
                var cmd = CommandBufferPool.Get(ShaderName);
                WhiteNoiseMaterial.SetFloat(ShaderConstants._Intensity,volume.Intensity.value);
                Blit(cmd, ref renderingData, WhiteNoiseMaterial);

                context.ExecuteCommandBuffer(cmd);
                cmd.Release();
            }

        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
    }

    CustomRenderPass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass();

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
}