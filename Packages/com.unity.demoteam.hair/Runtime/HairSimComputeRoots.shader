Shader "Hidden/HairSimComputeRoots"
{
	HLSLINCLUDE

	#pragma target 5.0

	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

	#include "HairSimComputeRoots.hlsl"

	// NOTE: WebGPU forbids writable storage in the vertex stage; roots are resolved via compute there (no SupportsVertexUAVWrites)
#if !SHADER_API_WEBGPU
	RWStructuredBuffer<float4> _UpdatedRootPosition : register(u1);
	RWStructuredBuffer<float4> _UpdatedRootFrame : register(u2);
#endif

	struct RootAttribs
	{
		float3 positionOS : POSITION;
		float4 tangentOS : TANGENT;
		float3 normalOS : NORMAL;
	};

	struct RootVaryings
	{
		float4 positionCS : SV_POSITION;
		float pointSize : PSIZE;
	};

	RootVaryings RootVert(RootAttribs attribs, uint strandIndex : SV_VertexID)
	{
#if !SHADER_API_WEBGPU
		RootResolve(
			_UpdatedRootPosition[strandIndex].xyz,
			_UpdatedRootFrame[strandIndex],
			attribs.positionOS,
			attribs.tangentOS,
			attribs.normalOS
		);
#endif

		// clip
		RootVaryings v;
		v.positionCS = float4(0.0, 0.0, 1.0, 0.0);
		v.pointSize = 1.0;
		return v;
	}

	void RootFragDiscard()
	{
		discard;
	}

	ENDHLSL

	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Cull Off
		ZTest Always
		ZWrite Off

		Pass
		{
			HLSLPROGRAM

			#pragma vertex RootVert
			#pragma fragment RootFragDiscard

			ENDHLSL
		}
	}

	Fallback Off
}
