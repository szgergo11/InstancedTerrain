Shader "Unlit/Sphere"
{

	// The properties block of the Unity shader. In this example this block is empty
	// because the output color is predefined in the fragment shader code.
	Properties
	{ }

	// The SubShader block containing the Shader code. 
	SubShader
	{
		// SubShader Tags define when and under which conditions a SubShader block or
		// a pass is executed.
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline" }

		HLSLINCLUDE
			StructuredBuffer<float3> _TEPositions;
			StructuredBuffer<uint> _CullingResultIdxs;
		ENDHLSL

		Pass
		{
			// The HLSL code block. Unity SRP uses the HLSL language.
			HLSLPROGRAM

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile _ _SHADOWS_SOFT

			#pragma multi_compile_fog

			#pragma vertex vert
			#pragma fragment frag

			// The Core.hlsl file contains definitions of frequently used HLSL
			// macros and functions, and also contains #include references to other
			// HLSL files (for example, Common.hlsl, SpaceTransforms.hlsl, etc.).
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


			// The structure definition defines which variables it contains.
			// This example uses the Attributes structure as an input structure in
			// the vertex shader.
			struct Attributes
			{
				// The positionOS variable contains the vertex positions in object
				// space.
				float4 positionOS   : POSITION;
			};

			struct Varyings
			{
				// The positions in this struct must have the SV_POSITION semantic.
				float4 positionHCS  : SV_POSITION;
			};

			// The vertex shader definition with properties defined in the Varyings 
			// structure. The type of the vert function must match the type (struct)
			// that it returns.
			Varyings vert(Attributes IN, uint id : SV_InstanceID)
			{
				// Declaring the output object (OUT) with the Varyings struct.
				Varyings OUT;
				// The TransformObjectToHClip function transforms vertex positions
				// from object space to homogenous space
				OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz + _TEPositions[_CullingResultIdxs[id]]);
				// Returning the output.
				return OUT;
			}

			// The fragment shader definition.            
			half4 frag() : SV_Target
			{
				// Defining the color variable and returning it.
				half4 customColor;
				customColor = half4(1, 1, 1, 1);
				return customColor;
			}
			ENDHLSL
		}
	}
}
