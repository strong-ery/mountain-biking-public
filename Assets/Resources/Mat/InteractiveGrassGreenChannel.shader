Shader "MilklessCereal/URP/Enhanced Waving Grass"
{
    Properties
    {
        [Header(Grass Appearance)]
        _GrassTexture("Grass Texture", 2D) = "white" {}
        _GrassColor("Grass Base Color", Color) = (0.3, 0.7, 0.2, 1)
        _GrassTipColor("Grass Tip Color", Color) = (0.8, 1.0, 0.4, 1)
        
        [Header(Wind Animation)]
        _WindStrength("Wind Strength", Range(0, 2)) = 0.5
        _WindSpeed("Wind Speed", Range(0, 5)) = 1.0
        _WindScale("Wind Scale", Range(0.1, 10)) = 1.0
        _WindDirection("Wind Direction", Vector) = (1, 0, 0.5, 0)
        _WindWaveLength("Wind Wave Length", Range(1, 50)) = 10.0
        _WindWaveSpeed("Wind Wave Speed", Range(0, 5)) = 1.5
        _WindCoherence("Wind Coherence", Range(0, 1)) = 0.7
        
        [Header(Grass Properties)]
        _GrassHeight("Base Grass Height", Range(0.1, 3)) = 1.0
        _GrassHeightVariation("Height Variation", Range(0, 1)) = 0.3
        _GrassClumping("Clumping Amount", Range(0, 1)) = 0.4
        _ClumpingScale("Clumping Scale", Range(0.1, 10)) = 2.0
        _GrassBend("Natural Bend", Range(0, 1)) = 0.2
        
        [Header(Distance Culling)]
        _MaxRenderDistance("Max Render Distance", Range(10, 500)) = 100.0
        _DistanceFadeRange("Distance Fade Range", Range(1, 50)) = 10.0
        [Toggle] _EnableDistanceCulling("Enable Distance Culling", Float) = 1
        
        [Header(Rendering)]
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "TransparentCutout" 
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "DisableBatching" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_GrassTexture); SAMPLER(sampler_GrassTexture);

        CBUFFER_START(UnityPerMaterial)
            float4 _GrassTexture_ST;
            float4 _GrassColor;
            float4 _GrassTipColor;
            float _WindStrength;
            float _WindSpeed;
            float _WindScale;
            float4 _WindDirection;
            float _WindWaveLength;
            float _WindWaveSpeed;
            float _WindCoherence;
            float _GrassHeight;
            float _GrassHeightVariation;
            float _GrassClumping;
            float _ClumpingScale;
            float _GrassBend;
            float _MaxRenderDistance;
            float _DistanceFadeRange;
            float _EnableDistanceCulling;
            float _Cutoff;
        CBUFFER_END

        // Improved noise functions
        float Hash(float2 p)
        {
            p = frac(p * float2(5.3987, 5.4421));
            p += dot(p.yx, p.xy + float2(21.5351, 14.3137));
            return frac(p.x * p.y * 95.4307);
        }

        float SimpleNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            
            float a = Hash(i);
            float b = Hash(i + float2(1, 0));
            float c = Hash(i + float2(0, 1));
            float d = Hash(i + float2(1, 1));
            
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        // Fractal noise for more interesting patterns
        float FractalNoise(float2 p)
        {
            float value = 0.0;
            float amplitude = 0.5;
            
            for (int i = 0; i < 4; i++)
            {
                value += SimpleNoise(p) * amplitude;
                p *= 2.0;
                amplitude *= 0.5;
            }
            
            return value;
        }

        // Calculate distance fade factor
        float CalculateDistanceFade(float3 worldPos)
        {
            if (_EnableDistanceCulling < 0.5) return 1.0;
            
            float distance = length(_WorldSpaceCameraPos - worldPos);
            float fadeStart = _MaxRenderDistance - _DistanceFadeRange;
            
            // Hard cutoff beyond max distance
            if (distance > _MaxRenderDistance) return 0.0;
            
            // Smooth fade within fade range
            if (distance > fadeStart)
            {
                float fadeAmount = (distance - fadeStart) / _DistanceFadeRange;
                return 1.0 - smoothstep(0.0, 1.0, fadeAmount);
            }
            
            return 1.0;
        }

        // Calculate cohesive wind with sine waves + noise
        float3 CalculateCohesiveWind(float3 worldPos, float time, float heightFactor)
        {
            // Primary wind direction
            float3 windDir = normalize(_WindDirection.xyz);
            
            // Create sine wave that travels across the field
            float wavePhase = dot(worldPos.xz, windDir.xz) / _WindWaveLength + time * _WindWaveSpeed;
            float windWave = sin(wavePhase) * 0.5 + 0.5;
            
            // Add secondary wave for more complexity
            float secondaryWave = sin(wavePhase * 1.7 + 2.1) * 0.3 + 0.5;
            float combinedWave = (windWave + secondaryWave) * 0.5;
            
            // Create noise for local variation
            float2 noiseUV = worldPos.xz * _WindScale + time * _WindSpeed * 0.5;
            float windNoise = FractalNoise(noiseUV) * 0.5 + 0.5;
            
            // Blend wave and noise based on coherence
            float windStrength = lerp(windNoise, combinedWave, _WindCoherence) * _WindStrength;
            
            // Apply wind direction with some perpendicular movement
            float3 windForce = windDir * windStrength;
            
            // Add some perpendicular wind movement for more natural look
            float3 perpDir = float3(-windDir.z, 0, windDir.x);
            windForce += perpDir * sin(wavePhase * 0.7 + time) * windStrength * 0.3;
            
            // Scale by height - more effect at grass tips
            windForce *= heightFactor * heightFactor;
            
            return windForce;
        }

        // Calculate height variation and clumping
        float CalculateGrassHeightMultiplier(float3 worldPos)
        {
            // Base height variation using fractal noise
            float2 heightUV = worldPos.xz * 0.1;
            float heightNoise = FractalNoise(heightUV) * 0.5 + 0.5;
            
            // Clumping effect - creates patches of taller/shorter grass
            float2 clumpUV = worldPos.xz * _ClumpingScale;
            float clumpNoise = FractalNoise(clumpUV);
            
            // Sharpen the clumping effect
            clumpNoise = smoothstep(0.3, 0.7, clumpNoise);
            
            // Combine height variation and clumping
            float heightVariation = lerp(1.0 - _GrassHeightVariation * 0.5, 1.0 + _GrassHeightVariation * 0.5, heightNoise);
            float clumpingEffect = lerp(1.0, clumpNoise, _GrassClumping);
            
            return heightVariation * clumpingEffect;
        }

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
            float2 uv : TEXCOORD0;
            float3 worldPos : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            float grassHeight : TEXCOORD3;
            float3 worldNormal : TEXCOORD4;
            float heightMultiplier : TEXCOORD5;
            float distanceFade : TEXCOORD6;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Get world position
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                
                // Calculate distance fade early for potential vertex culling
                float distanceFade = CalculateDistanceFade(worldPos);
                
                // Early exit if completely faded (for performance)
                if (distanceFade <= 0.0)
                {
                    // Move vertex far away to effectively cull it
                    output.positionCS = float4(0, 0, 0, -1);
                    return output;
                }
                
                // Calculate height multiplier for this grass blade
                float heightMultiplier = CalculateGrassHeightMultiplier(worldPos);
                
                // Height factor (0 = base, 1 = tip) - affected by height variation
                float heightFactor = input.uv.y;
                
                // Apply height variation to the mesh
                float3 scaledPos = input.positionOS.xyz;
                scaledPos.y *= _GrassHeight * heightMultiplier;
                worldPos = TransformObjectToWorld(scaledPos);
                
                // Calculate cohesive wind displacement
                float3 windOffset = CalculateCohesiveWind(worldPos, _Time.y, heightFactor);
                
                // Add natural bend (grass leans slightly)
                float3 naturalBend = float3(_GrassBend * heightFactor * 0.1, 0, 0);
                
                // Apply all offsets
                float3 finalOffset = windOffset + naturalBend;
                float3 finalWorldPos = worldPos + finalOffset;
                
                // Transform to clip space
                output.positionCS = TransformWorldToHClip(finalWorldPos);
                output.worldPos = finalWorldPos;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.worldNormal = output.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _GrassTexture);
                output.grassHeight = heightFactor;
                output.heightMultiplier = heightMultiplier;
                output.distanceFade = distanceFade;
                
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Sample grass texture
                float4 grassTex = SAMPLE_TEXTURE2D(_GrassTexture, sampler_GrassTexture, input.uv);
                
                // Apply distance fade to alpha
                grassTex.a *= input.distanceFade;
                
                // Alpha test with distance fade applied
                clip(grassTex.a - _Cutoff);
                
                // Color gradient from base to tip
                float3 grassColor = lerp(_GrassColor.rgb, _GrassTipColor.rgb, input.grassHeight);
                
                // Slightly vary color based on height multiplier for more variation
                float colorVariation = input.heightMultiplier * 0.1 + 0.95;
                grassColor *= colorVariation;
                
                float3 finalColor = grassTex.rgb * grassColor;
                
                // Simple lighting
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 normal = normalize(input.normalWS);
                
                // Lambert lighting with wrap-around
                float NdotL = dot(normal, lightDir) * 0.5 + 0.5;
                float3 lighting = mainLight.color * NdotL + unity_AmbientSky.rgb * 0.3;
                
                finalColor *= lighting;
                
                // Add some rim lighting for grass edges
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.worldPos);
                float rim = 1.0 - saturate(dot(normal, viewDir));
                rim = smoothstep(0.6, 1.0, rim);
                finalColor += rim * float3(0.2, 0.4, 0.1) * 0.5;
                
                // Apply distance fade to final alpha
                return float4(finalColor, grassTex.a);
            }

            ENDHLSL
        }
        
        // Shadow caster pass so grass casts shadows
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float distanceFade : TEXCOORD1;
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

                // Apply same transformations for consistent shadows
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                
                // Calculate distance fade
                float distanceFade = CalculateDistanceFade(worldPos);
                
                // Early exit if completely faded
                if (distanceFade <= 0.0)
                {
                    output.positionCS = float4(0, 0, 0, -1);
                    return output;
                }
                
                float heightMultiplier = CalculateGrassHeightMultiplier(worldPos);
                
                float3 scaledPos = input.positionOS.xyz;
                scaledPos.y *= _GrassHeight * heightMultiplier;
                worldPos = TransformObjectToWorld(scaledPos);
                
                float heightFactor = input.uv.y;
                float3 windOffset = CalculateCohesiveWind(worldPos, _Time.y, heightFactor);
                float3 naturalBend = float3(_GrassBend * heightFactor * 0.1, 0, 0);
                float3 finalWorldPos = worldPos + windOffset + naturalBend;
                
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - finalWorldPos);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(finalWorldPos, normalWS, lightDirectionWS));
                output.uv = TRANSFORM_TEX(input.uv, _GrassTexture);
                output.distanceFade = distanceFade;

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
                
                // Sample alpha for shadow cutout
                float4 grassTex = SAMPLE_TEXTURE2D(_GrassTexture, sampler_GrassTexture, input.uv);
                
                // Apply distance fade to shadow alpha
                float finalAlpha = grassTex.a * input.distanceFade;
                
                // Use dynamic cutoff for shadows too
                float dynamicCutoff = input.distanceFade > 0.99 ? _Cutoff : _Cutoff * 0.3;
                clip(finalAlpha - dynamicCutoff);
                
                return 0;
            }

            ENDHLSL
        }
    }
}
