Shader "Hidden/WorldSpaceClouds"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        
        Pass
        {
            Name "WorldSpaceClouds"
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 3.5
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            TEXTURE3D(_CloudTex1);
            SAMPLER(sampler_CloudTex1);
            TEXTURE3D(_CloudTex2);
            SAMPLER(sampler_CloudTex2);
            TEXTURE3D(_CloudTex3);
            SAMPLER(sampler_CloudTex3);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _CloudColor;
                float4 _CloudShadowColor;
                float4 _CloudHighlightColor;
                float _CloudScale;
                float4 _CloudSpeed;
                float _CloudHeight;
                float _CloudThickness;
                float _DensityMultiplier;
                int _StepCount;
                float _LightAbsorption;
                float _DetailScale;
                float _DetailStrength;
                float3 _SunDirection;
                float _AmbientLight;
                float _EdgeSoftness;
                float _Puffiness;
            CBUFFER_END
            
            // Smooth min function for blending
            float smin(float a, float b, float k)
            {
                float h = max(k - abs(a - b), 0.0) / k;
                return min(a, b) - h * h * k * 0.25;
            }
            
            float SampleCloudDensity(float3 worldPos, float time)
            {
                // Base world space UVW - use ALL three dimensions equally
                float3 uvw = worldPos * _CloudScale * 0.0005;
                
                // Animated wind
                uvw.xz += _CloudSpeed.xz * time;
                uvw.y += _CloudSpeed.y * time * 0.5;
                
                // MULTI-SCALE CLOUD SYSTEM - Create different sizes of clouds
                // BIGGER CLOUDS: Reduced frequencies for larger formations
                
                // === VERY LARGE CLOUDS === (Low frequency) - INCREASED SIZE
                float3 largeUvw = uvw * 0.15; // Was 0.3, now 0.15 = 2x bigger
                largeUvw.xz += float2(sin(largeUvw.z * 0.3), cos(largeUvw.x * 0.3)) * 0.4;
                float largeClouds = SAMPLE_TEXTURE3D(_CloudTex1, sampler_CloudTex1, largeUvw).r;
                largeClouds = smoothstep(0.35, 0.75, largeClouds); // Wider range = bigger clouds
                
                // === MEDIUM CLOUDS === (Main detail level) - INCREASED SIZE
                float angle1 = uvw.x * 0.4 + uvw.z * 0.25; // Reduced frequency
                float angle2 = uvw.x * 0.5 - uvw.z * 0.3;  // Reduced frequency
                
                float3 uvw1 = float3(
                    uvw.x + sin(angle1) * 0.3,
                    uvw.y,
                    uvw.z + cos(angle1) * 0.3
                );
                
                float3 uvw2 = float3(
                    uvw.x + cos(angle2) * 0.4 + uvw.y * 0.3,
                    uvw.y,
                    uvw.z + sin(angle2) * 0.4 + uvw.y * 0.2
                ) * 1.3 + float3(100.3, 50.2, 200.7); // Was 1.7, now 1.3 = bigger
                
                float cloud1 = SAMPLE_TEXTURE3D(_CloudTex1, sampler_CloudTex1, uvw1).r;
                float cloud2 = SAMPLE_TEXTURE3D(_CloudTex2, sampler_CloudTex2, uvw2).r;
                
                float mediumClouds = cloud1 * 0.7 + cloud2 * 0.3;
                mediumClouds = smoothstep(0.4, 0.75, mediumClouds); // Wider range
                
                // === SMALL CLOUDS === (High frequency details) - INCREASED SIZE
                float3 smallUvw = uvw * 1.5; // Was 2.5, now 1.5 = bigger
                smallUvw.xz += float2(sin(smallUvw.x * 1.2), cos(smallUvw.z * 1.0)) * 0.25;
                float smallClouds = SAMPLE_TEXTURE3D(_CloudTex3, sampler_CloudTex3, smallUvw).r;
                smallClouds = smoothstep(0.5, 0.8, smallClouds); // Better threshold
                
                // COMBINE SCALES - Blend different cloud sizes
                float baseDensity = largeClouds * 0.5;
                baseDensity += mediumClouds * 0.35;
                baseDensity += smallClouds * 0.15;
                baseDensity = saturate(baseDensity);
                
                // COVERAGE/SPACING - Larger scale for bigger cloud formations
                float3 gapUvw = worldPos * _CloudScale * 0.00005; // Was 0.0001, now 0.00005 = bigger gaps/clouds
                float gapNoise = SAMPLE_TEXTURE3D(_CloudTex2, sampler_CloudTex2, gapUvw).r;
                
                float coverage = smoothstep(0.2, 0.8, gapNoise); // Wider range = bigger formations
                baseDensity *= coverage;
                
                // Early exit if no cloud
                if (baseDensity < 0.05) return 0.0;
                
                // Apply threshold to create defined shapes
                baseDensity = smoothstep(0.3, 0.7, baseDensity);
                
                // Wispy lines - scaled for bigger clouds
                float3 wispyUvw = uvw * _DetailScale * 2.5; // Was 4.0, now 2.5 for bigger wisps
                wispyUvw.x += sin(uvw.z * 0.8) * 0.4 + uvw.y * 0.15; // Reduced frequency
                wispyUvw.z += cos(uvw.x * 0.6) * 0.3; // Reduced frequency
                wispyUvw.xz *= 2.0;
                wispyUvw.y *= 0.3;
                float wispy = SAMPLE_TEXTURE3D(_CloudTex2, sampler_CloudTex2, wispyUvw).r;
                
                float3 wispyUvw2 = uvw * _DetailScale * 4.0; // Was 6.0, now 4.0
                wispyUvw2.z += sin(uvw.x * 1.0) * 0.25 + uvw.y * 0.12; // Reduced frequency
                wispyUvw2.x += cos(uvw.z * 0.8) * 0.22; // Reduced frequency
                wispyUvw2.xz *= float2(1.5, 2.5);
                wispyUvw2.y *= 0.25;
                float wispy2 = SAMPLE_TEXTURE3D(_CloudTex3, sampler_CloudTex3, wispyUvw2).r;
                
                float wispyLines = wispy * 0.6 + wispy2 * 0.4;
                baseDensity = lerp(baseDensity, baseDensity * wispyLines, 0.45); // Slightly reduced influence
                
                // Detail erosion
                float3 detailUvw1 = float3(
                    uvw.x + sin(uvw.y * 2.0) * 0.1, 
                    uvw.z + uvw.y * 0.1, 
                    uvw.y
                ) * _DetailScale;
                
                float3 detailUvw2 = float3(
                    uvw.z + cos(uvw.y * 1.8) * 0.12, 
                    uvw.y, 
                    uvw.x + uvw.y * 0.15
                ) * _DetailScale * 2.5 + float3(50.0, 25.0, 75.0);
                
                float detail1 = SAMPLE_TEXTURE3D(_CloudTex1, sampler_CloudTex1, detailUvw1).r;
                float detail2 = SAMPLE_TEXTURE3D(_CloudTex2, sampler_CloudTex2, detailUvw2).r;
                float detail = detail1 * 0.7 + detail2 * 0.3;
                
                baseDensity = saturate(baseDensity - (1.0 - detail) * _DetailStrength * 1.5);
                baseDensity = smoothstep(0.2, 0.8, baseDensity);
                baseDensity = pow(saturate(baseDensity), 2.0 - _Puffiness * 1.0);
                
                // Height falloff - ASYMMETRIC: flat bottom, puffy rounded top
                // Use thickness-relative sampling to maintain smooth gradients
                float thicknessScale = _CloudThickness * 0.00001;
                
                float3 heightNoiseUvw = worldPos * thicknessScale * 0.3;
                heightNoiseUvw.xz += float2(sin(heightNoiseUvw.z * 0.5), cos(heightNoiseUvw.x * 0.5)) * 0.3;
                float heightNoise = SAMPLE_TEXTURE3D(_CloudTex3, sampler_CloudTex3, heightNoiseUvw).r;
                
                float3 heightNoise2Uvw = worldPos * thicknessScale * 0.8 + float3(200, 100, 300);
                float heightNoise2 = SAMPLE_TEXTURE3D(_CloudTex2, sampler_CloudTex2, heightNoise2Uvw).r;
                
                float3 heightNoise3Uvw = worldPos * thicknessScale * 1.5 + float3(500, 250, 750);
                float heightNoise3 = SAMPLE_TEXTURE3D(_CloudTex1, sampler_CloudTex1, heightNoise3Uvw).r;
                
                // Height offset for overall cloud layer position
                float heightOffset = (heightNoise - 0.5) * _CloudThickness * 0.8;
                heightOffset += (heightNoise2 - 0.5) * _CloudThickness * 0.4;
                heightOffset += (heightNoise3 - 0.5) * _CloudThickness * 0.2;
                
                float effectiveHeight = _CloudHeight + heightOffset;
                
                // Calculate distance from center
                float yDist = worldPos.y - effectiveHeight;
                
                // ASYMMETRIC FALLOFF - Different curves for top and bottom
                float heightFactor;
                
                if (yDist > 0) 
                {
                    // ABOVE CENTER - Puffy rounded top (sphere-like)
                    float topDist = yDist / (_CloudThickness * 0.5);
                    heightFactor = 1.0 - saturate(topDist);
                    
                    // Rounded puffy top with multiple smooth steps
                    heightFactor = smoothstep(0.0, 1.0, heightFactor);
                    heightFactor = pow(heightFactor, 0.5 + _EdgeSoftness * 2.0); // Softer curve
                    heightFactor = smoothstep(0.0, 1.0, heightFactor);
                    
                    // Add puffiness variation to top
                    float3 puffUvw = worldPos * thicknessScale * 2.0;
                    float puffNoise = SAMPLE_TEXTURE3D(_CloudTex2, sampler_CloudTex2, puffUvw).r;
                    heightFactor *= lerp(0.7, 1.0, puffNoise);
                }
                else 
                {
                    // BELOW CENTER - Flat bottom with slight softness
                    float bottomDist = abs(yDist) / (_CloudThickness * 0.5);
                    heightFactor = 1.0 - saturate(bottomDist);
                    
                    // Flatter bottom - harder falloff
                    heightFactor = smoothstep(0.0, 0.4, heightFactor); // Narrow range = flatter
                    heightFactor = pow(heightFactor, 1.5 + _EdgeSoftness); // Harder curve
                    
                    // Very subtle variation on bottom to avoid perfectly flat
                    float3 flatUvw = worldPos * thicknessScale * 3.0;
                    float flatNoise = SAMPLE_TEXTURE3D(_CloudTex3, sampler_CloudTex3, flatUvw).r;
                    heightFactor *= lerp(0.85, 1.0, flatNoise); // Less variation
                }
                
                // Add overall turbulence (affects both top and bottom)
                float3 vertTurbUvw = worldPos * thicknessScale * 1.8;
                vertTurbUvw = float3(vertTurbUvw.y, vertTurbUvw.z, vertTurbUvw.x);
                float vertTurb = SAMPLE_TEXTURE3D(_CloudTex1, sampler_CloudTex1, vertTurbUvw).r;
                
                heightFactor *= lerp(0.6, 1.0, vertTurb);
                
                // Final smoothing
                heightFactor = smoothstep(0.0, 1.0, heightFactor);
                heightFactor = smoothstep(0.0, 1.0, heightFactor);
                
                return baseDensity * heightFactor * _DensityMultiplier;
            }
            
            float3 CalculateLighting(float density, float3 worldPos, float3 viewDir)
            {
                float3 sunDir = normalize(_SunDirection);
                
                // Sun lighting
                float sunDot = dot(float3(0, 1, 0), sunDir);
                float lightEnergy = saturate(sunDot) * 0.7 + _AmbientLight;
                
                // Enhanced depth-based shading
                // Sample density in the light direction for self-shadowing
                float3 lightSamplePos = worldPos + sunDir * 10.0;
                float lightDensity = SampleCloudDensity(lightSamplePos, _Time.y);
                
                // Calculate shadow based on accumulated density
                float shadow = exp(-density * _LightAbsorption);
                float occlusion = exp(-lightDensity * _LightAbsorption * 0.5);
                
                // Combine shadow and occlusion
                float totalShadow = shadow * occlusion;
                
                // View-dependent lighting (silver lining effect)
                float viewDot = dot(viewDir, sunDir);
                float rimLight = pow(saturate(viewDot), 4.0) * 0.3;
                
                // Height-based lighting (top of clouds brighter)
                float heightGradient = saturate((worldPos.y - _CloudHeight + _CloudThickness * 0.3) / (_CloudThickness * 0.6));
                
                // Mix colors based on lighting
                // Dark shadow areas
                float3 shadowColor = _CloudShadowColor.rgb * 0.8;
                
                // Mid-tone cloud color
                float3 midColor = _CloudColor.rgb;
                
                // Bright highlights
                float3 highlightColor = _CloudHighlightColor.rgb * 1.2;
                
                // Blend based on shadow amount
                float3 baseColor = lerp(shadowColor, midColor, pow(totalShadow, 0.7));
                baseColor = lerp(baseColor, highlightColor, pow(totalShadow, 3.0) * heightGradient);
                
                // Add rim lighting
                baseColor += rimLight * _CloudHighlightColor.rgb;
                
                // Apply overall light energy
                baseColor *= lightEnergy;
                
                // Add slight color variation based on density
                float densityTint = saturate(density * 2.0);
                baseColor = lerp(baseColor, baseColor * float3(0.95, 0.97, 1.0), densityTint * 0.3);
                
                return baseColor;
            }
            
            float4 RayMarchClouds(float3 rayOrigin, float3 rayDir, float maxDistance)
            {
                float time = _Time.y;
                
                // Bounds scale proportionally with thickness - prevents cutoff at large values
                float boundsMultiplier = 2.0; // 2x thickness on each side
                float cloudBottom = _CloudHeight - _CloudThickness * boundsMultiplier;
                float cloudTop = _CloudHeight + _CloudThickness * boundsMultiplier;
                
                float tBottom = (cloudBottom - rayOrigin.y) / rayDir.y;
                float tTop = (cloudTop - rayOrigin.y) / rayDir.y;
                
                if (tBottom > tTop) {
                    float temp = tBottom;
                    tBottom = tTop;
                    tTop = temp;
                }
                
                if (tTop < 0) return float4(0, 0, 0, 0);
                
                float tStart = max(0, tBottom);
                float tEnd = min(tTop, maxDistance);
                float rayLength = tEnd - tStart;
                
                if (rayLength <= 0) return float4(0, 0, 0, 0);
                
                float stepSize = rayLength / float(_StepCount);
                float3 step = rayDir * stepSize;
                float3 pos = rayOrigin + rayDir * tStart;
                
                float transmittance = 1.0;
                float3 cloudColor = float3(0, 0, 0);
                
                int emptySteps = 0;
                bool foundClouds = false;
                
                [loop]
                for (int i = 0; i < _StepCount; i++)
                {
                    float density = SampleCloudDensity(pos, time);
                    
                    // INCREASED THRESHOLD - prevents black dots from low-density samples
                    if (density > 0.005)
                    {
                        foundClouds = true;
                        emptySteps = 0;
                        
                        float3 lighting = CalculateLighting(density, pos, rayDir);
                        
                        // Enhanced absorption based on density
                        float absorption = density * stepSize * 0.15;
                        float stepTransmittance = exp(-absorption);
                        
                        // Better color accumulation with minimum density filter
                        float weight = transmittance * (1.0 - stepTransmittance);
                        
                        // Only accumulate if weight is significant (prevents dots)
                        if (weight > 0.001)
                        {
                            cloudColor += weight * lighting;
                        }
                        
                        transmittance *= stepTransmittance;
                        
                        if (transmittance < 0.01) break;
                    }
                    else
                    {
                        emptySteps++;
                        if (foundClouds && emptySteps > 15) break;
                    }
                    
                    pos += step;
                }
                
                float alpha = 1.0 - transmittance;
                return float4(cloudColor, alpha);
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                float4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
                
                float depth = SampleSceneDepth(input.texcoord);
                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, depth, UNITY_MATRIX_I_VP);
                
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(worldPos - rayOrigin);
                float maxDistance = length(worldPos - rayOrigin);
                
                float4 cloud = RayMarchClouds(rayOrigin, rayDir, maxDistance);
                
                float3 finalColor = lerp(sceneColor.rgb, cloud.rgb, cloud.a);
                
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}