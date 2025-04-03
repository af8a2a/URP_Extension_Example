using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace URP_Extension.Features.HierarchyZGenerator
{
    public class HierarchyZPass : ScriptableRenderPass
    {
        public ComputeShader cs;
        List<int> list = new List<int>() { 1 };

        public class HierarchyZPassData
        {
            // Compute shader.
            public ComputeShader cs;
            public int kernel;
            public int dimX;
            public int dimY;

            public int mipCount;

            // Buffer handles for the compute buffers.
            public TextureHandle input;
            public TextureHandle output;
        }

        int GetMipsCount(float textureWidth, float textureHeight)
        {
            float maxDim = Mathf.Max(textureWidth, textureHeight);
            return Mathf.FloorToInt(Mathf.Log(maxDim, 2));
        }

        public void Setup(ComputeShader cs)
        {
            this.cs = cs;
        }


        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var depthTexture = resourceData.cameraDepth;

            var kernel = cs.FindKernel("HizMain");

            // Each threadgroup works on 64x64 texels
            // uint32_t dimX = (m_Width + 63) / 64;
            // uint32_t dimY = (m_Height + 63) / 64;
            // vkCmdDispatch(cb, dimX, dimY, 1);
            var destinationDesc = renderGraph.GetTextureDesc(depthTexture);
            destinationDesc.width >>= 1;
            destinationDesc.height >>= 1;
            destinationDesc.depthBufferBits = 0;
            destinationDesc.useMipMap = true;
            // destinationDesc.autoGenerateMips = false;
            destinationDesc.name = $"Hi-Z_Texture";
            destinationDesc.clearBuffer = false;
            destinationDesc.dimension = TextureDimension.Tex2D;
            destinationDesc.enableRandomWrite = true;
            destinationDesc.depthBufferBits = 0;
            destinationDesc.colorFormat = GraphicsFormat.R32_SFloat;

            TextureHandle hierarchyZTexture = renderGraph.CreateTexture(destinationDesc);

            using (var builder = renderGraph.AddComputePass(nameof(HierarchyZPass), out HierarchyZPassData passData))
            {
                // Set the pass data so the data can be transfered from the recording to the execution.
                builder.AllowPassCulling(false);
                passData.cs = cs;
                passData.input = depthTexture;
                passData.output = hierarchyZTexture;
                passData.kernel = kernel;
                passData.dimX = destinationDesc.width;
                passData.dimY = destinationDesc.height;
                passData.mipCount = GetMipsCount(destinationDesc.width, destinationDesc.height);
                // UseBuffer is used to setup render graph dependencies together with read and write flags.
                builder.UseTexture(passData.input, AccessFlags.Write);
                builder.UseTexture(passData.output, AccessFlags.ReadWrite);
                // The execution function is also call SetRenderfunc for compute passes.
                builder.SetRenderFunc((HierarchyZPassData data, ComputeGraphContext cgContext) =>
                    ExecutePass(data, cgContext));
            }
        }


        static void ExecutePass(HierarchyZPassData data, ComputeGraphContext cgContext)
        {
            // Attaches the compute buffers.
            var depthSource = data.input;
            var lastIdx = 0;
            for (int i = 0; i < data.mipCount; i++)
            {
                cgContext.cmd.SetComputeTextureParam(data.cs, data.kernel, "DepthTexture",
                    depthSource, lastIdx);


                cgContext.cmd.SetComputeTextureParam(data.cs, data.kernel, "DepthTextureOutput",
                    data.output, i);
                cgContext.cmd.DispatchCompute(data.cs, data.cs.FindKernel("HizMain"), data.dimX / 8, data.dimY / 8, 1);
                depthSource = data.output;
                lastIdx = i;
            }
        }
    }
}