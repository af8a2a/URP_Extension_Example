using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Features.Diffusion
{
    public class DiffusionFeature : ScriptableRendererFeature
    {
        private DiffusionPass _diffusionPass;
        [SerializeField] private Material DiffusionMaterial;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
       [Range(0f,1f)] public float Intensity;

        public override void Create()
        {
            _diffusionPass = new DiffusionPass(DiffusionMaterial,Intensity);
            _diffusionPass.renderPassEvent = renderPassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_diffusionPass);
        }
    }

    public class DiffusionPass : ScriptableRenderPass
    {
        private Material DiffusionMaterial;

        private RTHandle blurHorizonRT;
        private RTHandle blurVerticalRT;
        private RTHandle sourceRT;
        private float intensity;
        private static int SourceTexture = Shader.PropertyToID("_SourceTexture");
        private static int Intensity = Shader.PropertyToID("_Intensity");
        public DiffusionPass(Material material, float intensity)
        {
            DiffusionMaterial = material;
            this.intensity = intensity;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            
            var camera = renderingData.cameraData.camera;
            if (camera.cameraType == CameraType.Preview)
            {
                return;
            }
            var cmd = CommandBufferPool.Get("Diffusion");
            var desc = new RenderTextureDescriptor(renderingData.cameraData.cameraTargetDescriptor.width,
                renderingData.cameraData.cameraTargetDescriptor.height, RenderTextureFormat.RGB111110Float);


            RenderingUtils.ReAllocateIfNeeded(ref blurHorizonRT, desc, name: "BlurHorizonRT");
            RenderingUtils.ReAllocateIfNeeded(ref blurVerticalRT, desc, name: "BlurVerticalRT");
            RenderingUtils.ReAllocateIfNeeded(ref sourceRT, desc, name: "_SourceTexture");


            Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, sourceRT);
            DiffusionMaterial.SetTexture(SourceTexture, sourceRT);
            DiffusionMaterial.SetFloat(Intensity, intensity);
            // Blitter.BlitTexture(cmd, sourceRT, blurHorizonRT, DiffusionMaterial, 0);
            Blit(cmd, sourceRT, blurHorizonRT, DiffusionMaterial, 0);
            Blit(cmd, blurHorizonRT, blurVerticalRT, DiffusionMaterial, 1);
            Blit(cmd, blurVerticalRT, renderingData.cameraData.renderer.cameraColorTargetHandle, DiffusionMaterial, 2);

            // cmd.Blit(blurVerticalRT, renderingData.cameraData.renderer.cameraColorTargetHandle,
            //     DiffusionMaterial, 2);
            // Blitter.BlitTexture(cmd, blurVerticalRT, renderingData.cameraData.renderer.cameraColorTargetHandle,
            //     DiffusionMaterial, 2);

            context.ExecuteCommandBuffer(cmd);


            CommandBufferPool.Release(cmd);
        }
    }
}