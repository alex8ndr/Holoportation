Shader "Custom/ComputeBufferPointCloud"
{
    Properties
    {
        _PointSize("Point Size", Range(0, 0.1)) = 0.01
    }

    SubShader
    {
        Pass
        {
            Tags { "RenderType"="Opaque" }
            LOD 200

            CGPROGRAM
            #pragma target 5.0
            #pragma require geometry
            #pragma vertex VS_Main
            #pragma geometry GS_Main
            #pragma fragment FS_Main
            #include "UnityCG.cginc"

            struct GS_INPUT
            {
                float4 pos : POSITION;
                float3 col : COLOR;  // Store color as Vector3
            };

            struct FS_INPUT
            {
                float4 pos : SV_POSITION;
                float3 col : COLOR;  // Store color as Vector3
            };

            StructuredBuffer<float3> _PointCloudBuffer; // Now using Vector3 for position
            StructuredBuffer<float3> _ColorBuffer; // Now using Vector3 for color
            int _NumPoints;
            float _PointSize;
            float3 _CameraPosition;
            float4x4 _CameraRotation;

            // Vertex Shader
            GS_INPUT VS_Main(uint id : SV_VertexID)
            {
                GS_INPUT output;

                // Make sure we don't access out-of-bounds memory
                if (id >= _NumPoints)
                {
                    output.pos = float4(0, 0, 0, 0);
                    output.col = float3(0, 0, 0); // No color if out of bounds
                    return output;
                }

                // Get position and color from the buffers
                float3 pos = _PointCloudBuffer[id];
                float3 col = _ColorBuffer[id];

                output.pos = float4(pos, 1.0);  // Position as float4
                output.col = col;  // Use color from the buffer

                return output;
            }

            // Geometry Shader (Billboarding)
            [maxvertexcount(4)]
            void GS_Main(point GS_INPUT p[1], inout TriangleStream<FS_INPUT> triStream)
            {
                if (_NumPoints == 0)
                    return;

                float3 camPos = _CameraPosition;

                // Extract correct billboard orientation
                float3 up = normalize(_CameraRotation._m10_m11_m12);
                float3 look = normalize(camPos - p[0].pos.xyz);
                float3 right = normalize(cross(up, look));
                up = cross(look, right);

                float halfS = 0.5f * _PointSize;

                // Quad corners
                float3 v[4];
                v[0] = p[0].pos.xyz + halfS * right - halfS * up;
                v[1] = p[0].pos.xyz + halfS * right + halfS * up;
                v[2] = p[0].pos.xyz - halfS * right - halfS * up;
                v[3] = p[0].pos.xyz - halfS * right + halfS * up;

                FS_INPUT pOut;
                for (int i = 0; i < 4; i++)
                {
                    pOut.pos = UnityWorldToClipPos(float4(v[i], 1.0));
                    pOut.col = p[0].col; // Pass color
                    triStream.Append(pOut);
                }

                triStream.RestartStrip();
            }

            // Fragment Shader
            float4 FS_Main(FS_INPUT input) : SV_Target
            {
                return float4(input.col, 1.0); // Set color
            }

            ENDCG
        }
    }
}