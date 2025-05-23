Shader "Custom/InstancedPointCloud"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO

            #include "UnityCG.cginc"

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Colors)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float3 vertex : POSITION;
                float2 uv     : TEXCOORD0;
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

                float3 worldCenter = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;

                float3 right = normalize(UNITY_MATRIX_IT_MV[0].xyz);
                float3 up    = normalize(UNITY_MATRIX_IT_MV[1].xyz);

                float3 offset = right * v.vertex.x + up * v.vertex.y;
                float3 finalPos = worldCenter + offset;

                o.pos = UnityWorldToClipPos(finalPos);
                o.color = UNITY_ACCESS_INSTANCED_PROP(Props, _Colors);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDHLSL
        }
    }
}