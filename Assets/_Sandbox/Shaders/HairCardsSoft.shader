// Two-pass hair cards for URP (works on WebGL):
//  1. "UniversalForward": the solid core of the hair (alpha >= Core Cutoff), depth-written, alpha-to-coverage.
//  2. "SRPDefaultUnlit": only the soft fringe (Edge Cutoff <= alpha < core), alpha-blended, no depth write,
//     depth test Less so it never re-blends over the core it belongs to.
// Lighting: wrapped Lambert + ambient SH + main/additional lights + shadows, plus a two-lobe Kajiya-Kay
// strand highlight along the card's V axis (root->tip), jittered per strand by the texture's shade.
Shader "Custom/HairCardsSoft"
{
    Properties
    {
        _BaseMap ("Card Texture (RGB shade, A coverage)", 2D) = "white" {}
        _BaseColor ("Hair Color", Color) = (0.1, 0.05, 0.03, 1)
        _Cutoff ("Core Cutoff", Range(0, 1)) = 0.5
        _EdgeCutoff ("Edge Cutoff", Range(0, 1)) = 0.05
        _EdgeSoftness ("Edge Opacity", Range(0, 2)) = 1
        _Wrap ("Light Wrap", Range(0, 1)) = 0.4
        _SheenColor ("Sheen Color", Color) = (0.12, 0.11, 0.1, 1)
        _SheenSoftness ("Sheen Tightness", Range(1, 64)) = 8
        _SpecColor2 ("Secondary Spec Color", Color) = (0.35, 0.22, 0.14, 1)
        _SpecExp2 ("Secondary Tightness", Range(1, 256)) = 24
        _SpecShift ("Primary Shift", Range(-1, 1)) = 0.1
        _SpecShift2 ("Secondary Shift", Range(-1, 1)) = -0.15
        _ShiftJitter ("Per-Strand Shift Jitter", Range(0, 2)) = 0.6
        _StrandAxis ("Strand Axis (+1 V/bitangent, -1 U/tangent)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff, _EdgeCutoff, _EdgeSoftness, _Wrap, _SheenSoftness;
            half4 _SheenColor, _SpecColor2;
            half _SpecExp2, _SpecShift, _SpecShift2, _ShiftJitter, _StrandAxis;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        ENDHLSL

        // shared lit vertex/fragment code
        HLSLINCLUDE
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
        #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
        #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
        #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
        #pragma multi_compile_instancing
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; half3 sh : TEXCOORD3; float3 strandWS : TEXCOORD4; };

        Varyings vert(Attributes v)
        {
            Varyings o; UNITY_SETUP_INSTANCE_ID(v);
            VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
            o.positionCS = p.positionCS; o.positionWS = p.positionWS;
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            float3 t = TransformObjectToWorldDir(v.tangentOS.xyz);
            o.strandWS = _StrandAxis > 0 ? cross(o.normalWS, t) * v.tangentOS.w : t;
            o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
            o.sh = SampleSH(o.normalWS);
            return o;
        }

        float3 gV, gT1, gT2; half3 gAlbedo; half gMask;
        half StrandSpec(float3 T, float3 H, half e)
        {
            half th = dot(T, H);
            return pow(sqrt(saturate(1 - th * th)), e) * smoothstep(-1, 0, th);
        }
        half3 Diffuse(Light l, float3 N)
        {
            half ndl = dot(N, l.direction);
            half wrap = saturate((ndl + _Wrap) / (1 + _Wrap));
            float3 H = normalize(l.direction + gV);
            half vis = saturate(ndl + 0.3);
            half3 spec = _SheenColor.rgb * StrandSpec(gT1, H, _SheenSoftness * 8)
                       + _SpecColor2.rgb * gAlbedo * 4 * StrandSpec(gT2, H, _SpecExp2) * gMask;
            return l.color * l.distanceAttenuation * l.shadowAttenuation * (gAlbedo * wrap + spec * vis);
        }

        half3 Shade(Varyings i, bool frontFace, half3 albedo)
        {
            float3 N = normalize(i.normalWS); // cards carry outward (radial) normals on both sides: no back-face flip
            gV = GetWorldSpaceNormalizeViewDir(i.positionWS); gAlbedo = albedo;
            // per-strand variation from the card texture's shade
            half lum = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb, half3(0.3, 0.59, 0.11));
            half jit = (lum - 0.5) * _ShiftJitter;
            float3 T = normalize(i.strandWS);
            gT1 = normalize(T + N * (_SpecShift + jit));
            gT2 = normalize(T + N * (_SpecShift2 + jit));
            gMask = saturate(lum * 1.6 - 0.2);
            InputData d = (InputData)0;
            d.positionWS = i.positionWS; d.normalWS = N;
            d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
            d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
            half ao = 1;
            #if defined(_SCREEN_SPACE_OCCLUSION)
                ao = SampleAmbientOcclusion(d.normalizedScreenSpaceUV);
            #endif
            half3 light = albedo * i.sh * ao + Diffuse(GetMainLight(d.shadowCoord, i.positionWS, half4(1, 1, 1, 1)), N);
            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    light += Diffuse(GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1)), N);
                #endif
                InputData inputData = d;
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    light += Diffuse(GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1)), N);
                LIGHT_LOOP_END
            #endif
            return light;
        }
        ENDHLSL

        Pass
        {
            Name "Core"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            AlphaToMask On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragCore
            half4 fragCore(Varyings i, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half a = t.a * _BaseColor.a;
                clip(a - _Cutoff);
                return half4(Shade(i, frontFace, t.rgb * _BaseColor.rgb), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SoftEdge"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite Off
            ZTest Less
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragEdge
            half4 fragEdge(Varyings i, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half a = t.a * _BaseColor.a;
                clip(a - _EdgeCutoff);
                clip(_Cutoff - a - 1e-4); // the core is already drawn by pass 1
                return half4(Shade(i, frontFace, t.rgb * _BaseColor.rgb), saturate(a / max(_Cutoff, 1e-3) * _EdgeSoftness));
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vs(A v)
            {
                V o; float3 p = TransformObjectToWorld(v.positionOS.xyz); float3 n = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 L = normalize(_LightPosition - p);
                #else
                    float3 L = _LightDirection;
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(p, n, L)); o.uv = TRANSFORM_TEX(v.uv, _BaseMap); return o;
            }
            half4 fs(V i) : SV_Target { clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vs(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = TRANSFORM_TEX(v.uv, _BaseMap); return o; }
            half4 fs(V i) : SV_Target { clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            HLSLPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; };
            V vs(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = TRANSFORM_TEX(v.uv, _BaseMap); o.n = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 fs(V i, bool ff : SV_IsFrontFace) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return half4(normalize(i.n), 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
