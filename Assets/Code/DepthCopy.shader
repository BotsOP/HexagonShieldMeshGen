Shader "Hidden/DepthShader"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f {
                float4 pos : SV_POSITION;
                float depth : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.depth = o.pos.z / o.pos.w; // clip space depth
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float d = Linear01Depth(i.depth); // linear [0..1]
                return float4(d, d, d, 1);
            }
            ENDCG
        }
    }
}