using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

[Serializable]
public class VolumetricLightSettings
{
    public float Intensity;
    public float _StepTime;
}

public class VolumetricLightFeature : ScriptableRendererFeature
{
    public Material material;
    public VolumetricLightSettings settings;

    class CustomRenderPass : ScriptableRenderPass
    {
        private Material _material;
        private RTHandle sourceRT;
        private VolumetricLightSettings _settings;

        public CustomRenderPass(Material material, VolumetricLightSettings settings)
        {
            _material = material;
            _settings = settings;
        }


        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null)
            {
                return;
            }
            var camera = renderingData.cameraData.camera;

            if (camera.cameraType == CameraType.Preview)
            {
                return;
            }


            var cmd = CommandBufferPool.Get("VolumetricLight");
            var desc = new RenderTextureDescriptor(renderingData.cameraData.cameraTargetDescriptor.width,
                renderingData.cameraData.cameraTargetDescriptor.height, RenderTextureFormat.RGB111110Float);

            _material.SetFloat("_StepTime", _settings._StepTime);
            _material.SetFloat("_Intensity", _settings.Intensity);

            RenderingUtils.ReAllocateIfNeeded(ref sourceRT, desc, name: "VolumetricLightRT");


            Blit(cmd,renderingData.cameraData.renderer.cameraColorTargetHandle, sourceRT, _material, 0);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
    }

    public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

    CustomRenderPass m_ScriptablePass;


    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass(material, settings);

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = renderPassEvent;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null)
        {
            SetActive(false);
            return;
        }

        renderer.EnqueuePass(m_ScriptablePass);
    }
}