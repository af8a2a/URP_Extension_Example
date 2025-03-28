using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using RenderGraphUtils = UnityEngine.Rendering.RenderGraphModule.Util.RenderGraphUtils;

namespace Features.Diffusion
{
    public class DiffusionFeature : ScriptableRendererFeature
    {
        private DiffusionPass _diffusionPass;
        [SerializeField] private Material DiffusionMaterial;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        [Range(0f, 1f)] public float Intensity;
        [Range(0f, 1f)] public float BlurIntensity;

        public override void Create()
        {
            _diffusionPass = new DiffusionPass(DiffusionMaterial, Intensity, BlurIntensity);
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

        private float intensity;
        private float blurIntensity;
        private static int SourceTexture = Shader.PropertyToID("_SourceTexture");
        private static int IntensityID = Shader.PropertyToID("_Intensity");
        private static int BlurIntensityID = Shader.PropertyToID("_BlurIntensity");

        public DiffusionPass(Material material, float intensity, float blurIntensity)
        {
            profilingSampler = new ProfilingSampler("Diffusion");

            DiffusionMaterial = material;
            this.intensity = intensity;
            this.blurIntensity = blurIntensity;
        }

        class PassData
        {
            internal Material material;
            internal float intensity;
            internal float blurIntensity;

            internal TextureHandle cameraColor;

            internal TextureHandle sourceTexture;
            internal TextureHandle blurHTexture;
            internal TextureHandle blurVTexture;
            internal TextureHandle targetTexture;
        }

        private void InitDiffusionPassData(ref PassData data)
        {
            data.material = DiffusionMaterial;
            data.intensity = intensity;
        }

        private void CreateRenderTextureHandles(RenderGraph renderGraph, UniversalCameraData cameraData,
            out TextureHandle sourceTexture,
            out TextureHandle blurHTexture,
            out TextureHandle blurVTexture)
        {
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.colorFormat = RenderTextureFormat.RGB111110Float;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.msaaSamples = 1;

            sourceTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc,
                "Diffusion_Source", false, FilterMode.Bilinear);
            blurHTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc,
                "Diffusion_BlurH", false, FilterMode.Bilinear);
            blurVTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc,
                "Diffusion_BlurV", false, FilterMode.Bilinear);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            CreateRenderTextureHandles(renderGraph,
                cameraData,
                out var sourceTexture,
                out var blurTextureH,
                out var blurTextureV);

            using (var builder =
                   renderGraph.AddUnsafePass<PassData>("Diffusion", out var passData, profilingSampler))
            {
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                InitDiffusionPassData(ref passData);
                passData.sourceTexture = sourceTexture;
                passData.blurHTexture = blurTextureH;
                passData.blurVTexture = blurTextureV;
                passData.material = DiffusionMaterial;
                passData.cameraColor = resourceData.cameraColor;
                passData.blurIntensity = blurIntensity;
                passData.intensity = intensity;
                passData.targetTexture = resourceData.cameraColor;

                builder.UseTexture(passData.sourceTexture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.blurHTexture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.blurVTexture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.targetTexture, AccessFlags.ReadWrite);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext rgContext) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(rgContext.cmd);

                    Blitter.BlitCameraTexture(cmd, passData.cameraColor, data.sourceTexture);
                    data.material.SetTexture(SourceTexture, data.sourceTexture);
                    data.material.SetFloat(BlurIntensityID, data.blurIntensity);
                    data.material.SetFloat(IntensityID, data.intensity);

                    Blitter.BlitCameraTexture(cmd, data.sourceTexture, data.blurHTexture, data.material, 0);
                    Blitter.BlitCameraTexture(cmd, data.blurHTexture, data.blurVTexture, data.material, 1);
                    Blitter.BlitCameraTexture(cmd, data.blurVTexture, data.targetTexture, data.material,
                        2);
                });
            }
        }
    }
}