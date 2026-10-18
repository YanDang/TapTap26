// Boss 预警贴地柔光圆：加法混合，边缘 45% 起渐隐；不写深度、不挡原画。
Shader "GameJam/BossAuraGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.8, 0.1, 0.5)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend One One
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv);
                float body = pow(saturate(1.0 - r), 2.1);          // 中心亮、边缘软
                float lip  = smoothstep(0.88, 0.96, r) * (1.0 - smoothstep(0.96, 1.0, r));
                float a = (body * 0.9 + lip * 0.35) * _Color.a;
                return fixed4(_Color.rgb * a, 1.0);
            }
            ENDCG
        }
    }
}
