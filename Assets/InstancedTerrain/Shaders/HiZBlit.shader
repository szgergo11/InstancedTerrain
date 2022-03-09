Shader "Unlit/HiZBufferShader"
{

    Properties
    {
        _MainTex("Base (RGB)", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "HiZBlit_incl.hlsl"
        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertex
            #pragma fragment blit
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertex
            #pragma fragment reduce
            ENDHLSL
        }
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "HiZBlit_incl.hlsl"
        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex vertex
            #pragma fragment blit
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex vertex
            #pragma fragment reduce
            ENDHLSL
        }
    }
}