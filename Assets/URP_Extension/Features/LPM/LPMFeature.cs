using System;
using Features.LPM;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using RenderGraphUtils = UnityEngine.Rendering.RenderGraphModule.Util.RenderGraphUtils;

public class LPMFeature : ScriptableRendererFeature
{
    class LumaPreservingMapperPass : ScriptableRenderPass
    {
        private const string lpmShaderName = "LPM";

        [SerializeField] private Material material;
        private String lastKeyword = "SDR";

        static class ShaderConstants
        {
            public static readonly int _SoftGap = Shader.PropertyToID("_SoftGap");
            public static readonly int _HdrMax = Shader.PropertyToID("_HdrMax");
            public static readonly int _Exposure = Shader.PropertyToID("_Exposure");
            public static readonly int _LPMExposure = Shader.PropertyToID("_LPMExposure");
            public static readonly int _Contrast = Shader.PropertyToID("_Contrast");
            public static readonly int _ShoulderContrast = Shader.PropertyToID("_ShoulderContrast");
            public static readonly int _Saturation = Shader.PropertyToID("_Saturation");
            public static readonly int _Crosstalk = Shader.PropertyToID("_Crosstalk");
            public static readonly int _Intensity = Shader.PropertyToID("_LPMIntensity");
            public static readonly int _DisplayMinMaxLuminance = Shader.PropertyToID("_DisplayMinMaxLuminance");
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

        public LumaPreservingMapperPass()
        {
            profilingSampler = new ProfilingSampler(nameof(LumaPreservingMapperPass));
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var volume = VolumeManager.instance.stack.GetComponent<LPMVolume>();
            if (volume == null || !volume.IsActive())
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            lpmMaterial.SetFloat(ShaderConstants._Intensity, volume.Intensity.value);
            lpmMaterial.SetFloat(ShaderConstants._SoftGap, volume.SoftGap.value);
            lpmMaterial.SetFloat(ShaderConstants._HdrMax, volume.HdrMax.value);
            lpmMaterial.SetFloat(ShaderConstants._LPMExposure, volume.LPMExposure.value);
            lpmMaterial.SetFloat(ShaderConstants._Exposure, MathF.Pow(2.0f, volume.Exposure.value));
            lpmMaterial.SetFloat(ShaderConstants._Contrast, volume.Contrast.value);
            lpmMaterial.SetFloat(ShaderConstants._ShoulderContrast, volume.ShoulderContrast.value);
            lpmMaterial.SetFloat(ShaderConstants._Intensity, volume.Intensity.value);

            lpmMaterial.SetVector(ShaderConstants._Saturation, volume.Saturation.value);
            lpmMaterial.SetVector(ShaderConstants._Crosstalk, volume.Crosstalk.value);
            if (cameraData.isHDROutputActive)
            {
                if (volume.displayMode.value != DisplayMode.SDR)
                {
                    lpmMaterial.SetVector(ShaderConstants._DisplayMinMaxLuminance,
                        new Vector2(cameraData.hdrDisplayInformation.minToneMapLuminance,
                            cameraData.hdrDisplayInformation.maxToneMapLuminance));
                }

            }
            //now only support SDR
            lpmMaterial.DisableKeyword(lastKeyword);
            switch (volume.displayMode.value)
            {
                case DisplayMode.SDR:
                    lpmMaterial.EnableKeyword("SDR");
                    lastKeyword = "SDR";
                    break;
                case DisplayMode.DISPLAYMODE_HDR10_SCRGB: 
                    lpmMaterial.EnableKeyword("DISPLAYMODE_HDR10_SCRGB");
                    lastKeyword = "DISPLAYMODE_HDR10_SCRGB";
                    break;
                case DisplayMode.DISPLAYMODE_HDR10_2084:
                    lpmMaterial.EnableKeyword("DISPLAYMODE_HDR10_2084");
                    lastKeyword = "DISPLAYMODE_HDR10_2084";
                    break;
            }


            TextureHandle source = resourceData.activeColorTexture;
            var destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = $"CameraColor-{nameof(lpmMaterial)}";
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, lpmMaterial,
                0);
            renderGraph.AddBlitPass(para, passName: nameof(LumaPreservingMapperPass));
            resourceData.cameraColor = destination;
        }
    }

    public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    LumaPreservingMapperPass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new LumaPreservingMapperPass();

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = renderPassEvent;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
}