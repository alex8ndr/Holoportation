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
            #pragma require geometry
            #pragma vertex VS_Main
            #pragma geometry GS_Main
            #pragma fragment FS_Main
            #include "UnityCG.cginc"

            struct GS_INPUT
            {
                float4 pos : POSITION;
                float4 col : COLOR;
                float3 right : TEXCOORD0;
                float3 up : TEXCOORD1;
            };

            struct FS_INPUT
            {
                float4 pos : SV_POSITION;
                float4 col : COLOR;
            };

            float _PointSize;

            // Vertex Shader (Precompute billboard orientation)
            GS_INPUT VS_Main(appdata_full v)
            {
                GS_INPUT output;
                output.pos = mul(unity_ObjectToWorld, v.vertex); 
                output.col = v.color;

                float3 camPos = _WorldSpaceCameraPos;
                float3 look = normalize(camPos - output.pos.xyz);
                output.right = normalize(cross(float3(0, 1, 0), look));
                output.up = normalize(cross(look, output.right));

                return output;
            }

            // Geometry Shader
            [maxvertexcount(4)]
            void GS_Main(point GS_INPUT p[1], inout TriangleStream<FS_INPUT> triStream)
            {
                float halfS = 0.5f * _PointSize;
                float3 v[4];
                v[0] = p[0].pos.xyz + halfS * p[0].right - halfS * p[0].up;
                v[1] = p[0].pos.xyz + halfS * p[0].right + halfS * p[0].up;
                v[2] = p[0].pos.xyz - halfS * p[0].right - halfS * p[0].up;
                v[3] = p[0].pos.xyz - halfS * p[0].right + halfS * p[0].up;

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