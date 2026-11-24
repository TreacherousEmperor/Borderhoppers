Shader "World/Outline"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.85, 0.1, 1)
        _Width ("Width (roste se vzdáleností od kamery)", Float) = 0.008
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
        Cull Front
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Width;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f     { float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                float3 wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 wn = UnityObjectToWorldNormal(v.normal);
                float d = distance(_WorldSpaceCameraPos, wpos);
                wpos += wn * _Width * d;                 // tloušťka je zhruba stejná na obrazovce při jakémkoli zoomu
                o.pos = mul(UNITY_MATRIX_VP, float4(wpos, 1.0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return _Color; }
            ENDCG
        }
    }
}
