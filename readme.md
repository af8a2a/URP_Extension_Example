My Technical Artist Demo Repo  

Here is my technical art demo repository, showcasing prototypes of techniques and effects that interest me.  
Performance, visual effects, and implementations are not optimized based on specific assumptions or architectures, making it easier to adapt to any project.


Implement:
- Rendering Feature:
  - Snapdragon-GSR2:https://github.com/SnapdragonStudios/snapdragon-gsr in URP14,base on TAAU,2 pass upscaler.
  - ScreenspaceReflection: SSR prototype in URP14.
  - Ground Truth Ambient Occlusion: reference by https://github.com/bladesero/GTAO_URP
  - VolumetricLight: VolumetricLight prototype in URP14.
  - Diffusion: blur the dark.
  - Film: TV Noise effect.
  - LPM: https://github.com/GPUOpen-Effects/FidelityFX-LPM/ in URP14,adjust color saturation by luminance.
- Effect
  - UI Image Scratch: draw mask in RT,calculate average rate by mipmap
  - Glass:lerp in opaque texture and self color





