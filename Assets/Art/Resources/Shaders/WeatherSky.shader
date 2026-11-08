// Procedural sky driven entirely by WeatherPresentation. Own shader so it ships in builds without relying on built-in skybox shaders.
Shader "SheltreSim/WeatherSky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.3,0.5,0.8,1)
        _Horizon ("Horizon", Color) = (0.7,0.8,0.9,1)
        _Ground ("Ground", Color) = (0.3,0.3,0.3,1)
        _SunDir ("Sun direction", Vector) = (0,1,0,0)
        _SunColor ("Sun colour", Color) = (1,0.9,0.7,1)
        _MoonDir ("Moon direction", Vector) = (0,-1,0,0)
        _Params ("x sun disc, y moon disc, z stars, w glow", Vector) = (1,0,0,0)
        _Flash ("Lightning", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Zenith, _Horizon, _Ground, _SunColor;
            float4 _SunDir, _MoonDir, _Params;
            float _Flash;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float up = pow(saturate(h), 0.5);
                float3 col = lerp(_Horizon.rgb, _Zenith.rgb, up);
                float below = saturate(-h * 4.0);
                col = lerp(col, _Ground.rgb, smoothstep(0, 1, below));

                float sd = max(0, dot(d, _SunDir.xyz));
                // Wide warm glow around the sun, strongest near the horizon (dawn and dusk).
                float horizonBias = 1.0 - saturate(abs(h) * 1.5);
                col += _SunColor.rgb * (pow(sd, 6.0) * 0.45 + pow(sd, 40.0) * 0.35) * _Params.w * (0.35 + 0.65 * horizonBias);
                // Sun disc.
                float disc = smoothstep(0.9994, 0.9998, sd);
                col = lerp(col, _SunColor.rgb * 2.2, disc * _Params.x * step(-0.02, h));

                float md = dot(d, _MoonDir.xyz);
                float moon = smoothstep(0.9993, 0.9996, md);
                col = lerp(col, float3(0.85, 0.9, 1.0), moon * _Params.y * step(0.0, h));
                col += float3(0.5, 0.6, 0.8) * pow(max(0, md), 60.0) * 0.12 * _Params.y;

                // Stars: jittered round dots on an angular grid.
                float lat = asin(clamp(d.y, -1.0, 1.0));
                float2 sp = float2(atan2(d.z, d.x) * 0.15915 * 900.0 * max(cos(lat), 0.15), lat * 0.63662 * 260.0);
                float2 cell = floor(sp);
                float2 jit = float2(hash13(float3(cell, 1.7)), hash13(float3(cell, 9.1)));
                float present = step(0.955, hash13(float3(cell, 4.2)));
                float star = present * smoothstep(0.22, 0.04, length(frac(sp) - (0.25 + 0.5 * jit)));
                float tw = 0.5 + 0.5 * hash13(float3(cell, 7.7));
                col += star * tw * _Params.z * saturate(h * 4.0);

                col += float3(0.75, 0.82, 1.0) * _Flash * (0.35 + 0.65 * saturate(h + 0.3));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
