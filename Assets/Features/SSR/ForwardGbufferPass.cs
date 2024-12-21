using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

namespace ForwardSSR
{
    public class ForwardGbufferPass : ScriptableRenderPass,IDisposable
    {
        private readonly static FieldInfo renderingModeFieldInfo =
            typeof(UniversalRenderer).GetField("m_RenderingMode", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly static FieldInfo normalsTextureFieldInfo =
            typeof(UniversalRenderer).GetField("m_NormalsTexture", BindingFlags.NonPublic | BindingFlags.Instance);

        public RTHandle gBuffer0;
        public RTHandle gBuffer1;
        public RTHandle gBuffer2;
        public RTHandle depthHandle;
        private RTHandle[] gBuffers;

        public void Dispose()
        {
            gBuffer0?.Release();
            gBuffer1?.Release();
            gBuffer2?.Release();
            depthHandle?.Release();
        }

        public GraphicsFormat GetGBufferFormat(int index)
        {
            if (index == 0) // sRGB albedo, materialFlags
                return QualitySettings.activeColorSpace == ColorSpace.Linear
                    ? GraphicsFormat.R8G8B8A8_SRGB
                    : GraphicsFormat.R8G8B8A8_UNorm;
            else if (index == 1) // sRGB specular, occlusion
                return GraphicsFormat.R8G8B8A8_UNorm;
            else if (index == 2) // normal normal normal packedSmoothness
                // NormalWS range is -1.0 to 1.0, so we need a signed render texture.
#if UNITY_2023_2_OR_NEWER
                if (SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SNorm, GraphicsFormatUsage.Render))
#else
                if (SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SNorm, FormatUsage.Render))
#endif
                    return GraphicsFormat.R8G8B8A8_SNorm;
                else
                    return GraphicsFormat.R16G16B16A16_SFloat;
            else
                return GraphicsFormat.None;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0; // Color and depth cannot be combined in RTHandles
            desc.stencilFormat = GraphicsFormat.None;
            desc.msaaSamples = 1; // Do not enable MSAA for GBuffers.

            // Albedo.rgb + MaterialFlags.a
            desc.graphicsFormat = GetGBufferFormat(0);
            RenderingUtils.ReAllocateIfNeeded(ref gBuffer0, desc, FilterMode.Point, TextureWrapMode.Clamp,
                name: "_GBuffer0");
            cmd.SetGlobalTexture("_GBuffer0", gBuffer0);

            // Specular.rgb + Occlusion.a
            desc.graphicsFormat = GetGBufferFormat(1);
            RenderingUtils.ReAllocateIfNeeded(ref gBuffer1, desc, FilterMode.Point, TextureWrapMode.Clamp,
                name: "_GBuffer1");
            cmd.SetGlobalTexture("_GBuffer1", gBuffer1);

            // If "_CameraNormalsTexture" exists (lacking smoothness info), set the target to it instead of creating a new RT.
            if (normalsTextureFieldInfo.GetValue(renderingData.cameraData.renderer) is not RTHandle
                    normalsTextureHandle ||
                renderingData.cameraData.cameraType ==
                CameraType
                    .SceneView) // There're a problem (wrong render target) of reusing normals texture in scene view.
            {
                // NormalWS.rgb + Smoothness.a
                desc.graphicsFormat = GetGBufferFormat(2);
                RenderingUtils.ReAllocateIfNeeded(ref gBuffer2, desc, FilterMode.Point, TextureWrapMode.Clamp,
                    name: "_GBuffer2");
                cmd.SetGlobalTexture("_GBuffer2", gBuffer2);
                gBuffers = new RTHandle[] { gBuffer0, gBuffer1, gBuffer2 };
            }
            else
            {
                cmd.SetGlobalTexture("_GBuffer2", normalsTextureHandle);
                gBuffers = new RTHandle[] { gBuffer0, gBuffer1, normalsTextureHandle };
            }

            if (renderingData.cameraData.renderer.cameraDepthTargetHandle.isMSAAEnabled)
            {
                RenderTextureDescriptor depthDesc = renderingData.cameraData.cameraTargetDescriptor;
                depthDesc.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(ref depthHandle, depthDesc, FilterMode.Point,
                    TextureWrapMode.Clamp, name: "_GBuffersDepthTexture");
                ConfigureTarget(gBuffers, depthHandle);
            }
            else
                ConfigureTarget(gBuffers, renderingData.cameraData.renderer.cameraDepthTargetHandle);

            // [OpenGL] Reusing the depth buffer seems to cause black glitching artifacts, so clear the existing depth.
            bool isOpenGL = (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3) ||
                            (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore); // GLES 2 is removed.
            if (isOpenGL || renderingData.cameraData.renderer.cameraDepthTargetHandle.isMSAAEnabled)
                ConfigureClear(ClearFlag.Color | ClearFlag.Depth, Color.black);
            else
                // We have to also clear previous color so that the "background" will remain empty (black) when moving the camera.
                ConfigureClear(ClearFlag.Color, Color.clear);

            // Reduce GBuffer overdraw using the depth from opaque pass. (excluding OpenGL platforms)
            if (!isOpenGL &&
                (renderingData.cameraData.renderType == CameraRenderType.Base ||
                 renderingData.cameraData.clearDepth) &&
                !renderingData.cameraData.renderer.cameraDepthTargetHandle.isMSAAEnabled)
            {
                renderStateBlock.depthState = new DepthState(false, CompareFunction.Equal);
                renderStateBlock.mask |= RenderStateMask.Depth;
            }
            else if (renderStateBlock.depthState.compareFunction == CompareFunction.Equal)
            {
                renderStateBlock.depthState = new DepthState(true, CompareFunction.LessEqual);
                renderStateBlock.mask |= RenderStateMask.Depth;
            }
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(Shader.PropertyToID(gBuffer0.name));
            cmd.ReleaseTemporaryRT(Shader.PropertyToID(gBuffer1.name));
            cmd.ReleaseTemporaryRT(Shader.PropertyToID(gBuffer2.name));
            if (depthHandle != null)
                cmd.ReleaseTemporaryRT(Shader.PropertyToID(depthHandle.name));
        }

        private RenderStateBlock renderStateBlock = new(RenderStateMask.Everything);

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;

            if (camera.cameraType == CameraType.Preview)
            {
                return;
            }

            var cmd = CommandBufferPool.Get("ForwardGbuffer");


            SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;


            RendererListDesc rendererListDesc = new(new ShaderTagId("UniversalGBuffer"), renderingData.cullResults,
                renderingData.cameraData.camera);
            rendererListDesc.stateBlock = renderStateBlock;
            rendererListDesc.sortingCriteria = sortingCriteria;
            rendererListDesc.renderQueueRange = RenderQueueRange.opaque;
            RendererList rendererList = context.CreateRendererList(rendererListDesc);

            cmd.DrawRendererList(rendererList);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }


    }
}