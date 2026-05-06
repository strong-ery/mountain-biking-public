Shader "MilklessCereal/URP/ToonWaterForwardPlus"
{
    Properties
    {
        _ShallowColor ("Shallow Water Color", Color) = (0.4, 0.9, 1.0, 0.9)
        _DeepColor ("Deep Water Color", Color) = (0.0, 0.3, 0.7, 1.0)
        _HighlightColor ("Highlight Color", Color) = (1.0, 1.0, 1.0, 1.0)
        
        _FoamColor ("Foam Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _FoamAmount ("Foam Amount", Range(0, 5)) = 1.5
        _FoamCutoff ("Foam Cutoff", Range(0, 1)) = 0.4
        _FoamSmoothness ("Foam Smoothness", Range(0.01, 0.3)) = 0.05
        _FoamSpeed ("Foam Animation Speed", Range(0, 3)) = 0.8
        _FoamScale ("Foam Texture Scale", Range(0.1, 20)) = 8.0
        
        // Surface foam properties
        _SurfaceFoamAmount ("Surface Foam Amount", Range(0, 2)) = 0.3
        _SurfaceFoamCutoff ("Surface Foam Cutoff", Range(0, 1)) = 0.6
        _SurfaceFoamScale ("Surface Foam Scale", Range(0.5, 50)) = 15.0
        _SurfaceFoamSpeed ("Surface Foam Speed", Range(0, 2)) = 0.4
        _SurfaceFoamContrast ("Surface Foam Contrast", Range(1, 8)) = 3.0
        
        _LightSteps ("Light Steps", Range(2, 8)) = 3
        _ShadowIntensity ("Shadow Intensity", Range(0, 1)) = 0.3
        _HighlightSize ("Highlight Size", Range(0.1, 2)) = 0.8
        _HighlightSharpness ("Highlight Sharpness", Range(1, 20)) = 8.0
        _CellShadingThreshold ("Cell Shading Threshold", Range(0.1, 0.9)) = 0.5
        
        _WaveSpeed ("Wave Speed", Range(0, 3)) = 0.5
        _WaveScale ("Wave Scale", Range(0.5, 50)) = 10.0
        _WaveHeight ("Wave Height", Range(0, 0.3)) = 0.05
        _RippleScale ("Ripple Scale", Range(1, 100)) = 25.0
        
        _DepthFade ("Depth Fade Distance", Range(0.1, 10)) = 1.0
        _EdgeFade ("Edge Fade Distance", Range(0.01, 2)) = 0.3
        _Alpha ("Alpha", Range(0, 1)) = 0.85
        
        _WorldScale ("World Scale Multiplier", Range(0.1, 10)) = 1.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        LOD 300
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTERED_RENDERING
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
            float4 _ShallowColor;
            float4 _DeepColor;
            float4 _HighlightColor;
            float4 _FoamColor;
            float _FoamAmount;
            float _FoamCutoff;
            float _FoamSmoothness;
            float _FoamSpeed;
            float _FoamScale;
            float _SurfaceFoamAmount;
            float _SurfaceFoamCutoff;
            float _SurfaceFoamScale;
            float _SurfaceFoamSpeed;
            float _SurfaceFoamContrast;
            float _LightSteps;
            float _ShadowIntensity;
            float _HighlightSize;
            float _HighlightSharpness;
            float _CellShadingThreshold;
            float _WaveSpeed;
            float _WaveScale;
            float _WaveHeight;
            float _RippleScale;
            float _DepthFade;
            float _EdgeFade;
            float _Alpha;
            float _WorldScale;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                float3 viewDirWS : TEXCOORD4;
                float fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }
            
            float smoothNoise(float2 uv)
            {
                float2 lv = frac(uv);
                float2 id = floor(uv);
                lv = lv * lv * (3.0 - 2.0 * lv);
                
                float bl = hash21(id);
                float br = hash21(id + float2(1, 0));
                float tl = hash21(id + float2(0, 1));
                float tr = hash21(id + float2(1, 1));
                
                float b = lerp(bl, br, lv.x);
                float t = lerp(tl, tr, lv.x);
                
                return lerp(b, t, lv.y);
            }
            
            float layeredNoise(float2 uv, int octaves)
            {
                float value = 0.0;
                float amplitude = 1.0;
                float frequency = 1.0;
                float maxValue = 0.0;
                
                for (int i = 0; i < octaves; i++)
                {
                    value += smoothNoise(uv * frequency) * amplitude;
                    maxValue += amplitude;
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                
                return value / maxValue;
            }
            
            // Voronoi-style noise for more organic foam patterns
            float voronoi(float2 uv)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);
                
                float minDist = 1.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 cellPoint = hash21(g + neighbor) * float2(0.8, 0.8) + 0.1;
                        float2 diff = neighbor + cellPoint - f;
                        float dist = length(diff);
                        minDist = min(minDist, dist);
                    }
                }
                return minDist;
            }
            
            float ToonLighting(float NdotL)
            {
                float lightValue = NdotL * 0.5 + 0.5;
                lightValue = floor(lightValue * _LightSteps) / _LightSteps;
                
                if (lightValue < _CellShadingThreshold)
                    lightValue = _ShadowIntensity;
                else
                    lightValue = 1.0;
                
                return lightValue;
            }
            
            float ToonHighlight(float3 normal, float3 viewDir, float3 lightDir)
            {
                float3 halfVector = normalize(lightDir + viewDir);
                float NdotH = dot(normal, halfVector);
                float highlight = pow(max(0, NdotH), _HighlightSharpness);
                highlight = step(_HighlightSize, highlight);
                return highlight;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                float3 worldPos = vertexInput.positionWS;
                float2 worldUV = worldPos.xz * _WorldScale / _WaveScale;
                float time = _Time.y * _WaveSpeed;
                
                float wave1 = sin(worldUV.x * 2.0 + time) * 0.5;
                float wave2 = cos(worldUV.y * 1.5 + time * 0.8) * 0.3;
                float waveOffset = (wave1 + wave2) * _WaveHeight;
                
                worldPos.y += waveOffset;
                
                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                float2 worldUV = input.positionWS.xz * _WorldScale;
                
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceDepth = LinearEyeDepth(input.screenPos.z / input.screenPos.w, _ZBufferParams);
                float depthDifference = sceneDepth - surfaceDepth;
                
                float time = _Time.y;
                float2 rippleUV1 = worldUV / _RippleScale + time * _WaveSpeed * 0.1;
                float2 rippleUV2 = worldUV / _RippleScale * 0.7 - time * _WaveSpeed * 0.05;
                
                float ripple1 = layeredNoise(rippleUV1, 2) * 2.0 - 1.0;
                float ripple2 = layeredNoise(rippleUV2, 2) * 2.0 - 1.0;
                
                float3 worldNormal = normalize(input.normalWS + float3(ripple1, 0, ripple2) * 0.1);
                
                Light mainLight = GetMainLight();
                float NdotL = dot(worldNormal, mainLight.direction);
                float toonLight = ToonLighting(NdotL);
                
                float highlight = ToonHighlight(worldNormal, input.viewDirWS, mainLight.direction);
                
                float depthFactor = saturate(depthDifference / _DepthFade);
                float4 waterColor = lerp(_ShallowColor, _DeepColor, depthFactor);
                
                waterColor.rgb *= toonLight;
                waterColor.rgb += _HighlightColor.rgb * highlight;
                
                // Edge foam (original)
                float2 foamUV = worldUV / _FoamScale;
                float foamTime = time * _FoamSpeed;
                
                float foam1 = layeredNoise(foamUV + foamTime * float2(0.1, 0.2), 3);
                float foam2 = layeredNoise(foamUV * 1.3 - foamTime * float2(0.15, 0.1), 2);
                float foamPattern = (foam1 + foam2 * 0.6) / 1.6;
                
                float foamDepthMask = saturate(_FoamAmount / max(0.01, depthDifference));
                foamDepthMask = pow(foamDepthMask, 3.0);
                
                float edgeFoam = smoothstep(_FoamCutoff - _FoamSmoothness, _FoamCutoff + _FoamSmoothness, foamPattern * foamDepthMask);
                
                // Surface foam (new Wind Waker style)
                float2 surfaceFoamUV = worldUV / _SurfaceFoamScale;
                float surfaceFoamTime = time * _SurfaceFoamSpeed;
                
                // Create multiple layers of surface foam with different scales and speeds
                float surfaceFoam1 = voronoi(surfaceFoamUV + surfaceFoamTime * float2(0.1, 0.15));
                float surfaceFoam2 = layeredNoise(surfaceFoamUV * 0.8 + surfaceFoamTime * float2(-0.05, 0.12), 2);
                float surfaceFoam3 = layeredNoise(surfaceFoamUV * 1.5 - surfaceFoamTime * float2(0.08, -0.1), 1);
                
                // Combine foam patterns
                float surfaceFoamPattern = surfaceFoam1 * 0.6 + surfaceFoam2 * 0.3 + surfaceFoam3 * 0.1;
                surfaceFoamPattern = pow(surfaceFoamPattern, _SurfaceFoamContrast);
                
                // Apply threshold to create foam patches
                float surfaceFoam = smoothstep(_SurfaceFoamCutoff - 0.1, _SurfaceFoamCutoff + 0.1, surfaceFoamPattern);
                surfaceFoam *= _SurfaceFoamAmount;
                
                // Combine edge foam and surface foam
                float totalFoam = max(edgeFoam, surfaceFoam);
                
                float4 finalColor = waterColor;
                finalColor.rgb = lerp(finalColor.rgb, _FoamColor.rgb, totalFoam);
                
                float edgeFade = saturate(depthDifference / _EdgeFade);
                finalColor.a = _Alpha * edgeFade;
                finalColor.a = lerp(finalColor.a, 1.0, totalFoam);
                
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}