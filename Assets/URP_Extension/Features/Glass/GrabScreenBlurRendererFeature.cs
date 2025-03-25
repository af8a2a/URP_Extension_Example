using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GrabScreenBlurRendererFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Config
    {
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
        var volume = VolumeManager.instance.stack.GetComponent<GrabScreenBlur>();


        renderer.EnqueuePass(grabScreenBlurPass);
    }

    // render pass
    class GrabScreenBlurPass : ScriptableRenderPass
    {
        private Material blurMat;
        private int blurAmount;

        private RTHandle blurHorizonRT;
        private RTHandle blurVerticalRT;
        private RenderTexture _texture;

        public GrabScreenBlurPass(Config config)
        {
            blurMat = config.blurMaterial;
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
            var volume = VolumeManager.instance.stack.GetComponent<GrabScreenBlur>();
            if (volume == null || !volume.IsActive())
            {

                return;
            }


            RenderingUtils.ReAllocateIfNeeded(ref blurHorizonRT, desc, name: "CapturedBlurHorizonRT");
            RenderingUtils.ReAllocateIfNeeded(ref blurVerticalRT, desc, name: "CapturedBlurVerticalRT");

            Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, blurHorizonRT);
            for (int i = 0; i < volume.blurAmount.value; i++)
            {
                Blit(cmd, blurHorizonRT, blurVerticalRT, blurMat);
                Blit(cmd, blurVerticalRT, blurHorizonRT, blurMat, 1);
            }
            cmd.SetGlobalTexture("_BlurTexture", blurHorizonRT);
            blurMat.SetFloat("_BlurIntensity", volume.blurIntensity.value);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnFinishCameraStackRendering(CommandBuffer cmd)
        {
            cmd.SetGlobalTexture("_BlurTexture", Texture2D.whiteTexture);

        }
    }
}

[System.Serializable]
public class GrabScreenBlur : VolumeComponent, IPostProcessComponent
{
    public ClampedIntParameter blurAmount = new ClampedIntParameter(0, 0, 4);
    public ClampedFloatParameter blurIntensity = new ClampedFloatParameter(0f, 0f, 1f);


    public bool IsActive() => active&&blurAmount.value>0&&blurIntensity.value>0;

    public bool IsTileCompatible() => false;
}