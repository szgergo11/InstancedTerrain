#include "Assets/InstancedTerrain/AdditionalHLSL/MatrixOperations.hlsl"

#ifndef INSTANCEDTERRAIN_INCLUDED
#define INSTANCEDTERRAIN_INCLUDED


StructuredBuffer<float4x4> _TEMatrices;
StructuredBuffer<uint> _CullingResultIdxs;

#if UNITY_ANY_INSTANCING_ENABLED
void setMatrices(inout float4x4 objectToWorld, inout float4x4 worldToObject)
{
    float4x4 matr = _TEMatrices[_CullingResultIdxs[unity_InstanceID]];

    objectToWorld = mul(objectToWorld, matr);
	// Inverse transform matrix
    worldToObject = inverse(objectToWorld);
}

void setupInstancing()
{
    setMatrices(unity_ObjectToWorld, unity_WorldToObject);
}
#endif

void FNodeDummy_float(in float3 IN, out float3 OUT)
{
    OUT = IN;
}

#endif