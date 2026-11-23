Shader "Portrait/Ethan Skin"
{
 Properties
 {
  _BaseMap("Albedo",2D)="white"{}
  _BaseColor("Tint",Color)=(1,1,1,1)
  [Normal]_BumpMap("Normal",2D)="bump"{}
  _BumpScale("Normal strength",Range(0,1))=0.65
  _SurfaceMask("Smoothness (R), Thickness (G)",2D)="white"{}
  _Smoothness("Smoothness",Range(0,1))=0.45
  _Wrap("Diffuse scattering",Range(0,0.4))=0.12
  _Transmission("Transmission",Range(0,0.3))=0.06
  _ScatterColor("Scatter tint",Color)=(1,0.28,0.18,1)
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  CBUFFER_START(UnityPerMaterial)
  float4 _BaseMap_ST;
  half4 _BaseColor, _ScatterColor;
  half _BumpScale, _Smoothness, _Wrap, _Transmission;
  CBUFFER_END
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
  TEXTURE2D(_SurfaceMask); SAMPLER(sampler_SurfaceMask);
  struct A {float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0;};
  struct V {float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3;};
  V Vert(A a){V o; VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.tangentWS=half4(TransformObjectToWorldDir(a.tangentOS.xyz),a.tangentOS.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(a.uv,_BaseMap);return o;}
  half3 Normal(V i){half3 n=normalize(i.normalWS);half3 t=normalize(i.tangentWS.xyz);half3 b=cross(n,t)*i.tangentWS.w;return normalize(TransformTangentToWorld(UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale),half3x3(t,b,n)));}
  half3 Scatter(Light l,half3 n,half3 albedo,half thickness){half ndl=dot(n,l.direction);half wrapped=saturate((ndl+_Wrap)/(1+_Wrap));half extra=max(0,wrapped-saturate(ndl));return albedo*_ScatterColor.rgb*(extra+thickness*_Transmission*pow(saturate(-ndl),2))*l.color*l.distanceAttenuation*l.shadowAttenuation;}
  ENDHLSL
  Pass
  {
   Name "Forward" Tags{"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #pragma multi_compile_fog
   half4 Frag(V i):SV_Target
   {
    half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
    half2 mask=SAMPLE_TEXTURE2D(_SurfaceMask,sampler_SurfaceMask,i.uv).rg;
    InputData d=(InputData)0;d.positionWS=i.positionWS;d.normalWS=Normal(i);d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);d.shadowCoord=TransformWorldToShadowCoord(i.positionWS);d.bakedGI=SampleSH(d.normalWS);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);d.shadowMask=half4(1,1,1,1);
    SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.metallic=0;s.specular=half3(.04,.04,.04);s.smoothness=saturate(_Smoothness*lerp(.6,1.35,mask.r));s.normalTS=half3(0,0,1);s.occlusion=1;s.alpha=1;
    half4 c=UniversalFragmentPBR(d,s);c.rgb+=Scatter(GetMainLight(d.shadowCoord),d.normalWS,albedo,mask.g);
    #ifdef _ADDITIONAL_LIGHTS
    uint count=GetAdditionalLightsCount();for(uint j=0;j<count;j++)c.rgb+=Scatter(GetAdditionalLight(j,i.positionWS,half4(1,1,1,1)),d.normalWS,albedo,mask.g);
    #endif
    c.rgb=MixFog(c.rgb,ComputeFogFactor(i.positionCS.z));return c;
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"}
   ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma vertex ShadowVert
   #pragma fragment DepthFrag
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   float3 _LightDirection, _LightPosition;
   V ShadowVert(A a){V o=Vert(a);float3 lightDir=_LightDirection;
   #ifdef _CASTING_PUNCTUAL_LIGHT_SHADOW
   lightDir=normalize(_LightPosition-o.positionWS);
   #endif
   o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,lightDir));
   #if UNITY_REVERSED_Z
   o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
   #else
   o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
   #endif
   return o;}
   half4 DepthFrag(V i):SV_Target{return 0;}
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly" Tags{"LightMode"="DepthOnly"} ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment DepthFrag
   half4 DepthFrag(V i):SV_Target{return i.positionCS.z;}
   ENDHLSL
  }
  Pass
  {
   Name "DepthNormals" Tags{"LightMode"="DepthNormals"} ZWrite On
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment NormFrag
   #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
   half4 NormFrag(V i):SV_Target{float3 n=Normal(i);
   #ifdef _GBUFFER_NORMALS_OCT
   return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
   #else
   return half4(n,0);
   #endif
   }
   ENDHLSL
  }
 }
}
