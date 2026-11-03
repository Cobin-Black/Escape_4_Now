Shader "Escape4Now/Arena/SoftParticle"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Softness ("Edge Softness", Range(.25,4)) = 1.8
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _Softness;
            struct input { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct output { float4 position:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            output vert(input v)
            {
                output o; o.position=UnityObjectToClipPos(v.vertex); o.color=v.color*_Color; o.uv=v.uv; return o;
            }
            fixed4 frag(output i):SV_Target
            {
                float2 radialUV=i.uv*2-1;
                float edge=pow(saturate(1-dot(radialUV,radialUV)),_Softness);
                return fixed4(i.color.rgb,i.color.a*edge);
            }
            ENDCG
        }
    }
}
