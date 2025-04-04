using Features.InterleavedTexture;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace URP_Extension.Features.Playground
{
    public class PlayGroundPass : ScriptableRenderPass
    {
        internal class PassData
        {
            internal TextureHandle cameraTexture;
            internal TextureHandle targetTexture;
            internal int width;
            internal int height;
        }

        static void ExecutePass(PassData data, ComputeGraphContext cgContext)
        {
            var cmd = cgContext.cmd;
            InterleavedTextureGenerator.Instance.RenderInterleavedTexture(cmd, data.cameraTexture, data.targetTexture,data.width/8,data.height/8);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            var cameraColor = resourceData.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            desc.enableRandomWrite = true;
            desc.dimension = TextureDimension.Tex2DArray;
            desc.width /= 2;
            desc.height /= 2;
            desc.slices = 4;
            desc.name = "Playground";
            var output = renderGraph.CreateTexture(desc);
            using (var builder = renderGraph.AddComputePass<PassData>("Playground", out var data))
            {
                builder.AllowPassCulling(false);
                data.cameraTexture = cameraColor;
                data.targetTexture = output;
                data.width = desc.width;
                data.height = desc.height;
                builder.UseTexture(data.cameraTexture, AccessFlags.ReadWrite);
                builder.UseTexture(data.targetTexture, AccessFlags.ReadWrite);

                builder.SetRenderFunc((PassData data, ComputeGraphContext cgContext) => ExecutePass(data, cgContext));
            }
        }

        public void Setup()
        {
        }
    }
}