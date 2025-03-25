using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

namespace ForwardSSR
{
    public class BackFaceDepthPass : ScriptableRenderPass
    {
        const string profilerTag = "Render Backface Depth";
        public ScreenSpaceReflection ssrVolume;
        private RTHandle backFaceDepthHandle;

        private RenderStateBlock depthRenderStateBlock = new(RenderStateMask.Nothing);

        private const string ssrShaderName = "Screen Space Raytracing";

        [SerializeField] private Material material;
        private Material ssrMaterial
        {
            get
            {
                if (material == null)
                {
                    material = new Material(Shader.Find(ssrShaderName));
                }

                return material;
            }
        }

        

        public void Dispose()
        {
            backFaceDepthHandle?.Release();
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(ref backFaceDepthHandle, desc, FilterMode.Point, TextureWrapMode.Clamp,
                name: "_CameraBackDepthTexture");
            cmd.SetGlobalTexture("_CameraBackDepthTexture", backFaceDepthHandle);
            ssrVolume = VolumeManager.instance.stack.GetComponent<ScreenSpaceReflection>();

        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (backFaceDepthHandle != null)
                cmd.ReleaseTemporaryRT(Shader.PropertyToID(backFaceDepthHandle.name));
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            backFaceDepthHandle = null;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (ssrVolume.thicknessMode.value == ScreenSpaceReflection.ThicknessMode.ComputeBackface)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
                {
                    cmd.SetRenderTarget(
                        backFaceDepthHandle,
                        RenderBufferLoadAction.DontCare,
                        RenderBufferStoreAction.DontCare,
                        backFaceDepthHandle,
                        RenderBufferLoadAction.DontCare,
                        RenderBufferStoreAction.Store);
                    cmd.ClearRenderTarget(clearDepth: true, clearColor: false, Color.clear);

                    RendererListDesc rendererListDesc = new(new ShaderTagId("DepthOnly"), renderingData.cullResults,
                        renderingData.cameraData.camera);
                    depthRenderStateBlock.depthState = new DepthState(true, CompareFunction.LessEqual);
                    depthRenderStateBlock.mask |= RenderStateMask.Depth;
                    depthRenderStateBlock.rasterState = new RasterState(CullMode.Front);
                    depthRenderStateBlock.mask |= RenderStateMask.Raster;
                    rendererListDesc.stateBlock = depthRenderStateBlock;
                    rendererListDesc.sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                    rendererListDesc.renderQueueRange = RenderQueueRange.opaque;
                    RendererList rendererList = context.CreateRendererList(rendererListDesc);

                    cmd.DrawRendererList(rendererList);

                    ssrMaterial.EnableKeyword("_BACKFACE_ENABLED");
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
            else
                ssrMaterial.DisableKeyword("_BACKFACE_ENABLED");
        }
    }

    public class ForwardSSRFeature : ScriptableRendererFeature
    {
        public ForwardSSRUtil.Resolution resolution = ForwardSSRUtil.Resolution.Full;

        [Header("Approximation")] [Tooltip("Controls how URP compute rough reflections in approximation mode.")]
        public ForwardSSRUtil.MipmapsMode mipmapsMode = ForwardSSRUtil.MipmapsMode.Trilinear;


        ForwardGbufferPass m_ScriptablePass;
        ForwardSSRPass m_ScriptableReflectionPass;
        BackFaceDepthPass backFaceDepthPass;
        /// <inheritdoc/>
        public override void Create()
        {
            m_ScriptablePass = new ForwardGbufferPass();
            m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            m_ScriptableReflectionPass = new ForwardSSRPass(resolution, mipmapsMode);
            m_ScriptableReflectionPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            backFaceDepthPass = new BackFaceDepthPass();
            backFaceDepthPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_ScriptablePass);
            renderer.EnqueuePass(m_ScriptableReflectionPass);
            renderer.EnqueuePass(backFaceDepthPass);
        }
    }
}