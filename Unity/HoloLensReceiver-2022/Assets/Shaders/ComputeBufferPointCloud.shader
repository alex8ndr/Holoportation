Shader "Custom/ComputeBufferPointCloud"
{
    Properties
    {
        _PointSize("Point Size", Float) = 0.02
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO

            #include "UnityCG.cginc"

            half _PointSize;

            struct appdata
            {
                float3 vertex : POSITION;
                half3 color : COLOR;         // Drop alpha, use half
                float2 uv : TEXCOORD0; // New: index 0–5
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 GetOffset(float idx)
            {
                if (idx == 0) return float2(-0.5, -0.5);
                if (idx == 1) return float2( 0.5, -0.5);
                if (idx == 2) return float2(-0.5,  0.5);
                if (idx == 3) return float2( 0.5, -0.5);
                if (idx == 4) return float2( 0.5,  0.5);
                return float2(-0.5, 0.5); // case 5
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex, 1.0)).xyz;
                float3 right = normalize(UNITY_MATRIX_IT_MV[0].xyz);
                float3 up = normalize(UNITY_MATRIX_IT_MV[1].xyz);

                float2 baseOffset = GetOffset(v.uv.x);
                float3 offset = right * baseOffset.x * _PointSize + up * baseOffset.y * _PointSize;
                float3 finalPos = worldPos + offset;

                o.pos = UnityWorldToClipPos(finalPos);
                o.color = v.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return half4(i.color, 1.0); // Force opaque alpha
            }
            ENDCG
        }
    }
}