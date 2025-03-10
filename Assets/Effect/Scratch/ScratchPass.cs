using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Effect.Scratch
{
    public class ScratchPass : ScriptableRenderPass
    {
        public ScratchPass(RenderPassEvent evt)
        {
            profilingSampler = new ProfilingSampler(nameof(ScratchPass));
            renderPassEvent = evt;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("ScratchPass");

            using (new ProfilingScope(cmd, profilingSampler))
            {
                UIScratchEffectSystem.instance.RenderMask(cmd);
            }

            context.ExecuteCommandBuffer(cmd);
        }
    }
}