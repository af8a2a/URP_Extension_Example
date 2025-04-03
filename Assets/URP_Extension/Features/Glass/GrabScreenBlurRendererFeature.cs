using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using RenderGraphUtils = UnityEngine.Rendering.RenderGraphModule.Util.RenderGraphUtils;

public class GrabScreenBlurRendererFeature : ScriptableRendererFeature
{
    private GrabScreenBlurPass grabScreenBlurPass;

    public override void Create()
    {
        grabScreenBlurPass = new GrabScreenBlurPass();
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
        private int blurAmount;

        private Material BlurMat
        {
            get
            {
                if (blurMat == null)
                {
                    blurMat = CoreUtils.CreateEngineMaterial("FullScreenBlur");
                }

                return blurMat;
            }
        }

        private RenderTexture _texture;

        public GrabScreenBlurPass()
        {
            
            blurMat = new Material(Shader.Find("FullScreenBlur"));
            profilingSampler = new ProfilingSampler(nameof(GrabScreenBlurPass));
        }

        private class PassData
        {
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var volume = VolumeManager.instance.stack.GetComponent<GrabScreenBlur>();

            if (volume == null || !volume.IsActive())
            {
                return;
            }

            var source = resourceData.activeColorTexture;
            var destinationDesc = renderGraph.GetTextureDesc(source);
            // destinationDesc.name = $"CameraColor-{passName}";
            // destinationDesc.clearBuffer = false;

            TextureHandle blurHorizonRT = renderGraph.CreateTexture(destinationDesc);
            TextureHandle blurVerticalRT = renderGraph.CreateTexture(destinationDesc);
            blurMat.SetFloat("_BlurIntensity", volume.blurIntensity.value);

            // var blurHorizonRT = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "blurHorizonRT", false);
            // var blurVerticalRT = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "BlurVerticalRT", false);

            renderGraph.AddCopyPass(resourceData.activeColorTexture, blurHorizonRT);
            RenderGraphUtils.BlitMaterialParameters blitH =
                new RenderGraphUtils.BlitMaterialParameters(blurHorizonRT, blurVerticalRT, BlurMat, 0);
            RenderGraphUtils.BlitMaterialParameters blitV =
                new RenderGraphUtils.BlitMaterialParameters(blurVerticalRT, blurHorizonRT, BlurMat, 1);
            for (int i = 0; i < volume.blurAmount.value; i++)
            {
                renderGraph.AddBlitPass(blitH);
                renderGraph.AddBlitPass(blitV);
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("GrabBlur", out var passData))
            {
                
                builder.AllowPassCulling(false);
                builder.SetGlobalTextureAfterPass(blurHorizonRT,
                    Shader.PropertyToID("_BlurTexture"));

                builder.SetRenderFunc((PassData data, RasterGraphContext context) => { });
            }
        }
    }
}

[System.Serializable]
public class GrabScreenBlur : VolumeComponent, IPostProcessComponent
{
    public ClampedIntParameter blurAmount = new ClampedIntParameter(0, 0, 4);
    public ClampedFloatParameter blurIntensity = new ClampedFloatParameter(0f, 0f, 1f);
    public BoolParameter enabled = new BoolParameter(false);

    public bool IsActive() => enabled.value;

    public bool IsTileCompatible() => false;
}