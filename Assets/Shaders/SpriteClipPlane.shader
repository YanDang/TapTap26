// 精灵 + 世界空间平面裁剪：只保留 dot(_ClipPlane.xyz, 世界坐标) + _ClipPlane.w >= 0 的部分。
// 用于把一张图层运行时切成两半分别动画（眨眼时只合拢上睫毛，嘴巴不动）。平面由脚本每帧通过 MaterialPropertyBlock 设置，
// 用世界坐标是因为精灵合批时顶点已被变换到世界空间。
Shader "GameJam/SpriteClipPlane"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _ClipPlane ("Clip Plane (xyz 法线, w 偏移)", Vector) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float4 _ClipPlane;

            struct v2fc
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2fc vert(appdata_t IN)
            {
                v2fc o;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(IN.vertex);
                o.wpos = mul(unity_ObjectToWorld, IN.vertex).xyz;
                o.texcoord = IN.texcoord;
                o.color = IN.color * _Color * _RendererColor;
                return o;
            }

            fixed4 frag(v2fc i) : SV_Target
            {
                clip(dot(_ClipPlane.xyz, i.wpos) + _ClipPlane.w);
                fixed4 c = SampleSpriteTexture(i.texcoord) * i.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
