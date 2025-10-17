Shader "GoodOldBoy/OutlineShader"
{
    Properties
    {
        _Color ("Outline Color", Color) = (1, 1, 0, 1) // 黃色
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass // 這是第一層：把物件變成單色
        {
            Cull Front // 只渲染背面，這樣從正面看就是邊框

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
                // 把頂點沿著法線方向往外推一點點，形成邊框
                v.vertex.xyz += v.normal * _OutlineWidth;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 直接回傳我們設定的顏色
                return _Color;
            }
            ENDCG
        }
        
        Pass // 這是第二層：正常渲染物件本身
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
                // 這裡可以換成更複雜的貼圖渲染，但為了通用性我們先用白色
                return fixed4(1,1,1,1); 
            }
            ENDCG
        }
    }
}