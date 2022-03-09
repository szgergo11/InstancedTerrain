#include "Assets/InstancedTerrain/AdditionalHLSL/MatrixOperations.hlsl"

#ifndef INSTANCEDTERRAIN_INCLUDED
#define INSTANCEDTERRAIN_INCLUDED


StructuredBuffer<float4x4> _TEMatrices;
StructuredBuffer<uint> _CullingResultIdxs;


#if UNITY_ANY_INSTANCING_ENABLED
#define unity_ObjectToWorld unity_ObjectToWorld
#define unity_WorldToObject unity_WorldToObject

void setMatrices(inout float4x4 objectToWorld, inout float4x4 worldToObject)
{
    float4x4 matr = _TEMatrices[_CullingResultIdxs[unity_InstanceID]];

    objectToWorld = mul(objectToWorld, matr);
	// Inverse transform matrix
    worldToObject = inverse(objectToWorld);
}

void setupInstancing()
{
	//#define unity_LODFade unity_LODFade
    
    setMatrices(unity_ObjectToWorld, unity_WorldToObject);
}
#endif

void IncludeDummy_float(in float3 IN, out float3 OUT)
{
#if UNITY_ANY_INSTANCING_ENABLED
    OUT = IN;// +_TEPositions[_CullingResultIdxs[unity_InstanceID]];
#else
    OUT = IN + float3(2, 2, 2);
#endif
}

#endif