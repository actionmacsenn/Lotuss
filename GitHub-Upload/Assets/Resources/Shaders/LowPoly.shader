// Flat-shaded vertex-colour material used for nearly everything in the game.
// Vertex alpha doubles as an "emission" mask: alpha 1 = normally lit, alpha 0 = fully glowing.
Shader "Lotus/LowPoly"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Flash ("Flash", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows addshadow
        #pragma multi_compile_instancing
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
        };

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Flash)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
            fixed4 flash = UNITY_ACCESS_INSTANCED_PROP(Props, _Flash);
            fixed3 c = IN.color.rgb * tint.rgb;
            o.Albedo = c * IN.color.a;
            o.Emission = c * (1 - IN.color.a) * 1.35 + flash.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
