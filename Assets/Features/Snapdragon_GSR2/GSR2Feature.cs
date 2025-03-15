using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Features.Snapdragon_GSR2
{
    public class GSR2Feature : ScriptableRendererFeature
    {
        public float upscaledRatio = 1;
        public float minLerpContribution = 0.3f;

        internal class GSR2Pass : ScriptableRenderPass
        {
            private Vector2[] HaltonSequence = new Vector2[32];
            private Material material;
            private int jitterIndex = 0;
            private RTHandle motionDepthClipRT;
            private RTHandle[] outputRT;
            private int frameCount = 0;
            private float upscaledRatio = 1;
            private float minLerpContribution = 0.3f;

            private float Halton(int index, int baseN)
            {
                float result = 0f;
                float invBase = 1f / baseN;
                float fraction = invBase;

                while (index > 0)
                {
                    result += (index % baseN) * fraction;
                    index /= baseN;
                    fraction *= invBase;
                }

                return result;
            }

            public GSR2Pass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                for (int i = 0; i < HaltonSequence.Length; i++) // 使用32帧序列
                {
                    HaltonSequence[i] = new Vector2(
                        Halton(i + 1, 2) - 0.5f,
                        Halton(i + 1, 3) - 0.5f
                    );
                }

                outputRT = new RTHandle[2];
                material = new Material(Shader.Find("PostProcessing/GSR2"));
            }

            private Vector2 GetJitter()
            {
                return HaltonSequence[jitterIndex % HaltonSequence.Length];
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                ConfigureInput(ScriptableRenderPassInput.Motion | ScriptableRenderPassInput.Depth);
            }

            public void Setup(float upscaledRatio, float minLerpContribution)
            {
                this.upscaledRatio = upscaledRatio;
                this.minLerpContribution = minLerpContribution;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var camera = renderingData.cameraData.camera;
                if (!camera.orthographic)
                {
                    camera.ResetProjectionMatrix();
                    var nextJitter = GetJitter();
                    var jitProj = camera.projectionMatrix;
                    camera.nonJitteredProjectionMatrix = jitProj;
                    jitProj.m02 += nextJitter.x / camera.pixelWidth;
                    jitProj.m12 += nextJitter.y / camera.pixelHeight;
                    camera.projectionMatrix = jitProj;
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var camera = renderingData.cameraData.camera;
                if (camera.cameraType == CameraType.Preview)
                {
                    return;
                }


                var cmd = CommandBufferPool.Get("GSR2Pass");
                Matrix4x4 currentViewProj = camera.nonJitteredProjectionMatrix * camera.worldToCameraMatrix;

                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;
                Vector2 renderSize = new Vector2(descriptor.width, descriptor.height);

                var outputSize = new Vector2(descriptor.width, descriptor.height);
                descriptor.width = Mathf.RoundToInt(descriptor.width / upscaledRatio);
                descriptor.height = Mathf.RoundToInt(descriptor.height / upscaledRatio);
                RenderingUtils.ReAllocateIfNeeded(ref outputRT[0], descriptor, name: "outputRT_0");
                RenderingUtils.ReAllocateIfNeeded(ref outputRT[1], descriptor, name: "outputRT_1");

                descriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
                RenderingUtils.ReAllocateIfNeeded(ref motionDepthClipRT, descriptor, name: "motionDepthClipRT");

                Matrix4x4 clipToPrevClip = camera.previousViewProjectionMatrix * Matrix4x4.Inverse(currentViewProj);

                // Calculate render sizes

                // Generate jitter using the new method
                Vector4 jitter = GetJitter();

                material.SetMatrix("_ClipToPrevClip", clipToPrevClip);
                material.SetVector("_RenderSize", renderSize);
                material.SetVector("_RenderSizeRcp", new Vector2(1f / renderSize.x, 1f / renderSize.y));
                material.SetVector("_OutputSize", outputSize);
                material.SetVector("_OutputSizeRcp", new Vector2(1f / Screen.width, 1f / Screen.height));
                material.SetVector("_JitterOffset", jitter);
                material.SetVector("_ScaleRatio",
                    new Vector2(upscaledRatio,
                        Mathf.Min(20f,
                            Mathf.Pow((outputSize.x * outputSize.y) / (renderSize.x * renderSize.y), 3f))));
                material.SetFloat("_CameraFovAngleHor",
                    Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * renderSize.x / renderSize.y);
                material.SetFloat("_MinLerpContribution", minLerpContribution);

                material.SetFloat("_Reset", frameCount == 0 ? 1f : 0f);

                var output = outputRT[0];
                var history = outputRT[1];

                Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle,
                    motionDepthClipRT,
                    material, 0);

                material.SetTexture("_PrevHistory", history);
                material.SetTexture("MotionDepthClipAlphaBuffer", motionDepthClipRT);

                Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, output,
                    material, 1);
                Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, history);
                Blitter.BlitCameraTexture(cmd, output,renderingData.cameraData.renderer.cameraColorTargetHandle);


                context.ExecuteCommandBuffer(cmd);
                cmd.Release();
                frameCount++;
                jitterIndex++;
            }
        }

        GSR2Pass mGsr2Pass;

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            mGsr2Pass.Setup(upscaledRatio, minLerpContribution);
        }

        public override void Create()
        {
            mGsr2Pass = new GSR2Pass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(mGsr2Pass);
        }
    }
}