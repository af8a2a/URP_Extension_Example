using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Effect.Scratch
{
    public class ScratchFeature : ScriptableRendererFeature
    {
        ScratchPass scratchPass;
        [SerializeField] private Material ScratchMaterial;

        public override void Create()
        {
            UIScratchEffectSystem.instance.InitSnowMarkDrawMaterial(ScratchMaterial);
            scratchPass = new ScratchPass(RenderPassEvent.AfterRenderingOpaques);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (ScratchMaterial == null)
            {
                return;
            }

            renderer.EnqueuePass(scratchPass);
        }
    }
}