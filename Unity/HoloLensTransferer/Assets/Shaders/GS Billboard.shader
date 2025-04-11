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
            #pragma multi_compile_instancing // Required for SPI
            #pragma require geometry
            #pragma vertex VS_Main
            #pragma geometry GS_Main
            #pragma fragment FS_Main
            #include "UnityCG.cginc"

            struct GS_INPUT
            {
                float4 pos : POSITION;
                float4 col : COLOR;
                uint instanceID : SV_InstanceID;
            };

            struct FS_INPUT
            {
                float4 pos : SV_POSITION;
                float4 col : COLOR;
            };

            float _PointSize;

            // Vertex Shader
            GS_INPUT VS_Main(appdata_full v, uint instanceID : SV_InstanceID)
            {
                GS_INPUT output;
                output.pos = mul(unity_ObjectToWorld, v.vertex); // Convert to world space
                output.col = v.color;
                output.instanceID = instanceID; // Store instance ID
                return output;
            }

            // Geometry Shader
            [maxvertexcount(4)]
            void GS_Main(point GS_INPUT p[1], inout TriangleStream<FS_INPUT> triStream)
            {
                // Extract per-eye camera position from UNITY_MATRIX_I_V
                float3 camPos = float3(UNITY_MATRIX_I_V._m03, UNITY_MATRIX_I_V._m13, UNITY_MATRIX_I_V._m23);

                // Billboard alignment
                float3 up = float3(0, 1, 0);
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
                    pOut.col = p[0].col;
                    triStream.Append(pOut);
                }

                triStream.RestartStrip();
            }

            // Fragment Shader
            float4 FS_Main(FS_INPUT input) : SV_Target
            {
                return input.col;
            }

            ENDCG
        }
    }
}