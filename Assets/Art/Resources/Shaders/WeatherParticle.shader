// Rain streaks and snow flakes. Procedural shape, no textures; own shader so it is never stripped.
Shader "SheltreSim/WeatherParticle"
{
    Properties
    {
        _Color ("Colour", Color) = (1,1,1,1)
        _Shape ("0 streak, 1 flake", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Shape;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 c = i.uv * 2.0 - 1.0;
                float shape;
                if (_Shape > 0.5)
                {
                    shape = saturate(1.0 - dot(c, c));
                    shape = shape * shape;
                }
                else
                {
                    float across = saturate(1.0 - abs(c.x));
                    float along = saturate(1.0 - abs(c.y));
                    shape = pow(across, 1.2) * pow(along, 0.6);
                }
                return fixed4(i.color.rgb, i.color.a * shape);
            }
            ENDCG
        }
    }
}
