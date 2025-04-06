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

        public StochasticScreenSpaceReflectionIntersectionPass()
        {
            computeShader = Resources.Load<ComputeShader>("Intersect");
            kernelID = computeShader.FindKernel("Intersect");
            noiseTexture = Resources.Load<Texture>("tex_BlueNoise_1024x1024_UNI");
        }

        internal class PassData
        {
            internal ComputeShader computeShader;
            internal int kernelID;
            internal TextureHandle sceneColor;
            internal TextureHandle sceneDepth;
            internal TextureHandle sceneNormalSmoothness;
            internal TextureHandle intersectionOutput;
            internal Texture noiseTexture;
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
            cmd.SetComputeTextureParam(data.computeShader, data.kernelID, "g_blue_noise_texture", data.noiseTexture);
            cmd.SetComputeFloatParam(data.computeShader, "g_depth_buffer_thickness", 0.2f);
            cmd.SetComputeIntParam(data.computeShader, "g_most_detailed_mip", 0);
            cmd.SetComputeIntParam(data.computeShader, "g_max_traversal_intersections", 128);


            cmd.DispatchCompute(data.computeShader, data.kernelID, data.dispatchX, data.dispatchY, 1);
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
            var normalRoughness = resourceData.gBuffer[2];
            var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            desc.name = "StochasticScreenSpaceReflection_Intersection";
            desc.enableRandomWrite = true;
            desc.format = GraphicsFormat.R16G16B16A16_SFloat;
            var intersectOutput = renderGraph.CreateTexture(desc);
            using (var builder = renderGraph.AddUnsafePass<PassData>("SSSR Intersect", out var passData))
            {
                var view = cameraData.GetViewMatrix();
                var projection = cameraData.GetProjectionMatrix();
                passData.computeShader = computeShader;
                passData.kernelID = kernelID;
                passData.sceneColor = resourceData.activeColorTexture;
                passData.sceneDepth = hierarchyZData.HizTexture;
                passData.sceneNormalSmoothness = normalRoughness;
                passData.intersectionOutput = intersectOutput;
                passData.noiseTexture = noiseTexture;
                passData.dispatchX = desc.width / 8;
                passData.dispatchY = desc.height / 8;

                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.UseTexture(passData.sceneColor);
                builder.UseTexture(passData.sceneDepth);
                builder.UseTexture(passData.sceneNormalSmoothness);
                builder.UseTexture(passData.intersectionOutput, AccessFlags.Write);
                builder.SetRenderFunc((PassData data, UnsafeGraphContext computeGraphContext) =>
                    ExecutePass(data, computeGraphContext));
            }

            resourceData.cameraColor = intersectOutput;
        }
    }
}