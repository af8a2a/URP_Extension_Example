using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using URP_Extension.Features.HierarchyZGenerator;

namespace URP_Extension.Features.ScreenSpaceRaytracing.StochasticScreenSpaceReflection
{
    public class StochasticScreenSpaceReflectionIntersectionPass : ScriptableRenderPass
    {
        private ComputeShader computeShader;
        private int kernelID;
        private Texture noiseTexture;
        private Texture2D m_ScramblingTile256SPP;
        private Texture2D m_OwenScrambledRGBATex;

        public StochasticScreenSpaceReflectionIntersectionPass()
        {
            computeShader = Resources.Load<ComputeShader>("Intersect");
            kernelID = computeShader.FindKernel("Intersect");
            noiseTexture = Resources.Load<Texture>("tex_BlueNoise_1024x1024_UNI");
            m_ScramblingTile256SPP = Resources.Load<Texture2D>("ScramblingTile256SPP");
            m_OwenScrambledRGBATex = Resources.Load<Texture2D>("OwenScrambledNoise256");
        }

        internal class PassData
        {
            internal ComputeShader computeShader;
            internal int kernelID;
            internal TextureHandle sceneColor;
            internal TextureHandle sceneDepth;
            internal TextureHandle gbuffer1;
            internal TextureHandle sceneNormalSmoothness;
            internal TextureHandle intersectionOutput;
            internal Texture noiseTexture;
            internal Texture2D m_ScramblingTile256SPP;
            internal Texture2D m_OwenScrambledRGBATex;
            internal Vector4 JitterSizeAndOffset;
            internal int dispatchX;
            internal int dispatchY;
        }

        static void ExecutePass(PassData data, UnsafeGraphContext computeGraphContext)
        {
            var cmd = CommandBufferHelpers.GetNativeCommandBuffer(computeGraphContext.cmd);
            cmd.SetComputeTextureParam(data.computeShader, data.kernelID, "g_lit_scene", data.sceneColor);
            cmd.SetComputeTextureParam(data.computeShader, data.kernelID, "g_depth_buffer_hierarchy", data.sceneDepth);
            cmd.SetComputeTextureParam(data.computeShader, data.kernelID, "g_normalSmoothness",
                data.sceneNormalSmoothness);
            cmd.SetComputeTextureParam(data.computeShader, data.kernelID, "g_intersection_output",
                data.intersectionOutput);
            cmd.SetComputeTextureParam(data.computeShader, data.kernelID, "CameraGBufferTexture1", data.gbuffer1);
            cmd.SetComputeVectorParam(data.computeShader, "JitterSizeAndOffset", data.JitterSizeAndOffset);

            cmd.SetComputeTextureParam(data.computeShader, data.kernelID, "g_blue_noise_texture", data.noiseTexture);
            cmd.SetComputeFloatParam(data.computeShader, "g_depth_buffer_thickness", 0.2f);
            cmd.SetComputeIntParam(data.computeShader, "g_most_detailed_mip", 0);
            cmd.SetComputeIntParam(data.computeShader, "g_max_traversal_intersections", 128);


            cmd.DispatchCompute(data.computeShader, data.kernelID, data.dispatchX, data.dispatchY, 1);
        }

        // From Unity TAA
        private int m_SampleIndex = 0;
        private const int k_SampleCount = 64;

        private float GetHaltonValue(int index, int radix)
        {
            float result = 0f;
            float fraction = 1f / (float)radix;

            while (index > 0)
            {
                result += (float)(index % radix) * fraction;

                index /= radix;
                fraction /= (float)radix;
            }

            return result;
        }


        private Vector2 GenerateRandomOffset()
        {
            var offset = new Vector2(
                GetHaltonValue(m_SampleIndex & 1023, 2),
                GetHaltonValue(m_SampleIndex & 1023, 3));

            if (++m_SampleIndex >= k_SampleCount)
                m_SampleIndex = 0;

            return offset;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            if (!frameData.Contains<HierarchyZData>())
            {
                return;
            }

            
            var hierarchyZData = frameData.Get<HierarchyZData>();
            var gbuffer1 = resourceData.gBuffer[1];
            var normalRoughness = resourceData.gBuffer[2];
            var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            desc.name = "StochasticScreenSpaceReflection_Intersection";
            desc.enableRandomWrite = true;
            desc.format = GraphicsFormat.R16G16B16A16_SFloat;
            var intersectOutput = renderGraph.CreateTexture(desc);
            Vector2 jitterSample = GenerateRandomOffset();

            var JitterSizeAndOffset = new Vector4
            (
                (float)desc.width / (float)noiseTexture.width,
                (float)desc.height / (float)noiseTexture.height,
                jitterSample.x,
                jitterSample.y
            );

            using (var builder = renderGraph.AddUnsafePass<PassData>("SSSR Intersect", out var passData))
            {
                passData.computeShader = computeShader;
                passData.kernelID = kernelID;
                passData.sceneColor = resourceData.activeColorTexture;
                passData.sceneDepth = hierarchyZData.HizTexture;
                passData.sceneNormalSmoothness = normalRoughness;
                passData.intersectionOutput = intersectOutput;
                passData.noiseTexture = noiseTexture;
                passData.dispatchX = desc.width / 8;
                passData.dispatchY = desc.height / 8;
                passData.gbuffer1 = gbuffer1;
                passData.JitterSizeAndOffset = JitterSizeAndOffset;
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.UseTexture(passData.gbuffer1);
                builder.UseTexture(passData.sceneColor);
                builder.UseTexture(passData.sceneDepth);
                builder.UseTexture(passData.sceneNormalSmoothness);
                builder.UseTexture(passData.intersectionOutput, AccessFlags.Write);
                builder.SetRenderFunc((PassData data, UnsafeGraphContext computeGraphContext) =>
                    ExecutePass(data, computeGraphContext));
            }

            // resourceData.cameraColor = intersectOutput;
        }
    }
}