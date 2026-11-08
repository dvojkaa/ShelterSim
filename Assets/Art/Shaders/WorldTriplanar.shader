// World-space tiling for big stretched level geometry: a wall or ground cube can be any size
// and its texture stays at true metres, with no per-object materials or UVs.
// Not for moving objects, because the texture would swim as they move.
Shader "SheltreSim/WorldTriplanar"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Tiling texture", 2D) = "white" {}
        _TileMeters ("World metres per tile", Float) = 4
        _Macro ("Large-scale variation (hides repetition)", Range(0,1)) = 0
        _DepthBias ("Depth bias (negative pulls toward camera)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        // Lets a floor that is exactly coplanar with the ground win without moving its collider.
        Offset [_DepthBias], [_DepthBias]

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows nometa
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _TileMeters;
        float _Macro;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 weights = pow(abs(IN.worldNormal), 4.0);
            weights /= (weights.x + weights.y + weights.z);
            float3 p = IN.worldPos / _TileMeters;

            fixed3 albedo = tex2D(_MainTex, p.zy).rgb * weights.x
                          + tex2D(_MainTex, p.xz).rgb * weights.y
                          + tex2D(_MainTex, p.xy).rgb * weights.z;

            if (_Macro > 0.0)
            {
                // Blurred mips at unrelated large scales: broad tonal drift that breaks up visible
                // tiling. Sharp mips here would blow fine cracks up into giant dark veins.
                const fixed mean = 0.355;
                fixed a = dot(tex2Dlod(_MainTex, float4(p.xz * 0.137 + 0.31, 0, 5)).rgb, 0.333);
                fixed b = dot(tex2Dlod(_MainTex, float4(p.zx * 0.053 + 0.71, 0, 6)).rgb, 0.333);
                fixed drift = 1.0 + ((a - mean) * 5.0 + (b - mean) * 7.0);
                albedo *= lerp(1.0, clamp(drift, 0.8, 1.2), _Macro);
            }

            o.Albedo = albedo * _Color.rgb;
            o.Metallic = 0.0;
            o.Smoothness = 0.0;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
