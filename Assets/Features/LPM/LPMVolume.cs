using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Features.LPM
{
    public enum DisplayMode
    {
        SDR,
        DISPLAYMODE_HDR10_SCRGB,
        DISPLAYMODE_HDR10_2084
    }

    public class LPMVolume : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter Intensity = new ClampedFloatParameter(1f, 0f, 1f);
        public FloatParameter HdrMax = new FloatParameter(256.0f);
        public ClampedFloatParameter SoftGap = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter Exposure = new ClampedFloatParameter(0.0f, -4.0f, 1.0f);
        public ClampedFloatParameter LPMExposure = new ClampedFloatParameter(8.0f, 3.0f, 11.0f);
        public ClampedFloatParameter Contrast = new ClampedFloatParameter(0.3f, 0.0f, 1.0f);
        public ClampedFloatParameter ShoulderContrast = new ClampedFloatParameter(1.0f, 1.0f, 1.2f);
        public NoInterpVector3Parameter Saturation = new NoInterpVector3Parameter(Vector3.zero);
        public NoInterpVector3Parameter Crosstalk = new NoInterpVector3Parameter(new Vector3(1.0f, 0.5f, 1.0f / 32.0f));
        public VolumeParameter<DisplayMode> displayMode = new VolumeParameter<DisplayMode>();

        public bool IsActive()
        {
            return Intensity.value > 0f && active;
        }

        public bool IsTileCompatible() => false;
    }
}