using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Features.InterleavedTexture
{
    public class InterleavedTextureGenerator
    {
        private ComputeShader InterleavedTextureCS;

        private static readonly Lazy<InterleavedTextureGenerator> _instance = new Lazy<InterleavedTextureGenerator>();
        public static InterleavedTextureGenerator Instance => _instance.Value;

        private static int _interleavedInputTextureID = Shader.PropertyToID("Input");
        private static int _interleavedResultTextureID = Shader.PropertyToID("Result");

        private int kernelID;

        public InterleavedTextureGenerator()
        {
            InterleavedTextureCS = Resources.Load<ComputeShader>("InterleavedTexture");
            kernelID = InterleavedTextureCS.FindKernel("PrepareInterleavedTexture");
        }

        
        public RTHandle CreateTexture2DArray(CommandBuffer cmd, RTHandle rtInput,ref RTHandle rtOutput)
        {
            cmd.SetComputeTextureParam(InterleavedTextureCS, kernelID, _interleavedInputTextureID, rtInput);
            cmd.SetComputeTextureParam(InterleavedTextureCS, kernelID, _interleavedResultTextureID, rtOutput);

            cmd.DispatchCompute(InterleavedTextureCS, kernelID, 8, 8, 1);
            
            return rtOutput;
        }
    }
}