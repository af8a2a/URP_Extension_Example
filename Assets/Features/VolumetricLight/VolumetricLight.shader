Shader "Volumetric Light"
{
    HLSLINCLUDE
    #pragma exclude_renderers gles

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    float4 _BlitTexture_TexelSize;

    float4 _SourceTexLowMip_TexelSize;

    TEXTURE2D(_SourceTexture);
    float _Intensity;

    half3 DecodeHDR(half4 color)
    {
        #if UNITY_COLORSPACE_GAMMA
            color.xyz *= color.xyz; // γ to linear
        #endif

        #if _USE_RGBM
            return DecodeRGBM(color);
        #else
        return color.xyz;
        #endif
    }

    half4 EncodeHDR(half3 color)
    {
        #if _USE_RGBM
            half4 outColor = EncodeRGBM(color);
        #else
        half4 outColor = half4(color, 1.0);
        #endif

        #if UNITY_COLORSPACE_GAMMA
            return half4(sqrt(outColor.xyz), outColor.w); // linear to γ
        #else
        return outColor;
        #endif
    }



    float3 GetWorldPosition(float3 positionHCS)
    {
        /* get world space position from clip position */

        float2 UV = positionHCS.xy / _ScaledScreenParams.xy;
        #if UNITY_REVERSED_Z
        real depth = SampleSceneDepth(UV);
        #else
    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
        #endif
        return ComputeWorldSpacePosition(UV, depth, UNITY_MATRIX_I_VP);
    }

    half4 Frag(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float texelSize = _BlitTexture_TexelSize.y;
        float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

        
        float3 worldPos = GetWorldPosition(input.positionCS);


        return half4(worldPos,1);
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100




        Pass
        {
            Name "Blend"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

    }
}