// Sprite-Lit-Default with 2D light bands quantized and ordered-dithered on the art's pixel grid.
Shader "Poko Pond/Pixel Lit Sprite"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _LightSteps("Light Steps", Range(1, 16)) = 5
        _DitherStrength("Dither Strength", Range(0, 1)) = 1
        _PixelsPerUnit("Dither Pixels Per Unit", Float) = 32
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightVariables.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS_SHARED
                half2 lightingUV   : TEXCOORD1;
                float2 gridPos     : TEXCOORD2;
                half4 color        : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _LightSteps;
                half _DitherStrength;
                float _PixelsPerUnit;
            CBUFFER_END

            static const half kBayer4x4[16] =
            {
                 0.0/16,  8.0/16,  2.0/16, 10.0/16,
                12.0/16,  4.0/16, 14.0/16,  6.0/16,
                 3.0/16, 11.0/16,  1.0/16,  9.0/16,
                15.0/16,  7.0/16, 13.0/16,  5.0/16
            };

            #define ACCUMULATE_SHAPE_LIGHT(i)                                                                                   \
            {                                                                                                                   \
                half4 shapeLight = SAMPLE_TEXTURE2D(_ShapeLightTexture##i, sampler_ShapeLightTexture##i, input.lightingUV);     \
                if (any(_ShapeLightMaskFilter##i))                                                                              \
                {                                                                                                               \
                    half4 processedMask = (1 - _ShapeLightInvertedFilter##i) * mask + _ShapeLightInvertedFilter##i * (1 - mask); \
                    shapeLight *= dot(processedMask, _ShapeLightMaskFilter##i);                                                 \
                }                                                                                                               \
                modulate += shapeLight * _ShapeLightBlendFactors##i.x;                                                          \
                additive += shapeLight * _ShapeLightBlendFactors##i.y;                                                          \
                anyLight = true;                                                                                                \
            }

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.gridPos = TransformObjectToWorld(input.positionOS).xy * _PixelsPerUnit;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half3 Posterize(half3 light, float2 gridPos)
            {
                uint2 cell = (uint2)(int2)floor(gridPos) & 3;
                half threshold = lerp(0.5, kBayer4x4[cell.y * 4 + cell.x], _DitherStrength);
                return floor(light * _LightSteps + threshold) / _LightSteps;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 color = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (color.a == 0.0)
                    discard;

                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                half4 modulate = 0;
                half4 additive = 0;
                bool anyLight = false;

                #if USE_SHAPE_LIGHT_TYPE_0
                ACCUMULATE_SHAPE_LIGHT(0)
                #endif
                #if USE_SHAPE_LIGHT_TYPE_1
                ACCUMULATE_SHAPE_LIGHT(1)
                #endif
                #if USE_SHAPE_LIGHT_TYPE_2
                ACCUMULATE_SHAPE_LIGHT(2)
                #endif
                #if USE_SHAPE_LIGHT_TYPE_3
                ACCUMULATE_SHAPE_LIGHT(3)
                #endif

                if (!anyLight)
                    return color;

                half3 lit = color.rgb * Posterize(modulate.rgb, input.gridPos) + Posterize(additive.rgb, input.gridPos);
                return half4(max(0, _HDREmulationScale * lit), color.a);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4   color           : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _LightSteps;
                half _DitherStrength;
                float _PixelsPerUnit;
            CBUFFER_END

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                return CommonNormalsFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _LightSteps;
                half _DitherStrength;
                float _PixelsPerUnit;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }
}
