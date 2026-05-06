Shader "MilklessCereal/URP/Terrain Multi-Texture Triplanar Toon"
{
    Properties
    {
        // Texture Layer 1 (Red Channel)
        [Header(Layer 1 Red Channel)]
        _Layer1_BaseMap("Layer 1 Base Map", 2D) = "white" {}
        _Layer1_NormalMap("Layer 1 Normal Map", 2D) = "bump" {}
        _Layer1_NormalScale("Layer 1 Normal Scale", Range(0, 2)) = 1.0
        _Layer1_Tiling("Layer 1 Tiling", Float) = 1.0
        
        // Texture Layer 2 (Green Channel)
        [Header(Layer 2 Green Channel)]
        _Layer2_BaseMap("Layer 2 Base Map", 2D) = "white" {}
        _Layer2_NormalMap("Layer 2 Normal Map", 2D) = "bump" {}
        _Layer2_NormalScale("Layer 2 Normal Scale", Range(0, 2)) = 1.0
        _Layer2_Tiling("Layer 2 Tiling", Float) = 1.0
        
        // Texture Layer 3 (Blue Channel)
        [Header(Layer 3 Blue Channel)]
        _Layer3_BaseMap("Layer 3 Base Map", 2D) = "white" {}
        _Layer3_NormalMap("Layer 3 Normal Map", 2D) = "bump" {}
        _Layer3_NormalScale("Layer 3 Normal Scale", Range(0, 2)) = 1.0
        _Layer3_Tiling("Layer 3 Tiling", Float) = 1.0
        
        // Texture Layer 4 (White Areas)
        [Header(Layer 4 White Areas)]
        _Layer4_BaseMap("Layer 4 Base Map", 2D) = "white" {}
        _Layer4_NormalMap("Layer 4 Normal Map", 2D) = "bump" {}
        _Layer4_NormalScale("Layer 4 Normal Scale", Range(0, 2)) = 1.0
        _Layer4_Tiling("Layer 4 Tiling", Float) = 1.0

        [Header(Triplanar Settings)]
        _TriplanarBlendSharpness("Triplanar Blend Sharpness", Range(1, 16)) = 4.0
        
        [Header(White Detection)]
        _WhiteThreshold("White Detection Threshold", Range(0.8, 1.0)) = 0.95

        [Header(Toon Shading)]
        _ShadowThreshold("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSmoothness("Shadow Smoothness", Range(0.001, 0.1)) = 0.01
        _ShadowColor("Shadow Tint", Color) = (0.4, 0.4, 0.6, 1)
        
        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower("Rim Power", Range(0.1, 10)) = 2.0
        _RimIntensity("Rim Intensity", Range(0, 2)) = 1.0
        
        [Header(Highlight)]
        _HighlightColor("Highlight Color", Color) = (1, 1, 0.8, 1)
        _HighlightThreshold("Highlight Threshold", Range(0.8, 1)) = 0.95
        _HighlightSmoothness("Highlight Smoothness", Range(0.001, 0.1)) = 0.02
        _HighlightIntensity("Highlight Intensity", Range(0, 3)) = 1.5
        
        [Header(Ambient)]
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.3

        // Hidden properties for render pipeline
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        // Texture declarations
        TEXTURE2D(_Layer1_BaseMap); SAMPLER(sampler_Layer1_BaseMap);
        TEXTURE2D(_Layer1_NormalMap); SAMPLER(sampler_Layer1_NormalMap);
        TEXTURE2D(_Layer2_BaseMap); SAMPLER(sampler_Layer2_BaseMap);
        TEXTURE2D(_Layer2_NormalMap); SAMPLER(sampler_Layer2_NormalMap);
        TEXTURE2D(_Layer3_BaseMap); SAMPLER(sampler_Layer3_BaseMap);
        TEXTURE2D(_Layer3_NormalMap); SAMPLER(sampler_Layer3_NormalMap);
        TEXTURE2D(_Layer4_BaseMap); SAMPLER(sampler_Layer4_BaseMap);
        TEXTURE2D(_Layer4_NormalMap); SAMPLER(sampler_Layer4_NormalMap);

        CBUFFER_START(UnityPerMaterial)
            float _Layer1_Tiling;
            float _Layer1_NormalScale;
            float _Layer2_Tiling;
            float _Layer2_NormalScale;
            float _Layer3_Tiling;
            float _Layer3_NormalScale;
            float _Layer4_Tiling;
            float _Layer4_NormalScale;
            float _TriplanarBlendSharpness;
            float _WhiteThreshold;
            
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
            
            // Hidden properties
            float _Surface;
            float _Blend;
            float _AlphaClip;
            float _SrcBlend;
            float _DstBlend;
            float _ZWrite;
            float _Cull;
        CBUFFER_END

        // Shared vertex input structure for consistency
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 texcoord : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Triplanar sampling functions
        float3 GetTriplanarWeights(float3 normal, float sharpness)
        {
            float3 blendWeights = abs(normal);
            blendWeights = pow(blendWeights, sharpness);
            return blendWeights / (blendWeights.x + blendWeights.y + blendWeights.z);
        }

        float4 SampleTriplanar(TEXTURE2D_PARAM(tex, sampler_tex), float3 worldPos, float3 normal, float tiling, float sharpness)
        {
            float3 blendWeights = GetTriplanarWeights(normal, sharpness);
            
            // Sample from three planes
            float4 xProjection = SAMPLE_TEXTURE2D(tex, sampler_tex, worldPos.zy * tiling);
            float4 yProjection = SAMPLE_TEXTURE2D(tex, sampler_tex, worldPos.xz * tiling);
            float4 zProjection = SAMPLE_TEXTURE2D(tex, sampler_tex, worldPos.xy * tiling);
            
            // Blend based on normal direction
            return xProjection * blendWeights.x + yProjection * blendWeights.y + zProjection * blendWeights.z;
        }

        // Triplanar normal sampling with proper orientation
        float3 SampleTriplanarNormal(TEXTURE2D_PARAM(normalTex, sampler_normalTex), float3 worldPos, float3 worldNormal, float3 tangentX, float3 tangentY, float3 tangentZ, float tiling, float sharpness, float normalScale)
        {
            float3 blendWeights = GetTriplanarWeights(worldNormal, sharpness);
            
            // Sample normals from three planes
            float3 normalX = UnpackNormalScale(SAMPLE_TEXTURE2D(normalTex, sampler_normalTex, worldPos.zy * tiling), normalScale);
            float3 normalY = UnpackNormalScale(SAMPLE_TEXTURE2D(normalTex, sampler_normalTex, worldPos.xz * tiling), normalScale);
            float3 normalZ = UnpackNormalScale(SAMPLE_TEXTURE2D(normalTex, sampler_normalTex, worldPos.xy * tiling), normalScale);
            
            // Transform normals to world space for each projection plane
            float3 worldNormalX = normalize(normalX.x * tangentX + normalX.y * float3(-worldNormal.z, 0, worldNormal.x) + normalX.z * worldNormal);
            float3 worldNormalY = normalize(normalY.x * tangentY + normalY.y * float3(0, -worldNormal.z, worldNormal.y) + normalY.z * worldNormal);
            float3 worldNormalZ = normalize(normalZ.x * tangentZ + normalZ.y * float3(worldNormal.y, -worldNormal.x, 0) + normalZ.z * worldNormal);
            
            // Blend the world-space normals
            float3 blendedNormal = worldNormalX * blendWeights.x + worldNormalY * blendWeights.y + worldNormalZ * blendWeights.z;
            return normalize(blendedNormal);
        }

        // Generate tangent vectors for triplanar normal mapping
        void GetTriplanarTangents(float3 worldNormal, out float3 tangentX, out float3 tangentY, out float3 tangentZ)
        {
            // For X projection (YZ plane)
            tangentX = normalize(cross(worldNormal, float3(0, 0, 1)));
            if (length(tangentX) < 0.1) tangentX = normalize(cross(worldNormal, float3(0, 1, 0)));
            
            // For Y projection (XZ plane) 
            tangentY = normalize(cross(worldNormal, float3(1, 0, 0)));
            if (length(tangentY) < 0.1) tangentY = normalize(cross(worldNormal, float3(0, 0, 1)));
            
            // For Z projection (XY plane)
            tangentZ = normalize(cross(worldNormal, float3(0, 1, 0)));
            if (length(tangentZ) < 0.1) tangentZ = normalize(cross(worldNormal, float3(1, 0, 0)));
        }

        // Toon shading functions
        float ToonRamp(float cosTheta, float threshold, float smoothness)
        {
            return smoothstep(threshold - smoothness, threshold + smoothness, cosTheta);
        }

        // Apply toon shading to a single light - works for all light types
        float3 ApplyToonShadingToLight(Light light, float3 normal, float3 viewDir, float3 baseColor)
        {
            // Calculate total light attenuation first
            float lightAtten = light.distanceAttenuation * light.shadowAttenuation;
            
            // Early exit if light has absolutely no contribution
            if (lightAtten <= 0.0001)
                return float3(0, 0, 0);
                
            // Diffuse calculations (n dot l)
            float NdotL = dot(normal, light.direction);
            float rawDiffuseAmount = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, NdotL);
            
            // Calculate radiance - lerp between shadow and light tint based on diffuse amount
            float3 radiance = lerp(_ShadowColor.rgb, float3(1, 1, 1), rawDiffuseAmount);
            radiance *= light.color * lightAtten;
            
            // Apply to base color
            float3 diffuse = baseColor * radiance;
            
            // Highlight/Specular calculation
            float3 halfVector = normalize(light.direction + viewDir);
            float NdotH = saturate(dot(normal, halfVector));
            
            // Simple toon specular ramp
            float highlight = smoothstep(_HighlightThreshold - _HighlightSmoothness, _HighlightThreshold + _HighlightSmoothness, NdotH);
            highlight *= lightAtten; // Apply same attenuation
            
            float3 specular = _HighlightColor.rgb * highlight * _HighlightIntensity;
            
            return diffuse + specular;
        }

        // Calculate rim lighting (view dependent, so done once)
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

            Blend Off
            ZWrite On
            ZTest LEqual
            Cull Back
            ColorMask RGBA

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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
                float4 vertexColor : COLOR;
                float fogCoord : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);
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

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.vertexColor = input.color;
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
                
                // Shadow coordinates
                output.shadowCoord = GetShadowCoord(vertexInput);

                // Lightmap and SH (for GI)
                OUTPUT_LIGHTMAP_UV(input.texcoord, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Sample all texture layers using triplanar projection
                float4 tex1 = SampleTriplanar(TEXTURE2D_ARGS(_Layer1_BaseMap, sampler_Layer1_BaseMap), input.positionWS, normalWS, _Layer1_Tiling, _TriplanarBlendSharpness);
                float4 tex2 = SampleTriplanar(TEXTURE2D_ARGS(_Layer2_BaseMap, sampler_Layer2_BaseMap), input.positionWS, normalWS, _Layer2_Tiling, _TriplanarBlendSharpness);
                float4 tex3 = SampleTriplanar(TEXTURE2D_ARGS(_Layer3_BaseMap, sampler_Layer3_BaseMap), input.positionWS, normalWS, _Layer3_Tiling, _TriplanarBlendSharpness);
                float4 tex4 = SampleTriplanar(TEXTURE2D_ARGS(_Layer4_BaseMap, sampler_Layer4_BaseMap), input.positionWS, normalWS, _Layer4_Tiling, _TriplanarBlendSharpness);

                // Generate tangent vectors for triplanar normal mapping
                float3 tangentX, tangentY, tangentZ;
                GetTriplanarTangents(normalWS, tangentX, tangentY, tangentZ);

                // Sample normal maps using triplanar projection
                float3 normal1 = SampleTriplanarNormal(TEXTURE2D_ARGS(_Layer1_NormalMap, sampler_Layer1_NormalMap), input.positionWS, normalWS, tangentX, tangentY, tangentZ, _Layer1_Tiling, _TriplanarBlendSharpness, _Layer1_NormalScale);
                float3 normal2 = SampleTriplanarNormal(TEXTURE2D_ARGS(_Layer2_NormalMap, sampler_Layer2_NormalMap), input.positionWS, normalWS, tangentX, tangentY, tangentZ, _Layer2_Tiling, _TriplanarBlendSharpness, _Layer2_NormalScale);
                float3 normal3 = SampleTriplanarNormal(TEXTURE2D_ARGS(_Layer3_NormalMap, sampler_Layer3_NormalMap), input.positionWS, normalWS, tangentX, tangentY, tangentZ, _Layer3_Tiling, _TriplanarBlendSharpness, _Layer3_NormalScale);
                float3 normal4 = SampleTriplanarNormal(TEXTURE2D_ARGS(_Layer4_NormalMap, sampler_Layer4_NormalMap), input.positionWS, normalWS, tangentX, tangentY, tangentZ, _Layer4_Tiling, _TriplanarBlendSharpness, _Layer4_NormalScale);

                float3 vertexRGB = input.vertexColor.rgb;
                
                // Calculate how "white" this vertex is (minimum of all channels)
                float whiteWeight = min(min(vertexRGB.r, vertexRGB.g), vertexRGB.b);
                
                // Subtract the white component from each channel to get pure colors
                float3 pureColors = vertexRGB - whiteWeight;
                
                // Individual channel weights are now the pure color components
                float redWeight = pureColors.r;
                float greenWeight = pureColors.g;
                float blueWeight = pureColors.b;
                
                // Normalize weights
                float totalWeight = redWeight + greenWeight + blueWeight + whiteWeight;
                if (totalWeight > 0.001)
                {
                    redWeight /= totalWeight;
                    greenWeight /= totalWeight; 
                    blueWeight /= totalWeight;
                    whiteWeight /= totalWeight;
                }
                else
                {
                    // Fallback - equal blend
                    redWeight = greenWeight = blueWeight = whiteWeight = 0.25;
                }

                // Blend textures and normals
                float3 baseColor = tex1.rgb * redWeight + 
                                  tex2.rgb * greenWeight + 
                                  tex3.rgb * blueWeight + 
                                  tex4.rgb * whiteWeight;

                float3 finalNormal = normalize(normal1 * redWeight + 
                                             normal2 * greenWeight + 
                                             normal3 * blueWeight + 
                                             normal4 * whiteWeight);

                // Initialize lighting accumulation
                float3 finalColor = float3(0, 0, 0);

                // Ambient/GI contribution
                float3 bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, finalNormal);
                finalColor += baseColor * bakedGI * _AmbientStrength;

                // Get shadow mask for proper light blending
                float4 shadowMask = unity_ProbesOcclusion;

                // Create InputData structure for Forward+ compatibility
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = finalNormal;
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogCoord;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = bakedGI;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = shadowMask;

                // Main light
                #ifdef _MAIN_LIGHT_SHADOWS
                    Light mainLight = GetMainLight(input.shadowCoord, input.positionWS, shadowMask);
                #else
                    Light mainLight = GetMainLight();
                #endif
                
                finalColor += ApplyToonShadingToLight(mainLight, finalNormal, viewDirWS, baseColor);

                // Additional lights with Forward+ support
                half3 additionalLightColor = 0.0h;
                #ifdef _ADDITIONAL_LIGHTS
                    uint pixelLightCount = GetAdditionalLightsCount();
                    
                    #if USE_FORWARD_PLUS
                        // Forward+ path with proper InputData
                        for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex) 
                        {
                            FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                            
                            Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                            additionalLightColor += ApplyToonShadingToLight(light, finalNormal, viewDirWS, baseColor);
                        }
                    #endif
                    
                    // Standard additional lights loop
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                        #ifdef _ADDITIONAL_LIGHT_SHADOWS
                            light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, input.positionWS, light.direction);
                        #endif
                        additionalLightColor += ApplyToonShadingToLight(light, finalNormal, viewDirWS, baseColor);
                    LIGHT_LOOP_END
                #endif
                
                finalColor += additionalLightColor;

                // Rim lighting (view dependent, calculated once)
                finalColor += CalculateRimLight(finalNormal, viewDirWS);

                // Apply fog
                finalColor = MixFog(finalColor, input.fogCoord);
                
                return float4(finalColor, 1.0);
            }

            ENDHLSL
        }
        
        // Shadow caster pass for proper shadow casting
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

            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
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

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

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
                return 0;
            }

            ENDHLSL
        }

        // Depth pass for depth pre-pass
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

            #pragma multi_compile_instancing

            struct DepthOnlyVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthOnlyVaryings DepthOnlyVertex(Attributes input)
            {
                DepthOnlyVaryings output = (DepthOnlyVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(DepthOnlyVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }

        // Depth Normals pass for SSAO and other effects
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile_instancing

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthNormalsVaryings DepthNormalsVertex(Attributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 DepthNormalsFragment(DepthNormalsVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                return float4(PackNormalOctRectEncode(TransformWorldToViewDir(normalWS, true)), 0.0, 0.0);
            }
            ENDHLSL
        }
    }
    CustomEditor "MilklessCereal.Editor.TerrainTriplanarToonShaderGUI"
}