// Cloud layer projected onto a camera-centred dome. Procedural noise, no textures.
Shader "SheltreSim/WeatherClouds"
{
    Properties
    {
        _LitColor ("Lit colour", Color) = (1,1,1,1)
        _DarkColor ("Dark colour", Color) = (0.4,0.4,0.45,1)
        _Cover ("Coverage", Range(0,1)) = 0.3
        _Wind ("Wind offset", Vector) = (0,0,0,0)
        _Scale ("Scale", Float) = 1.6
        _Flash ("Lightning", Float) = 0
        _Opacity ("Max opacity", Range(0,1)) = 0.95
    }
    SubShader
    {
        Tags { "Queue"="Transparent-100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _LitColor, _DarkColor;
            float _Cover, _Scale, _Flash, _Opacity;
            float4 _Wind;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash12(i), hash12(i + float2(1, 0)), f.x),
                            lerp(hash12(i + float2(0, 1)), hash12(i + float2(1, 1)), f.x), f.y);
            }

            float fbm(float2 p, float detail)
            {
                float v = 0, a = 0.5;
                for (int o = 0; o < 5; o++)
                {
                    v += a * vnoise(p) * (o < 3 ? 1.0 : detail);
                    p = p * 2.03 + float2(17.1, 9.2);
                    a *= 0.5;
                }
                return v / 0.97;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float fadeH = smoothstep(0.02, 0.32, d.y);
                if (fadeH <= 0) return fixed4(0, 0, 0, 0);
                // Project onto a flat layer; near the horizon the pattern compresses, so fade the fine detail out there.
                float2 uv = d.xz / (d.y + 0.18) * _Scale + _Wind.xy;
                float detail = smoothstep(0.05, 0.4, d.y);
                float n = fbm(uv, detail);
                float warp = vnoise(uv * 0.35 + 3.1);
                n = lerp(n, n * 0.7 + warp * 0.3, 0.5);
                float edge = lerp(0.62, 0.30, _Cover);     // threshold falls as coverage grows
                float soft = lerp(0.10, 0.5, _Cover);
                float a = smoothstep(edge - soft * 0.3, edge + soft, n);
                a *= saturate(_Cover * 3.0);
                // Thicker core is darker, thin rim is bright.
                float shade = saturate((n - edge) / (soft + 0.2));
                float3 col = lerp(_LitColor.rgb, _DarkColor.rgb, shade * lerp(0.25, 0.75, saturate((_Cover - 0.3) / 0.5)));
                col += float3(0.7, 0.78, 1.0) * _Flash * (0.4 + 0.6 * (1 - shade));
                return fixed4(col, a * fadeH * _Opacity);
            }
            ENDCG
        }
    }
}
