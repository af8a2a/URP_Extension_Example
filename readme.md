My Technical Artist Demo Repo  

Here is my technical art demo repository, showcasing prototypes of techniques and effects that interest me.  
Performance, visual effects, and implementations are not optimized based on specific assumptions or architectures, making it easier to adapt to any project.


Implement:
- Rendering Feature:
  - Snapdragon-GSR2:https://github.com/SnapdragonStudios/snapdragon-gsr in URP17,base on TAAU,2 pass upscaler.
  - ScreenspaceReflection: SSR prototype in URP17.
  - Ground Truth Ambient Occlusion: reference by https://github.com/bladesero/GTAO_URP
  - VolumetricLight: VolumetricLight prototype in URP17.
  - Diffusion: blur the dark.
  - Film: TV Noise effect.
  - LPM: https://github.com/GPUOpen-Effects/FidelityFX-LPM/ in URP17,adjust color saturation by luminance.
  - SinglePassGaussianBlur:https://gpuopen.com/fidelityfx-blur/ in URP17, high performance gaussianBlur, base on Compute Shader,LDS, only D3D12,Vulkan
- Effect
  - UI Image Scratch: draw mask in RT,calculate average rate by mipmap
  - Glass:lerp in opaque texture and self color
  - OcclusionOutline: 2 pass effect, base on stencil





