// Animated painted backdrop for the 2D renderer. The colour strip comes from SpriteRenderer (_MainTex);
// _MaskTex is the Blender "animation mask" pass of the same strip:
//   R = screens / indicator glyphs  -> flicker, rolling scan band, faint interference lines
//   G = liquid                      -> wobble refraction, drifting caustics, rising glints
//   B = light shafts / lamps        -> slow breathing, travelling dust specks inside the beams
// Everything else is untouched, so the art stays exactly as painted.
Shader "Biopulse/LivingBackdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _MaskTex ("Animation mask", 2D) = "black" {}
        _Intensity ("Effect intensity", Range(0, 2)) = 1
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "CanUseSpriteAtlas" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                float _Intensity;
                float _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = v.uv;
                o.color = v.color;
                o.world = ws.xy;
                return o;
            }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y + _Seed;
                half4 m = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv) * _Intensity;
                float2 uv = i.uv;
                // pixel-space coordinates of this strip (stable under tiling and parallax)
                float2 px = i.uv * _MainTex_TexelSize.zw;

                // liquid: refraction wobble
                float2 wob = float2(sin(px.y * 0.09 + t * 2.1) + sin(px.y * 0.031 - t * 1.3), cos(px.x * 0.05 + t * 1.7)) * 0.9;
                uv += wob * _MainTex_TexelSize.xy * m.g;
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // liquid: caustic ripples + rising glints
                float ca = noise(px * 0.035 + float2(t * 0.25, -t * 0.6)) * noise(px * 0.06 - float2(t * 0.4, t * 0.9));
                float glint = step(0.985, hash21(floor(float2(px.x * 0.12, px.y * 0.12 - t * 3.5))));
                c.rgb += m.g * (ca * 0.35 + glint * 0.35) * c.rgb;

                // screens: flicker + rolling scan band + interference lines
                float flick = 1 + 0.06 * sin(t * 47 + _Seed) + 0.05 * (noise(float2(t * 6, _Seed)) - 0.5);
                float drop = step(0.97, noise(float2(t * 2.3, _Seed * 3))) * 0.35;
                float band = smoothstep(0.0, 0.08, frac(px.y * 0.004 - t * 0.35)) * smoothstep(0.16, 0.08, frac(px.y * 0.004 - t * 0.35));
                float lines = 0.92 + 0.08 * sin(px.y * 1.6);
                c.rgb = lerp(c.rgb, c.rgb * (flick - drop) * lines + band * 0.18 * c.rgb, m.r);

                // shafts / lamps: breathing + dust specks drifting inside the beam
                float breath = 1 + 0.18 * sin(t * 0.9 + i.world.x * 0.3) + 0.06 * sin(t * 3.1);
                float2 dp = px * 0.08 + float2(t * 0.6, t * 1.1);
                float dust = step(0.992, hash21(floor(dp))) * smoothstep(0.5, 0.0, length(frac(dp) - 0.5));
                c.rgb = lerp(c.rgb, c.rgb * breath, m.b) + m.b * dust * 0.6;
                c.a = saturate(c.a + m.b * dust * 0.4);

                return c * i.color;
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}
