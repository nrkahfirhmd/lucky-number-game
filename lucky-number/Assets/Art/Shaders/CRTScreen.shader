// Ported from the FRAG shader in prototypes/desk.html. Defaults are the Marquee screen values.
Shader "No Winners/CRT Screen"
{
    Properties
    {
        _MainTex ("Screen (640x360)", 2D) = "black" {}
        _Strength ("CRT strength, player setting", Range(0, 1)) = 1
        _Static ("Channel-change static", Range(0, 1)) = 0
        _Collapse ("Signal loss, picture squashes to a line", Range(0, 1)) = 0
        _Curvature ("Curvature", Float) = 0.03
        _Scanline ("Scanline", Float) = 0.35
        _Mask ("Aperture mask", Float) = 0.18
        _Bloom ("Bloom", Float) = 0.36
        _Chroma ("Chroma", Float) = 0.5
        _Vignette ("Vignette", Float) = 0.13
        _Noise ("Noise", Float) = 0.012
        _Roll ("Roll", Float) = 0.015
        _Black ("Black lift", Float) = 0.012
        _Gain ("Gain", Float) = 1.26
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "CRT"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Strength, _Static, _Collapse, _Curvature, _Scanline, _Mask, _Bloom, _Chroma, _Vignette, _Noise, _Roll, _Black, _Gain;
            CBUFFER_END

            static const float2 V = float2(640.0, 360.0);
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct I { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            I vert(A a) { I o; o.pos = TransformObjectToHClip(a.pos.xyz); o.uv = a.uv; return o; }
            // Mip 0 always: fwidth below handles minification, and the art has no mips.
            float3 tex(float2 uv) { return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).rgb; }
            float3 beam(float2 uv) { return tex(float2(uv.x, (floor(uv.y * V.y) + 0.5) / V.y)); }
            float hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            float4 frag(I i) : SV_Target
            {
                float2 c = i.uv * 2.0 - 1.0;
                c *= 1.0 + _Curvature * _Strength * dot(c, c);
                c.y /= max(1.0 - _Collapse, 0.004);
                float2 uv = c * 0.5 + 0.5;
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return float4(0, 0, 0, 1);

                float ch = _Chroma * _Strength / V.x;
                float3 col = float3(beam(uv - float2(ch, 0)).r, beam(uv).g, beam(uv + float2(ch, 0)).b);

                float3 glow = 0;
                static const float2 kernel[9] = {
                    float2(0, 0),
                    float2(-2.5, 0), float2(2.5, 0), float2(0, -1.8), float2(0, 1.8),
                    float2(-1.8, -1.3), float2(1.8, -1.3), float2(-1.8, 1.3), float2(1.8, 1.3)
                };
                for (int k = 0; k < 9; k++)
                {
                    float3 s = tex(uv + kernel[k] * float2(1.5 / V.x, 1.5 / V.y));
                    glow += s * smoothstep(0.35, 0.9, max(s.r, max(s.g, s.b)));
                }
                col += glow / 9.0 * _Bloom * 2.2 * _Strength;

                // The screen's size on the display changes, so rows and mask fade before they shimmer.
                float rows = uv.y * V.y, rowFade = 1.0 - smoothstep(0.3, 0.6, fwidth(rows));
                float luma = dot(col, float3(0.3, 0.59, 0.11));
                float lineV = 0.5 + 0.5 * cos(6.28318 * (rows - 0.5));
                col *= lerp(1.0, lineV, _Scanline * _Strength * rowFade * (1.0 - 0.55 * saturate(luma)));

                float tri = uv.x * V.x * 3.0, maskFade = 1.0 - smoothstep(0.3, 0.6, fwidth(tri));
                float m = fmod(floor(tri), 3.0);
                float3 tint = m < 0.5 ? float3(1.0, 0.8, 0.8) : m < 1.5 ? float3(0.8, 1.0, 0.8) : float3(0.8, 0.8, 1.0);
                col *= lerp(float3(1, 1, 1), tint, _Mask / 0.2 * _Strength * maskFade);
                col *= lerp(1.0, lerp(1.1, _Gain, rowFade), _Strength);
                col += _Black * _Strength;

                float2 v = uv * (1.0 - uv);
                col *= pow(abs(16.0 * v.x * v.y), _Vignette * 2.2 * _Strength);

                float t = _Time.y, f = frac(uv.y + t * 0.07);
                col *= 1.0 + _Roll * _Strength * smoothstep(0.0, 0.08, f) * smoothstep(0.16, 0.08, f);
                col += (hash(i.uv * 900.0 + t * 61.0) - 0.5) * _Noise * _Strength;

                // Stays mid-brightness, because the game allows no full-screen white flashes.
                col = lerp(col, 0.2 + 0.45 * hash(float2(floor(uv.y * 90.0), floor(uv.x * 160.0)) + t), _Static);
                col = lerp(col, float3(0.95, 0.95, 0.9), smoothstep(0.9, 1.0, _Collapse));
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}
