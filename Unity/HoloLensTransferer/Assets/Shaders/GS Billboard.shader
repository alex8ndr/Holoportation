Shader "Custom/GS Billboard"
{
    Properties
    {
        _PointSize("PointSize", Range(0, 0.1)) = 0.01
    }

    SubShader
    {
        Pass
        {
            Tags { "RenderType"="Opaque" }
            LOD 200

            CGPROGRAM
            #pragma target 5.0
            #pragma multi_compile_instancing
            #pragma vertex VS_Main
            #pragma fragment FS_Main
            #include "UnityCG.cginc"

            struct VS_INPUT
            {
                float4 pos : POSITION;
                float4 col : COLOR;
            };

            struct VS_OUTPUT
            {
                float4 pos : SV_POSITION;
                float4 col : COLOR;
            };

            float _PointSize;

            VS_OUTPUT VS_Main(VS_INPUT v, uint instanceID : SV_InstanceID)
            {
                VS_OUTPUT o;

                // Use Unity's precomputed camera position
                float3 camPos = _WorldSpaceCameraPos;

                // Billboard alignment
                float3 up = float3(0, 1, 0);
                float3 look = normalize(v.pos.xyz - camPos);
                float3 right = normalize(cross(up, look)); // Use normalize()

                // Precomputed quad offsets
                static const float2 offsets[4] = { float2(-1,-1), float2(1,-1), float2(-1,1), float2(1,1) };

                // Expand point to quad
                float halfSize = 0.5 * _PointSize;
                float3 cornerPos = v.pos.xyz + offsets[instanceID].x * halfSize * right + offsets[instanceID].y * halfSize * up;
    
                // Transform to clip space
                o.pos = UnityWorldToClipPos(float4(cornerPos, 1.0));
                o.col = v.col;
    
                return o;
            }


            float4 FS_Main(VS_OUTPUT i) : SV_Target
            {
                return i.col;
            }

            ENDCG
        }
    }
}