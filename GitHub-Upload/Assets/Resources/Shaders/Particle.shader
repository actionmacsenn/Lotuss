// Simple lit vertex-colour shader for mesh particles (shards, petals, puffs).
// Two-sided lighting; particle colour alpha < 1 blends toward a glowing, unlit look.
Shader "Lotus/Particle"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float3 n : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float ndl = abs(dot(n, _WorldSpaceLightPos0.xyz));
                fixed3 lit = i.color.rgb * (_LightColor0.rgb * ndl + ShadeSH9(float4(0, 1, 0, 1)) * 0.85);
                fixed3 glow = i.color.rgb * 1.35;
                fixed4 c = fixed4(lerp(glow, lit, saturate(i.color.a)), 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
