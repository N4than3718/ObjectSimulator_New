Shader "GoodOldBoy/OutlineShader"
{
    Properties
    {
        _Color ("Outline Color", Color) = (1, 1, 0, 1) // ����
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass // �o�O�Ĥ@�h�G�⪫���ܦ����
        {
            Cull Front // �u��V�I���A�o�˱q�����ݴN�O���

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            float4 _Color;
            float _OutlineWidth;

            v2f vert (appdata v)
            {
                v2f o;
                // �⳻�I�u�۪k�u��V���~���@�I�I�A�Φ����
                v.vertex.xyz += v.normal * _OutlineWidth;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // �����^�ǧڭ̳]�w���C��
                return _Color;
            }
            ENDCG
        }
        
        Pass // �o�O�ĤG�h�G���`��V���󥻨�
        {
            ZWrite On
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // �o�̥i�H������������K�ϴ�V�A�����F�q�Ωʧڭ̥��Υզ�
                return fixed4(1,1,1,1); 
            }
            ENDCG
        }
    }
}