Shader "Custom/GS Billboard HoloLens2"
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
            #pragma vertex VS_Main
            #pragma fragment FS_Main
            #pragma geometry GS_Main
            #include "UnityCG.cginc"

            struct GS_INPUT
            {
                float4 pos : POSITION;
                float4 col : COLOR;
            };

            struct FS_INPUT
            {
                float4 pos : SV_POSITION;
                float4 col : COLOR;
            };

            float _PointSize;

            // Vertex Shader
            GS_INPUT VS_Main(appdata_full v)
            {
                GS_INPUT output;
                output.pos = mul(unity_ObjectToWorld, v.vertex); // Convert to world space
                output.col = v.color;
                return output;
            }

            // Geometry Shader
            [maxvertexcount(4)]
            void GS_Main(point GS_INPUT p[1], inout TriangleStream<FS_INPUT> triStream)
            {
                // Get correct camera position per eye
                float3 camPos = UNITY_MATRIX_V[3].xyz;

                // Billboard alignment
                float3 up = float3(0, 1, 0);
                float3 look = normalize(p[0].pos.xyz - camPos);
                float3 right = normalize(cross(up, look));

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

                triStream.RestartStrip(); // Ensure proper triangle strip handling
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