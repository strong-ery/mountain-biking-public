Shader "MilklessCereal/URP/Toon Bush"
{
    Properties
    {
        // Surface Type (hidden but available for debugging)
        [HideInInspector] _Surface("__surface", Float) = 1.0 // 1 = Cutout
        [HideInInspector] _Blend("__blend", Float) = 0.0     // 0 = Alpha
        [HideInInspector] _SrcBlend("__src", Float) = 1.0    // One
        [HideInInspector] _DstBlend("__dst", Float) = 0.0    // Zero
        [HideInInspector] _ZWrite("__zw", Float) = 1.0       // On
        
        // Main Texture Properties
        [Header(Main Textures)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.4, 0.8, 0.3, 1)
        _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0, 2)) = 1.0
        
        // Alpha and Cutoff
        [Header(Alpha)]
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        
        // Toon Shading
        [Header(Toon Shading)]
        _ShadowThreshold("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSmoothness("Shadow Smoothness", Range(0.001, 0.1)) = 0.01
        _ShadowColor("Shadow Tint", Color) = (0.2, 0.4, 0.2, 1)
        
        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (0.8, 1, 0.6, 1)
        _RimPower("Rim Power", Range(0.1, 10)) = 2.0
        _RimIntensity("Rim Intensity", Range(0, 2)) = 1.0
        
        [Header(Highlight)]
        _HighlightColor("Highlight Color", Color) = (1, 1, 0.8, 1)
        _HighlightThreshold("Highlight Threshold", Range(0.8, 1)) = 0.95
        _HighlightSmoothness("Highlight Smoothness", Range(0.001, 0.1)) = 0.02
        _HighlightIntensity("Highlight Intensity", Range(0, 3)) = 1.0
        
        [Header(Ambient)]
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.4
        
        // Wind Animation
        [Header(Wind Animation)]
        [Toggle] _EnableWind("Enable Wind", Float) = 1
        _WindStrength("Wind Strength", Range(0, 5)) = 1.0
        _WindSpeed("Wind Speed", Range(0, 10)) = 2.0
        _WindDirection("Wind Direction", Vector) = (1, 0, 0.3, 0)
        _WindTurbulence("Wind Turbulence", Range(0, 2)) = 0.5
        _WindPhaseVariation("Wind Phase Variation", Range(0, 10)) = 3.0
        
        // Bush Specific
        [Header(Bush Properties)]
        _VertexColorInfluence("Vertex Color Wind Influence", Range(0, 2)) = 1.0
        _BushDensityMask("Bush Density Mask", 2D) = "white" {}
        _DensityInfluence("Density Wind Influence", Range(0, 1)) = 0.3
        
        // Two-sided rendering for leaves
        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 0
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
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        // Texture declarations
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        TEXTURE2D(_BushDensityMask); SAMPLER(sampler_BushDensityMask);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _NormalScale;
            float _Cutoff;
            
            // Toon shading properties
            float _ShadowThreshold;
            float _ShadowSmoothness;
            float4 _ShadowColor;
            
            float4 _RimColor;
            float _RimPower;
            float _RimIntensity;
            
            float4 _HighlightColor;
            float _HighlightThreshold;
            float _HighlightSmoothness;
            float _HighlightIntensity;
            
            float _AmbientStrength;
            
            // Wind properties
            float _EnableWind;
            float _WindStrength;
            float _WindSpeed;
            float4 _WindDirection;
            float _WindTurbulence;
            float _WindPhaseVariation;
            
            // Bush properties
            float _VertexColorInfluence;
            float4 _BushDensityMask_ST;
            float _DensityInfluence;
            
            // Rendering
            float _Cull;
        CBUFFER_END

        // Shared vertex input structure
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 texcoord : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Wind animation function - Enhanced for more natural movement
        float3 ApplyWindAnimation(float3 worldPos, float3 objectPos, float4 vertexColor, float2 uv)
        {
            if (_EnableWind < 0.5) return worldPos;
            
            float time = _Time.y * _WindSpeed;
            
            // Create phase variation based on object position to avoid synchronization
            float phaseX = sin(objectPos.x * _WindPhaseVariation) * 0.5 + 0.5;
            float phaseZ = cos(objectPos.z * _WindPhaseVariation) * 0.5 + 0.5;
            
            // Sample density mask for wind influence variation
            float densityMask = SAMPLE_TEXTURE2D_LOD(_BushDensityMask, sampler_BushDensityMask, 
                TRANSFORM_TEX(uv, _BushDensityMask), 0).r;
            
            // Primary wind wave - smoother, more natural movement
            float windWave = sin(time + phaseX * 6.28) * cos(time * 0.7 + phaseZ * 6.28);
            
            // Add secondary wave for more complexity
            float secondaryWave = sin(time * 1.3 + phaseX * 4.0) * 0.4;
            
            // Add turbulence with multiple frequencies
            float turbulence1 = sin(time * 3.0 + phaseX * 12.56) * cos(time * 2.3 + phaseZ * 9.42);
            float turbulence2 = sin(time * 5.7 + phaseZ * 8.0) * 0.6;
            float turbulence = (turbulence1 + turbulence2) * _WindTurbulence * 0.5;
            
            // Combine wind effects
            float windEffect = (windWave + secondaryWave + turbulence * 0.3) * _WindStrength;
            
            // Use vertex color red channel and density for wind influence
            float windInfluence = vertexColor.r * _VertexColorInfluence;
            windInfluence += densityMask * _DensityInfluence;
            windInfluence = saturate(windInfluence);
            
            // Apply wind displacement with some vertical component for more natural movement
            float3 windDir = normalize(_WindDirection.xyz);
            float3 windOffset = windDir * windEffect * windInfluence;
            
            // Add slight vertical movement for leaves
            windOffset.y += sin(time * 2.0 + phaseX * 3.14) * windEffect * windInfluence * 0.2;
            
            return worldPos + windOffset;
        }

        // Toon shading functions
        float ToonRamp(float cosTheta, float threshold, float smoothness)
        {
            return smoothstep(threshold - smoothness, threshold + smoothness, cosTheta);
        }

        // Apply toon shading to a single light
        float3 ApplyToonShadingToLight(Light light, float3 normal, float3 viewDir, float3 baseColor)
        {
            // Calculate total light attenuation
            float lightAtten = light.distanceAttenuation * light.shadowAttenuation;
            
            if (lightAtten <= 0.0001)
                return float3(0, 0, 0);
                
            // Diffuse calculations
            float NdotL = dot(normal, light.direction);
            float rawDiffuseAmount = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, NdotL);
            
            // Calculate radiance
            float3 radiance = lerp(_ShadowColor.rgb, float3(1, 1, 1), rawDiffuseAmount);
            radiance *= light.color * lightAtten;
            
            float3 diffuse = baseColor * radiance;
            
            // Highlight calculation
            float3 halfVector = normalize(light.direction + viewDir);
            float NdotH = saturate(dot(normal, halfVector));
            
            float highlight = smoothstep(_HighlightThreshold - _HighlightSmoothness, _HighlightThreshold + _HighlightSmoothness, NdotH);
            highlight *= lightAtten;
            
            float3 specular = _HighlightColor.rgb * highlight * _HighlightIntensity;
            
            return diffuse + specular;
        }

        // Calculate rim lighting
        float3 CalculateRimLight(float3 normal, float3 viewDir)
        {
            float NdotV = 1.0 - saturate(dot(normal, viewDir));
            float rim = smoothstep(_RimPower - 0.1, _RimPower + 0.1, pow(NdotV, 2.0)) * _RimIntensity;
            return _RimColor.rgb * rim;
        }

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Proper cutout settings - key fix here
            Blend Off
            ZWrite On
            ZTest LEqual
            Cull [_Cull]
            ColorMask RGBA
            AlphaToMask On    // This helps with MSAA edge quality

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Enable alpha test - critical for cutout
            #define _ALPHATEST_ON 1

            // URP Forward pass keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS

            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogCoord : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float3 positionWS : TEXCOORD5;
                float4 shadowCoord : TEXCOORD6;
                float4 vertexColor : TEXCOORD7;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 8);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                // Apply wind animation
                float3 objectPosWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 animatedPositionWS = ApplyWindAnimation(vertexInput.positionWS, objectPosWS, input.color, input.texcoord);
                
                output.positionCS = TransformWorldToHClip(animatedPositionWS);
                output.positionWS = animatedPositionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = normalInput.bitangentWS;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.vertexColor = input.color;
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                
                // Shadow coordinates using animated position
                output.shadowCoord = GetShadowCoord(GetVertexPositionInputs(TransformWorldToObject(animatedPositionWS)));

                // Lightmap and SH
                OUTPUT_LIGHTMAP_UV(input.texcoord, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Sample textures
                half4 baseTexture = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = baseTexture * _BaseColor;

                // Enhanced alpha test - this is the key fix
                half alpha = baseColor.a * input.vertexColor.a;
                
                // Use a slightly sharper cutoff and ensure proper clipping
                half cutoffThreshold = max(_Cutoff, 0.001); // Ensure minimum threshold
                clip(alpha - cutoffThreshold);
                
                // Additional check for very low alpha values that might cause issues
                if (alpha < 0.01) discard;
                
                // From this point on, the pixel will be rendered fully opaque

                // Normal mapping
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalScale);
                half3x3 tangentToWorld = half3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                half3 normalWS = normalize(mul(normalTS, tangentToWorld));

                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Initialize lighting
                half3 finalColor = half3(0, 0, 0);

                // Ambient/GI contribution - enhanced for better skybox integration
                half3 bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS);
                finalColor += baseColor.rgb * bakedGI * _AmbientStrength;

                // Get shadow mask
                half4 shadowMask = unity_ProbesOcclusion;

                // Main light
                #ifdef _MAIN_LIGHT_SHADOWS
                    Light mainLight = GetMainLight(input.shadowCoord, input.positionWS, shadowMask);
                #else
                    Light mainLight = GetMainLight();
                #endif
                
                finalColor += ApplyToonShadingToLight(mainLight, normalWS, viewDirWS, baseColor.rgb);

                // Additional lights
                #ifdef _ADDITIONAL_LIGHTS
                    uint pixelLightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                    {
                        Light light = GetAdditionalLight(lightIndex, input.positionWS);
                        finalColor += ApplyToonShadingToLight(light, normalWS, viewDirWS, baseColor.rgb);
                    }
                #endif

                // Rim lighting
                finalColor += CalculateRimLight(normalWS, viewDirWS);

                // Apply fog
                finalColor = MixFog(finalColor, input.fogCoord);
                
                // CRITICAL: Return full alpha for cutout materials
                return half4(saturate(finalColor), 1.0);
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
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #define _ALPHATEST_ON 1
            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 vertexColor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 _LightDirection;
            float3 _LightPosition;

            ShadowVaryings ShadowPassVertex(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                
                // Apply wind animation for shadows too
                float3 objectPosWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 animatedPositionWS = ApplyWindAnimation(vertexInput.positionWS, objectPosWS, input.color, input.texcoord);
                
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - animatedPositionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(animatedPositionWS, normalWS, lightDirectionWS));
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.vertexColor = input.color;

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowPassFragment(ShadowVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseTexture = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = baseTexture.a * _BaseColor.a * input.vertexColor.a;
                
                // Same enhanced alpha test as main pass
                half cutoffThreshold = max(_Cutoff, 0.001);
                clip(alpha - cutoffThreshold);
                if (alpha < 0.01) discard;

                return 0;
            }

            ENDHLSL
        }

        // Depth pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #define _ALPHATEST_ON 1
            #pragma multi_compile_instancing

            struct DepthOnlyVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 vertexColor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthOnlyVaryings DepthOnlyVertex(Attributes input)
            {
                DepthOnlyVaryings output = (DepthOnlyVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                
                // Apply wind animation
                float3 objectPosWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 animatedPositionWS = ApplyWindAnimation(vertexInput.positionWS, objectPosWS, input.color, input.texcoord);
                
                output.positionCS = TransformWorldToHClip(animatedPositionWS);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.vertexColor = input.color;
                
                return output;
            }

            half4 DepthOnlyFragment(DepthOnlyVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseTexture = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = baseTexture.a * _BaseColor.a * input.vertexColor.a;
                
                // Same enhanced alpha test as main pass
                half cutoffThreshold = max(_Cutoff, 0.001);
                clip(alpha - cutoffThreshold);
                if (alpha < 0.01) discard;

                return 0;
            }
            ENDHLSL
        }
        
        // Depth Normals pass - important for some post effects like SSAO
        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode" = "DepthNormals"}

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #define _ALPHATEST_ON 1
            #pragma multi_compile_instancing

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 vertexColor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthNormalsVaryings DepthNormalsVertex(Attributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                // Apply wind animation
                float3 objectPosWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 animatedPositionWS = ApplyWindAnimation(vertexInput.positionWS, objectPosWS, input.color, input.texcoord);
                
                output.positionCS = TransformWorldToHClip(animatedPositionWS);
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.vertexColor = input.color;
                
                return output;
            }

            half4 DepthNormalsFragment(DepthNormalsVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseTexture = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = baseTexture.a * _BaseColor.a * input.vertexColor.a;
                
                // Same enhanced alpha test
                half cutoffThreshold = max(_Cutoff, 0.001);
                clip(alpha - cutoffThreshold);
                if (alpha < 0.01) discard;

                // Pack normals for depth normals texture
                float3 normalVS = TransformWorldToViewDir(input.normalWS, true);
                return float4(PackNormalOctRectEncode(normalVS), 0.0, 0.0);
            }
            ENDHLSL
        }
    }
    // CustomEditor "MilklessCereal.Editor.LeafToonShaderGUI"
}