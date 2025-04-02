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
            #pragma require geometry
            #pragma vertex VS_Main
            #pragma geometry GS_Main
            #pragma fragment FS_Main
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
            float3 _CameraPosition;
            float4x4 _CameraRotation;

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
                // Use passed camera position
                float3 camPos = _CameraPosition;

                // Compute correct view-dependent billboard orientation
                float3 up = normalize(_CameraRotation._m10_m11_m12);   // Extract UP vector from rotation matrix
                float3 look = normalize(camPos - p[0].pos.xyz);        // View direction
                float3 right = normalize(cross(up, look));             // Compute right vector
                up = cross(look, right);                               // Recalculate up to ensure orthogonality

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