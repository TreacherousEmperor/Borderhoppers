Shader "World/VertexColorUnlit"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f     { float4 pos : SV_POSITION; fixed4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 c = i.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    c = GammaToLinearSpace(c);   // barvy píšu v sRGB, projekt je v Linear
                #endif
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
