using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ForwardSSR
{
    public class ForwardSSRPass : ScriptableRenderPass
    {

        private const string ssrShaderName = "Screen Space Raytracing";
        public ScreenSpaceReflection ssrVolume;
        private static readonly int minSmoothness = Shader.PropertyToID("_MinSmoothness");
        private static readonly int fadeSmoothness = Shader.PropertyToID("_FadeSmoothness");
        private static readonly int edgeFade = Shader.PropertyToID("_EdgeFade");
        private static readonly int thickness = Shader.PropertyToID("_Thickness");
        private static readonly int stepSize = Shader.PropertyToID("_StepSize");
        private static readonly int stepSizeMultiplier = Shader.PropertyToID("_StepSizeMultiplier");
        private static readonly int maxStep = Shader.PropertyToID("_MaxStep");
        private static readonly int downSample = Shader.PropertyToID("_DownSample");
        private static readonly int accumFactor = Shader.PropertyToID("_AccumulationFactor");

        [SerializeField] private Material material;
        private Material ssrMaterial
        {
            get
            {
                if (material == null)
                {
                    material = new Material(Shader.Find(ssrShaderName));
                }

                return material;
            }
        }

        private RTHandle sourceHandle;
        private RTHandle reflectHandle;
        private RTHandle historyHandle;
        public ForwardSSRUtil.Resolution resolution;
        public ForwardSSRUtil.MipmapsMode mipmapsMode;

        public ForwardSSRPass(ForwardSSRUtil.Resolution resolution, ForwardSSRUtil.MipmapsMode mipmapsMode)
        {
            this.resolution = resolution;
            this.mipmapsMode = mipmapsMode;
            ssrVolume = VolumeManager.instance.stack.GetComponent<ScreenSpaceReflection>();
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;

            if (camera.cameraType == CameraType.Preview)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("SSR");

            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.useMipMap = false;


            RenderingUtils.ReAllocateIfNeeded(ref sourceHandle, desc, FilterMode.Point, TextureWrapMode.Clamp,
                name: "_ScreenSpaceReflectionSourceTexture");

            desc.width = (int)resolution * (int)(desc.width * 0.25f);
            desc.height = (int)resolution * (int)(desc.height * 0.25f);
            desc.useMipMap = (mipmapsMode == ForwardSSRUtil.MipmapsMode.Trilinear);
            //desc.colorFormat = RenderTextureFormat.ARGBHalf; // needs alpha channel to store hit mask.
            FilterMode filterMode = (mipmapsMode == ForwardSSRUtil.MipmapsMode.Trilinear)
                ? FilterMode.Trilinear
                : FilterMode.Point;

            RenderingUtils.ReAllocateIfNeeded(ref reflectHandle, desc, filterMode, TextureWrapMode.Clamp,
                name: "_ScreenSpaceReflectionColorTexture");

            using (new ProfilingScope(cmd, new ProfilingSampler("Screen Space Reflection")))
            {
                // Set the parameters here to avoid using 4 shader keywords.
                if (ssrVolume.quality.value == ScreenSpaceReflection.Quality.Low)
                {
                    ssrMaterial.SetFloat(stepSize, 0.4f);
                    ssrMaterial.SetFloat(stepSizeMultiplier, 1.33f);
                    ssrMaterial.SetFloat(maxStep, 16);
                }
                else if (ssrVolume.quality.value == ScreenSpaceReflection.Quality.Medium)
                {
                    ssrMaterial.SetFloat(stepSize, 0.3f);
                    ssrMaterial.SetFloat(stepSizeMultiplier, 1.33f);
                    ssrMaterial.SetFloat(maxStep, 32);
                }
                else if (ssrVolume.quality.value == ScreenSpaceReflection.Quality.High)
                {
                    ssrMaterial.SetFloat(stepSize, 0.2f);
                    ssrMaterial.SetFloat(stepSizeMultiplier, 1.33f);
                    ssrMaterial.SetFloat(maxStep, 64);
                }
                else
                {
                    ssrMaterial.SetFloat(stepSize, 0.2f);
                    ssrMaterial.SetFloat(stepSizeMultiplier, 1.1f);
                    ssrMaterial.SetFloat(maxStep, ssrVolume.maxStep.value);
                }

                ssrMaterial.SetFloat(minSmoothness, ssrVolume.minSmoothness.value);
                ssrMaterial.SetFloat(fadeSmoothness,
                    ssrVolume.fadeSmoothness.value <= ssrVolume.minSmoothness.value
                        ? ssrVolume.minSmoothness.value + 0.01f
                        : ssrVolume.fadeSmoothness.value);
                ssrMaterial.SetFloat(edgeFade, ssrVolume.edgeFade.value);
                ssrMaterial.SetFloat(thickness, ssrVolume.thickness.value);
                ssrMaterial.SetFloat(downSample, (float)resolution * 0.25f);

                // Blit() may not handle XR rendering correctly.


                if (mipmapsMode == ForwardSSRUtil.MipmapsMode.Trilinear)
                    ssrMaterial.EnableKeyword("_SSR_APPROX_COLOR_MIPMAPS");
                else
                    ssrMaterial.DisableKeyword("_SSR_APPROX_COLOR_MIPMAPS");

                // Copy Scene Color
                Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, sourceHandle);
                // Screen Space Reflection
                Blitter.BlitCameraTexture(cmd, sourceHandle, reflectHandle, ssrMaterial,0);
                // Combine Color
                Blitter.BlitCameraTexture(cmd, reflectHandle, renderingData.cameraData.renderer.cameraColorTargetHandle, ssrMaterial, 1);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }
}