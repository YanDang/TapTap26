Shader "TilesHD/ContinuousWorldLight"
{
 Properties { _SoilLight("Baked soil light removal",2D)="gray"{} [PerRendererData] _MainTex("Sprite Texture",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" "RenderPipeline"="UniversalPipeline" }
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   Tags { "LightMode"="Universal2D" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 color:COLOR; };
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   TEXTURE2D(_SoilLight); SAMPLER(sampler_SoilLight);
   CBUFFER_START(UnityPerMaterial)
   float4 _Color;
   CBUFFER_END
   Varyings vert(Attributes v) { Varyings o; float3 w=TransformObjectToWorld(v.positionOS); o.positionCS=TransformWorldToHClip(w); o.world=w.xy; o.uv=v.uv; o.color=v.color*_Color; return o; }
   half4 frag(Varyings i):SV_Target {
    half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;
    // Continuous per-fragment world field: wavelengths ~45 and 67 world units.
    // No floor/frac/cell hash or UV dependency: adjacent cells share exactly one field.
    float light=0.96+0.055*sin(i.world.x*0.14+i.world.y*0.09)+0.025*cos(i.world.y*0.094-i.world.x*0.043);
    float2 lightUV=(i.world-float2(-21,-10))/float2(42,22);
    lightUV.y=1-lightUV.y;
    float correction=SAMPLE_TEXTURE2D(_SoilLight,sampler_SoilLight,lightUV).r*2;
    c.rgb*=light*correction; return c;
   }
   ENDHLSL
  }
 }
}
