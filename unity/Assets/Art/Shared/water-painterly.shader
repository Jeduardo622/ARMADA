// Painterly sea (art-needs.md §3 P2, GDD "ocean as a character"): layered
// value-noise bands over the reviewed sea base color, hand-written URP
// HLSL so it can be authored and diffed headless. The capture determinism
// contract survives via _Animate: 0 (the serialized default) freezes time
// at zero, so batchmode captures render a byte-stable painterly frame;
// WaterAnimator flips it to 1 in play mode only.
Shader "Armada/WaterPainterly"
{
    Properties
    {
        // The reviewed sea color (spectator-tuning.md: 0.07, 0.22, 0.36)
        // stays the mid band; [MainColor] preserves the material.color
        // contract (asset-pipeline.md §4).
        [MainColor] _BaseColor("Base Color", Color) = (0.07, 0.22, 0.36, 1)
        _DeepColor("Deep Color", Color) = (0.04, 0.13, 0.24, 1)
        _CrestColor("Crest Color", Color) = (0.13, 0.34, 0.46, 1)
        _FoamColor("Foam Color", Color) = (0.55, 0.72, 0.76, 1)
        _WaveScale("Wave Scale (per world unit)", Float) = 0.55
        _WaveSpeed("Wave Speed", Float) = 0.35
        _Animate("Animate (runtime only; captures need 0)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _DeepColor;
                half4 _CrestColor;
                half4 _FoamColor;
                float _WaveScale;
                float _WaveSpeed;
                float _Animate;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Smooth bilinear value noise; world-space input keeps the
            // pattern camera-independent.
            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(cell);
                float b = Hash(cell + float2(1, 0));
                float c = Hash(cell + float2(0, 1));
                float d = Hash(cell + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Animate * _Time.y * _WaveSpeed;
                float2 uv = input.positionWS.xz * _WaveScale;

                // Two drifting octaves; counter-directions read as swell.
                float n = 0.62 * ValueNoise(uv + float2(t, t * 0.6))
                        + 0.38 * ValueNoise(uv * 2.17 + float2(-t * 0.8, t));

                // Painterly banding: posterize the swell, keep a soft edge.
                float band = floor(n * 4.0) / 4.0;
                float soft = lerp(band, n, 0.35);

                half3 color = lerp(_DeepColor.rgb, _BaseColor.rgb, saturate(soft * 1.6));
                color = lerp(color, _CrestColor.rgb, smoothstep(0.62, 0.78, n));
                color = lerp(color, _FoamColor.rgb, smoothstep(0.86, 0.95, n) * 0.6);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
