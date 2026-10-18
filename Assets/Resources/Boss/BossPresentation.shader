Shader "Tap/BossPresentation" {
Properties { _MainTex("Texture",2D)="white" {} }
SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass { HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A {float4 p:POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};
struct V {float4 p:SV_POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};
TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.c=a.c;o.uv=a.uv;return o;}
half4 frag(V i):SV_Target{return i.c*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);}
ENDHLSL } } }
