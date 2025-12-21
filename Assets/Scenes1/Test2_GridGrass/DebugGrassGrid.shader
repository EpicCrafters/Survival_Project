// DebugGrassGrid.shader
Shader "Custom/DebugGrassGrid"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            sampler2D _GrassGrid;
            float4 _GridOrigin;
            float _CellSize;
            float _GridSize;
            
            struct appdata
            {
                float4 vertex : POSITION;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate grid UV
                float2 gridPos;
                gridPos.x = (i.worldPos.x - _GridOrigin.x);
                gridPos.y = (i.worldPos.z - _GridOrigin.z);
                
                float2 gridUV;
                gridUV.x = gridPos.x / (_GridSize * _CellSize);
                gridUV.y = gridPos.y / (_GridSize * _CellSize);
                
                // Clamp and sample
                gridUV = saturate(gridUV);
                float grassAllowed = tex2D(_GrassGrid, gridUV).r;
                
                // Visualize
                if (grassAllowed > 0.5)
                    return float4(0, 1, 0, 1); // Green = grass allowed
                else
                    return float4(1, 0, 0, 1); // Red = no grass
            }
            ENDCG
        }
    }
}