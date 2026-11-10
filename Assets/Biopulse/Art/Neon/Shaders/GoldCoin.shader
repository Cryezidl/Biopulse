Shader "Biopulse/GoldCoin"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HDR] _GoldColor ("Warm gold emission", Color) = (1, 0.62, 0.18, 1)
        _Emission ("Emission strength", Range(0, 3)) = 0.65
        _PulseDepth ("Pulse depth", Range(0, 0.5)) = 0.16
        _PulseSpeed ("Pulse speed", Range(0, 5)) = 1.8
        _GlintStrength ("Passing highlight", Range(0, 3)) = 1.1
        _GlintSpeed ("Highlight speed", Range(0, 1)) = 0.24
        _GlintWidth ("Highlight width (world units)", Range(0.01, 0.2)) = 0.07
        _InkThreshold ("Protect dark ink", Range(0, 0.5)) = 0.08
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip ("Flip", Vector) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "CanUseSpriteAtlas" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "GoldCoinUnlit"
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _GoldColor;
                float _Emission;
                float _PulseDepth;
                float _PulseSpeed;
                float _GlintStrength;
                float _GlintSpeed;
                float _GlintWidth;
                float _InkThreshold;
            CBUFFER_END

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };
            struct Varyings
            {
                COMMON_2D_OUTPUTS_SHARED
                half4 color : COLOR;
                float2 localPosition : TEXCOORD1;
                float phase : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                output.localPosition = input.positionOS.xy;
                // Shared material, different timing for coins at different positions.
                output.phase = frac(TransformObjectToWorld(float3(0, 0, 0)).x * 0.137) * TWO_PI;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(sprite.rgb, half3(0.2126, 0.7152, 0.0722));
                // Emission follows the painted gold; the outline and engraved joints stay dark.
                half gold = smoothstep(_InkThreshold, _InkThreshold + 0.24, luminance);
                float pulse = 1 + _PulseDepth * sin(_Time.y * _PulseSpeed + input.phase);
                // Object-space sweep also works when the texture is packed into an atlas.
                float sweep = lerp(-0.65, 0.65, frac(_Time.y * _GlintSpeed + input.phase / TWO_PI));
                float distanceToBand = abs(input.localPosition.x + input.localPosition.y * 0.45 - sweep);
                float band = 1 - smoothstep(0, _GlintWidth, distanceToBand);
                half3 emission = _GoldColor.rgb * gold * (_Emission * pulse + band * _GlintStrength);
                return half4((sprite.rgb + emission) * input.color.rgb, sprite.a * input.color.a);
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}
