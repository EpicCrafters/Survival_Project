// URP_GrassCulling.shader
Shader "Custom/URP_GrassCulling"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _CullingThreshold("Culling Threshold", Range(0.0, 1.0)) = 0.5
        
        // Grid properties - MUST match manager settings
        [NoScaleOffset] _GrassGrid("Grass Grid", 2D) = "white" {}
        _GridOrigin("Grid Origin", Vector) = (0, 0, 0, 0)
        _CellSize("Cell Size", Float) = 1.0
        _GridSize("Grid Size", Float) = 512.0
        _GridCenter("Grid Center", Vector) = (0, 0, 0, 0)

        // Add these new properties for transparency control
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, Front, 1, Back, 2)] _Cull("Cull", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 1
        
        // Simple Wind Properties
        [Toggle(_WIND_ON)] _WindToggle("Enable Wind/Jiggle", Float) = 0
        _WindSpeed("Wind Speed", Range(0, 5)) = 1.0
        _WindStrength("Wind Strength", Range(0, 2)) = 0.1
        
        _WindDirection("Wind Direction", Vector) = (1, 0, 0, 0)
        _WindFrequency("Wind Frequency", Range(0.1, 5)) = 1.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            Cull [_Cull]
            ZTest LEqual
            ZWrite On
            Blend [_SrcBlend] [_DstBlend]
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _WIND_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _CullingThreshold;

                float4 _GridOrigin;
                float _CellSize;
                float _GridSize;
                float4 _GridCenter;
                
                // Simple wind properties
                float _WindSpeed;
                float _WindStrength;
                float4 _WindDirection;
                float _WindFrequency;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            TEXTURE2D(_GrassGrid);
            SAMPLER(sampler_GrassGrid);
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            // Function to check if grass should be rendered
            // Now matches the manager's coordinate system
            float ShouldRenderGrass(float3 worldPos)
            {
                // Calculate grid coordinates relative to grid center (like the manager does)
                float2 gridPos;
                gridPos.x = (worldPos.x - _GridCenter.x);
                gridPos.y = (worldPos.z - _GridCenter.z);
                
                // Convert from world space to grid cell coordinates
                // Add half grid size to shift from [-size/2, size/2] to [0, size]
                float gridWorldSize = _GridSize * _CellSize;
                gridPos.x = gridPos.x + (gridWorldSize * 0.5);
                gridPos.y = gridPos.y + (gridWorldSize * 0.5);
                
                // Convert to texture UV (0-1 range)
                float2 gridUV;
                gridUV.x = gridPos.x / gridWorldSize;
                gridUV.y = gridPos.y / gridWorldSize;
                
                // Clamp to avoid sampling outside texture
                gridUV = saturate(gridUV);
                
                // Sample the grid texture (red channel only)
                float grassAllowed = SAMPLE_TEXTURE2D_LOD(_GrassGrid, sampler_GrassGrid, gridUV, 0).r;
                
                // Return 1 if grass is allowed, 0 if not
                return step(_CullingThreshold, grassAllowed);
            }
            
            // Improved wind function for vertical grass (Y-axis up)
            float3 ApplyWind(float3 positionOS, float3 worldPos)
            {
                // Base (Y = 0) = 0 movement, Top (higher Y) = full movement
                // Normalize Y position if grass height is known, otherwise use saturate
                float movementFactor = saturate(positionOS.y); // Assuming grass goes from y=0 to y=1
                
                // Add variation based on world position so nearby grass sways together
                float windTime = _Time.y * _WindSpeed;
                float windNoise = sin(windTime + worldPos.x * _WindFrequency * 0.1 + worldPos.z * _WindFrequency * 0.1);
                
                // Additional high-frequency noise for more natural movement
                float windNoise2 = sin(windTime * 2.3 + worldPos.x * _WindFrequency * 0.05) * 0.3;
                
                // Combine wind patterns
                float windWave = windNoise + windNoise2;
                
                // Apply wind displacement - more at the top, none at the base
                // Wind direction normalized and strength applied
                float3 windDirection = normalize(_WindDirection.xyz);
                float3 windDisplacement = windDirection * windWave * _WindStrength * movementFactor;
                
                // Add slight upward movement at the tip for more natural sway
                windDisplacement.y += windWave * _WindStrength * 0.1 * movementFactor * movementFactor;
                
                return positionOS.xyz + windDisplacement;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                // Transform position to world space for wind calculation
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                
                #ifdef _WIND_ON
                    // Apply wind displacement for vertical grass
                    input.positionOS.xyz = ApplyWind(input.positionOS.xyz, vertexInput.positionWS);
                #endif
                
                // Recalculate vertex inputs after wind displacement
                vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
    
                // Check if grass should be rendered at this position
                float shouldRender = ShouldRenderGrass(input.positionWS);
                clip(shouldRender - 0.5);
    
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                color *= _BaseColor;

                #ifdef _ALPHATEST_ON
                    clip(color.a - _Cutoff);
                #endif

                Light mainLight = GetMainLight();
                float3 normal = normalize(input.normalWS);
                float NdotL = saturate(dot(normal, mainLight.direction));
                color.rgb *= (mainLight.color * NdotL + 0.2);

                return color;
            }
            ENDHLSL
        }
        
        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _WIND_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _CullingThreshold;
                
                // Grid parameters
                float4 _GridOrigin;
                float _CellSize;
                float _GridSize;
                float4 _GridCenter;
                
                // Simple wind properties
                float _WindSpeed;
                float _WindStrength;
                float4 _WindDirection;
                float _WindFrequency;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            TEXTURE2D(_GrassGrid);
            SAMPLER(sampler_GrassGrid);
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            // Same function as forward pass
            float ShouldRenderGrass(float3 worldPos)
            {
                float2 gridPos;
                gridPos.x = (worldPos.x - _GridCenter.x);
                gridPos.y = (worldPos.z - _GridCenter.z);
                
                float gridWorldSize = _GridSize * _CellSize;
                gridPos.x = gridPos.x + (gridWorldSize * 0.5);
                gridPos.y = gridPos.y + (gridWorldSize * 0.5);
                
                float2 gridUV;
                gridUV.x = gridPos.x / gridWorldSize;
                gridUV.y = gridPos.y / gridWorldSize;
                
                gridUV = saturate(gridUV);
                float grassAllowed = SAMPLE_TEXTURE2D_LOD(_GrassGrid, sampler_GrassGrid, gridUV, 0).r;
                
                return step(_CullingThreshold, grassAllowed);
            }
            
            // Wind function for shadow pass (same as forward pass)
            float3 ApplyWind(float3 positionOS, float3 worldPos)
            {
                float movementFactor = saturate(positionOS.y);
                
                float windTime = _Time.y * _WindSpeed;
                float windNoise = sin(windTime + worldPos.x * _WindFrequency * 0.1 + worldPos.z * _WindFrequency * 0.1);
                float windNoise2 = sin(windTime * 2.3 + worldPos.x * _WindFrequency * 0.05) * 0.3;
                
                float windWave = windNoise + windNoise2;
                
                float3 windDirection = normalize(_WindDirection.xyz);
                float3 windDisplacement = windDirection * windWave * _WindStrength * movementFactor;
                
                windDisplacement.y += windWave * _WindStrength * 0.1 * movementFactor * movementFactor;
                
                return positionOS.xyz + windDisplacement;
            }
            
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                // Transform position to world space for wind calculation
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                
                #ifdef _WIND_ON
                    // Apply wind displacement for vertical grass
                    input.positionOS.xyz = ApplyWind(input.positionOS.xyz, vertexInput.positionWS);
                #endif
                
                // Recalculate vertex inputs after wind displacement
                vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                // Apply grid culling in shadow pass too!
                float shouldRender = ShouldRenderGrass(input.positionWS);
                clip(shouldRender - 0.5);
                
                // Alpha test
                #ifdef _ALPHATEST_ON
                    half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    clip(color.a - _Cutoff);
                #endif
                
                return 0;
            }
            
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}