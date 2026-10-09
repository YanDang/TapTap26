Shader "GameJam/ScreenFX"
{
    // "通透世界": desaturate + darken + cold vignette, plus an expanding pulse ring when slow-mo starts.
    Properties
    {
        _MainTex ("Screen", 2D) = "white" {}
        _Amount ("Amount", Range(0,1)) = 0
        _Pulse ("Pulse Radius", Float) = 2
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Amount, _Pulse;
            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float2 d = i.uv - 0.5;
                d.x *= _ScreenParams.x / _ScreenParams.y;
                float r = length(d);
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                float3 g = lerp(c.rgb, lum * float3(0.82, 0.95, 1.12), 0.7 * _Amount);   // drain colour, cool tint
                g *= lerp(1.0, 0.8, _Amount);
                float vig = smoothstep(0.32, 0.95, r);
                g = lerp(g, g * 0.18 + float3(0.0, 0.015, 0.05), vig * _Amount);            // dark cold border
                float ring = exp(-pow((r - _Pulse) / 0.035, 2.0)) * saturate(1.2 - _Pulse);
                g += ring * float3(0.55, 0.85, 1.0) * 0.5;
                return fixed4(g, 1);
            }
            ENDCG
        }
    }
}
