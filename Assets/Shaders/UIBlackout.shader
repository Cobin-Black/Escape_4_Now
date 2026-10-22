Shader "Escape 4 Now/UI/Blackout"
{
    Properties
    {
        _Color ("Tint", Color) = (0,0,0,1)
        _Center ("Screen Center", Vector) = (0,0,0,0)
        _Radius ("Screen Radius", Float) = 0
        _ClearRatio ("Clear Radius Ratio", Range(0,0.99)) = 0.7
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float4 screen : TEXCOORD0; float4 color : COLOR; };
            float4 _Color, _Center;
            float _Radius, _ClearRatio;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeScreenPos(o.vertex);
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 pixel = i.screen.xy / i.screen.w * _ScreenParams.xy;
                float alpha = _Radius <= 0 ? 1 : smoothstep(_Radius * _ClearRatio, _Radius, distance(pixel, _Center.xy));
                return fixed4(i.color.rgb, i.color.a * alpha);
            }
            ENDCG
        }
    }
}
