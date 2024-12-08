Shader "Glass"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        _Intensity("Intensity", Range(0.0, 1.0)) = 0.5
        _Radius("Blur Radius", Range(0.0, 5.0)) = 0.5
    }
    HLSLINCLUDE
    #define SAMPLE_TEXTURE2D
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"

    float _Intensity;
    float _Radius;

    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float4 tangentOS : TANGENT;
        float2 texcoord : TEXCOORD0;
        float2 staticLightmapUV : TEXCOORD1;
        float2 dynamicLightmapUV : TEXCOORD2;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float2 uv : TEXCOORD0;
        float3 positionWS : TEXCOORD1;
        float3 normalWS : TEXCOORD2;
        half4 tangentWS : TEXCOORD3; // xyz: tangent, w: sign
        half3 viewDirTS : TEXCOORD7;
        float4 positionCS : SV_POSITION;
    };

    Varyings LitPassVertex(Attributes input)
    {
        Varyings output = (Varyings)0;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

        // normalWS and tangentWS already normalize.
        // this is required to avoid skewing the direction during interpolation
        // also required for per-vertex lighting and SH evaluation
        VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
        output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

        // already normalized from normal transform to WS.
        output.normalWS = normalInput.normalWS;
        real sign = input.tangentOS.w * GetOddNegativeScale();
        half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
        output.tangentWS = tangentWS;

        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
        half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
        output.viewDirTS = viewDirTS;


        output.positionWS = vertexInput.positionWS;


        output.positionCS = vertexInput.positionCS;

        return output;
    }

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

    half4 FragBlurH(Varyings input)
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
        float texelSize = _ScreenSize.w * _Radius;


        // 9-tap gaussian blur on the downsampled source
        half3 c0 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
                                                uv - float2(texelSize * 4.0, 0.0)));
        half3 c1 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
                                                        uv - float2(texelSize * 3.0, 0.0)));
        half3 c2 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
         uv - float2(texelSize * 2.0, 0.0)));
        half3 c3 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
      uv - float2(texelSize * 1.0, 0.0)));
        half3 c4 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp, uv));
        half3 c5 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
uv + float2(texelSize * 1.0, 0.0)));
        half3 c6 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
                                                                               uv + float2(texelSize * 2.0, 0.0)));
        half3 c7 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
                              uv + float2(texelSize * 3.0, 0.0)));
        half3 c8 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
                                                           uv + float2(texelSize * 4.0, 0.0)));

        half3 color = c0 * 0.01621622 + c1 * 0.05405405 + c2 * 0.12162162 + c3 * 0.19459459
            + c4 * 0.22702703
            + c5 * 0.19459459 + c6 * 0.12162162 + c7 * 0.05405405 + c8 * 0.01621622;
        return EncodeHDR(color);
    }

    half4 FragBlurV(Varyings input)
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
        float texelSize = _ScreenSize.w * _Radius;

        // Optimized bilinear 5-tap gaussian on the same-sized source (9-tap equivalent)
        half3 c0 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
                                                                   uv - float2(0.0, texelSize * 3.23076923)));
        half3 c1 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
            uv - float2(0.0, texelSize * 1.38461538)));
        half3 c2 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp, uv));
        half3 c3 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
            uv + float2(0.0, texelSize * 1.38461538
            )));
        half3 c4 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
                                                   uv + float2(0.0, texelSize * 3.23076923)));

        half3 color = c0 * 0.07027027 + c1 * 0.31621622
            + c2 * 0.22702703
            + c3 * 0.31621622 + c4 * 0.07027027;

        return EncodeHDR(color);
    }


    half4 Frag(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);

        float4 base_color = SAMPLE_TEXTURE2D_X(_BaseMap, sampler_LinearClamp, uv);
        // float2 screenspace_uv = GetNormalizedScreenSpaceUV(input.positionCS);
        // float texelSize = _ScreenSize.w * _Radius;
        // half3 c0 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
        //                                             screenspace_uv - float2(0.0, texelSize * 3.23076923)));
        // half3 c1 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
        //                                  screenspace_uv - float2(0.0, texelSize * 1.38461538)));
        // half3 c2 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp, screenspace_uv));
        // half3 c3 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
        //              screenspace_uv + float2(0.0, texelSize * 1.38461538
        //              )));
        // half3 c4 = DecodeHDR(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_LinearClamp,
        //                                screenspace_uv + float2(0.0, texelSize * 3.23076923
        //                                )));
        half4 blurH = FragBlurH(input);
        half4 blurV = FragBlurV(input);

        half4 color = lerp(base_color, (blurH + blurV) / 2.0, _Intensity);
        return EncodeHDR(color);
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
            Name "Frag"

            HLSLPROGRAM
            #pragma vertex LitPassVertex
            #pragma fragment Frag
            ENDHLSL
        }


    }
}