﻿using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Effect.Scratch
{
    public class ScratchFeature : ScriptableRendererFeature
    {
        ScratchPass scratchPass;

        public override void Create()
        {
            scratchPass = new ScratchPass(RenderPassEvent.AfterRenderingOpaques);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {

            renderer.EnqueuePass(scratchPass);
        }
    }
}