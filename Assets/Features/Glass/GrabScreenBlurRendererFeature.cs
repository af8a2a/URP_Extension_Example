using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GrabScreenBlurRendererFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Config
    {
        public float blurAmount;
        public Material blurMaterial;
    }

    [SerializeField] private Config config;

    private GrabScreenBlurPass grabScreenBlurPass;

    public override void Create()
    {
        grabScreenBlurPass = new GrabScreenBlurPass(config);
        grabScreenBlurPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
    }


    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(grabScreenBlurPass);
    }

    // render pass
    class GrabScreenBlurPass : ScriptableRenderPass
    {
        private Material blurMat;
        private float blurAmount;

        private RTHandle blurHorizonRT;
        private RTHandle blurVerticalRT;
        private RenderTexture _texture;
        private int[] sizes = { 1, 2, 4, 8 };

        public GrabScreenBlurPass(Config config)
        {
            blurMat = config.blurMaterial;
            blurAmount = config.blurAmount;

            profilingSampler = new ProfilingSampler(nameof(GrabScreenBlurPass));
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            if (camera.cameraType == CameraType.Preview)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("GrabScreenBlur");

            var desc = new RenderTextureDescriptor(renderingData.cameraData.cameraTargetDescriptor.width,
                renderingData.cameraData.cameraTargetDescriptor.height, RenderTextureFormat.RGB111110Float);


            RenderingUtils.ReAllocateIfNeeded(ref blurHorizonRT, desc, name: "CapturedBlurHorizonRT");
            RenderingUtils.ReAllocateIfNeeded(ref blurVerticalRT, desc, name: "CapturedBlurVerticalRT");

            Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, blurHorizonRT,
                blurMat, 0);
            for (int i = 0; i < 4; i++)
            {
                Blit(cmd, blurHorizonRT, blurVerticalRT, blurMat, i % 2);
                (blurVerticalRT, blurHorizonRT) = (blurHorizonRT, blurVerticalRT);
            }

            cmd.SetGlobalTexture("_BlurTexture", blurVerticalRT);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        //schedule command buffer
    }
}