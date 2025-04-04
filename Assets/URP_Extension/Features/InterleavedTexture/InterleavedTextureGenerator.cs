using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Features.InterleavedTexture
{
    public class InterleavedTextureGenerator
    {
        private ComputeShader InterleavedTextureCS;

        private static readonly Lazy<InterleavedTextureGenerator> _instance = new();
        public static InterleavedTextureGenerator Instance => _instance.Value;

        private static int _interleavedInputTextureID = Shader.PropertyToID("Input");
        private static int _interleavedResultTextureID = Shader.PropertyToID("Result");

        private int kernelID;

        public InterleavedTextureGenerator()
        {
            InterleavedTextureCS = Resources.Load<ComputeShader>("InterleavedTexture");
            kernelID = InterleavedTextureCS.FindKernel("PrepareInterleavedTexture");
        }


        public void RenderInterleavedTexture(ComputeCommandBuffer cmd, TextureHandle textureInput,
            TextureHandle resultTexture, int DispathX, int DispathY)
        {
            cmd.SetComputeTextureParam(InterleavedTextureCS, kernelID, _interleavedInputTextureID, textureInput);
            cmd.SetComputeTextureParam(InterleavedTextureCS, kernelID, _interleavedResultTextureID, resultTexture);

            cmd.DispatchCompute(InterleavedTextureCS, kernelID, DispathX, DispathY, 1);
        }
    }
}