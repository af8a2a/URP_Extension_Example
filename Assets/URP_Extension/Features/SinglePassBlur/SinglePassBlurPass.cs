using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace URP_Extension.Features.SinglePassBlur
{
    public class SinglePassBlurPass : ScriptableRenderPass
    {
        private ComputeShader _computeShader;
        private int _ffxBlurTileSizeX = 8;
        private int _ffxBlurTileSizeY = 8;
        private int kernelID;

        public void Setup(ComputeShader computeShader)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            _computeShader = computeShader;
            kernelID = computeShader.FindKernel("SinglePassBlur");
        }

        internal class PassData
        {
            internal ComputeShader computeShader;
            internal int kernelID;
            internal Vector2 imageSize;
            internal Vector2 BlurTileSize;
            internal TextureHandle inputTexture;
            internal TextureHandle outputTexture;
        }

        
        static void ExecutePass(PassData passData, ComputeGraphContext computeGraphContext)
        {
            var cmd = computeGraphContext.cmd;
            cmd.SetComputeVectorParam(passData.computeShader, "imageSize", passData.imageSize);

            cmd.SetComputeTextureParam(passData.computeShader, passData.kernelID, "r_input_src", passData.inputTexture);
            cmd.SetComputeTextureParam(passData.computeShader, passData.kernelID, "rw_output", passData.outputTexture);
            cmd.DispatchCompute(passData.computeShader, passData.kernelID,
                (int)(passData.imageSize.x / passData.BlurTileSize.x), (int)(passData.imageSize.y /
                                                                             passData.BlurTileSize.y),
                1);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            desc.enableRandomWrite = true;
            desc.name = "SinglePassBlur";
            var output = renderGraph.CreateTexture(desc);
            using (var builder = renderGraph.AddComputePass<PassData>("SinglePassBlurPass", out var passData))
            {
                passData.inputTexture = resourceData.activeColorTexture;
                passData.outputTexture = output;
                passData.BlurTileSize = new Vector2(_ffxBlurTileSizeX, _ffxBlurTileSizeY);
                passData.imageSize = new Vector2(desc.width, desc.height);
                passData.computeShader = _computeShader;
                passData.kernelID = kernelID;

                builder.UseTexture(passData.inputTexture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.outputTexture, AccessFlags.ReadWrite);

                builder.SetRenderFunc((PassData data, ComputeGraphContext cgContext) => ExecutePass(data, cgContext));
            }
        }
    }
}