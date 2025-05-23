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

            float _PointSize;

            struct appdata
            {
                float3 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex, 1.0)).xyz;
                float3 right = normalize(UNITY_MATRIX_IT_MV[0].xyz);
                float3 up = normalize(UNITY_MATRIX_IT_MV[1].xyz);

                float3 offset = right * v.uv.x * _PointSize + up * v.uv.y * _PointSize;
                float3 finalPos = worldPos + offset;

                o.pos = UnityWorldToClipPos(finalPos);
                o.color = v.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}