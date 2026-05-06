Shader "Custom/URP/ShadowOnly"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
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
        
        // Forward pass - Make object invisible
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            // Make the object completely invisible
            ColorMask 0  // Don't write to color buffer
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample texture for alpha cutoff support
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // Alpha test for cutout materials
                clip(albedo.a - _Cutoff);
                
                // Return completely transparent
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
        
        // Simple shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
            };
            
            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
            CBUFFER_END
            
            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                
                // Basic transformation with simple bias
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                // Apply a simple normal bias to prevent shadow acne
                positionWS += normalWS * 0.01;
                
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                
                return output;
            }
            
            half4 ShadowFrag(ShadowVaryings input) : SV_TARGET
            {
                // Sample texture for alpha cutoff support
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // Alpha test for cutout materials
                clip(albedo.a - _Cutoff);
                
                return 0;
            }
            
            ENDHLSL
        }
        
        // Depth only pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct DepthAttributes
            {
                float4 position : POSITION;
                float2 texcoord : TEXCOORD0;
            };
            
            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
            CBUFFER_END
            
            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.position.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                
                return output;
            }
            
            half4 DepthFrag(DepthVaryings input) : SV_TARGET
            {
                // Sample texture for alpha cutoff support
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // Alpha test for cutout materials
                clip(albedo.a - _Cutoff);
                
                return 0;
            }
            
            ENDHLSL
        }
    }
}