using UnityEngine;
using UnityEditor;

namespace MilklessCereal.Editor
{
    public class LeafToonShaderGUI : ShaderGUI
    {
        private static class Styles
        {
            public static readonly GUIContent mainTexturesText = EditorGUIUtility.TrTextContent("Main Textures");
            public static readonly GUIContent alphaText = EditorGUIUtility.TrTextContent("Alpha Cutout");
            public static readonly GUIContent toonShadingText = EditorGUIUtility.TrTextContent("Toon Shading");
            public static readonly GUIContent rimLightText = EditorGUIUtility.TrTextContent("Rim Light");
            public static readonly GUIContent highlightText = EditorGUIUtility.TrTextContent("Highlight");
            public static readonly GUIContent ambientText = EditorGUIUtility.TrTextContent("Ambient");
            public static readonly GUIContent windAnimationText = EditorGUIUtility.TrTextContent("Wind Animation");
            public static readonly GUIContent bushPropertiesText = EditorGUIUtility.TrTextContent("Bush Properties");
            public static readonly GUIContent renderingText = EditorGUIUtility.TrTextContent("Rendering");
            public static readonly GUIContent debugText = EditorGUIUtility.TrTextContent("Material State");
            
            public static readonly GUIContent baseMapText = EditorGUIUtility.TrTextContent("Base Map");
            public static readonly GUIContent baseColorText = EditorGUIUtility.TrTextContent("Base Color");
            public static readonly GUIContent normalMapText = EditorGUIUtility.TrTextContent("Normal Map");
            public static readonly GUIContent normalScaleText = EditorGUIUtility.TrTextContent("Normal Scale");
            
            public static readonly GUIContent cutoffText = EditorGUIUtility.TrTextContent("Alpha Cutoff");
            
            public static readonly GUIContent shadowThresholdText = EditorGUIUtility.TrTextContent("Shadow Threshold");
            public static readonly GUIContent shadowSmoothnessText = EditorGUIUtility.TrTextContent("Shadow Smoothness");
            public static readonly GUIContent shadowColorText = EditorGUIUtility.TrTextContent("Shadow Tint");
            
            public static readonly GUIContent rimColorText = EditorGUIUtility.TrTextContent("Rim Color");
            public static readonly GUIContent rimPowerText = EditorGUIUtility.TrTextContent("Rim Power");
            public static readonly GUIContent rimIntensityText = EditorGUIUtility.TrTextContent("Rim Intensity");
            
            public static readonly GUIContent highlightColorText = EditorGUIUtility.TrTextContent("Highlight Color");
            public static readonly GUIContent highlightThresholdText = EditorGUIUtility.TrTextContent("Highlight Threshold");
            public static readonly GUIContent highlightSmoothnessText = EditorGUIUtility.TrTextContent("Highlight Smoothness");
            public static readonly GUIContent highlightIntensityText = EditorGUIUtility.TrTextContent("Highlight Intensity");
            
            public static readonly GUIContent ambientStrengthText = EditorGUIUtility.TrTextContent("Ambient Strength");
            
            public static readonly GUIContent enableWindText = EditorGUIUtility.TrTextContent("Enable Wind");
            public static readonly GUIContent windStrengthText = EditorGUIUtility.TrTextContent("Wind Strength");
            public static readonly GUIContent windSpeedText = EditorGUIUtility.TrTextContent("Wind Speed");
            public static readonly GUIContent windDirectionText = EditorGUIUtility.TrTextContent("Wind Direction");
            public static readonly GUIContent windTurbulenceText = EditorGUIUtility.TrTextContent("Wind Turbulence");
            public static readonly GUIContent windPhaseVariationText = EditorGUIUtility.TrTextContent("Wind Phase Variation");
            
            public static readonly GUIContent vertexColorInfluenceText = EditorGUIUtility.TrTextContent("Vertex Color Wind Influence");
            public static readonly GUIContent bushDensityMaskText = EditorGUIUtility.TrTextContent("Bush Density Mask");
            public static readonly GUIContent densityInfluenceText = EditorGUIUtility.TrTextContent("Density Wind Influence");
            
            public static readonly GUIContent cullModeText = EditorGUIUtility.TrTextContent("Cull Mode");
        }

        private MaterialProperty baseMap;
        private MaterialProperty baseColor;
        private MaterialProperty normalMap;
        private MaterialProperty normalScale;
        
        private MaterialProperty cutoff;
        
        private MaterialProperty shadowThreshold;
        private MaterialProperty shadowSmoothness;
        private MaterialProperty shadowColor;
        
        private MaterialProperty rimColor;
        private MaterialProperty rimPower;
        private MaterialProperty rimIntensity;
        
        private MaterialProperty highlightColor;
        private MaterialProperty highlightThreshold;
        private MaterialProperty highlightSmoothness;
        private MaterialProperty highlightIntensity;
        
        private MaterialProperty ambientStrength;
        
        private MaterialProperty enableWind;
        private MaterialProperty windStrength;
        private MaterialProperty windSpeed;
        private MaterialProperty windDirection;
        private MaterialProperty windTurbulence;
        private MaterialProperty windPhaseVariation;
        
        private MaterialProperty vertexColorInfluence;
        private MaterialProperty bushDensityMask;
        private MaterialProperty densityInfluence;
        
        private MaterialProperty cullMode;
        
        // Editor state
        private bool showMainTextures = true;
        private bool showAlpha = true;
        private bool showToonShading = true;
        private bool showRimLight = true;
        private bool showHighlight = true;
        private bool showAmbient = true;
        private bool showWindAnimation = true;
        private bool showBushProperties = true;
        private bool showRendering = true;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (materialEditor == null)
                throw new System.ArgumentNullException("materialEditor");

            FindProperties(properties);
            Material material = materialEditor.target as Material;
            
            EditorGUI.BeginChangeCheck();
            
            DrawHeader("Toon Bush Cutout Shader", "A stylized toon shader for foliage with binary alpha cutout and wind animation");
            
            EditorGUILayout.Space();
            
            // Main Textures
            showMainTextures = EditorGUILayout.Foldout(showMainTextures, Styles.mainTexturesText, true);
            if (showMainTextures)
            {
                EditorGUI.indentLevel++;
                DrawMainTextureProperties(materialEditor, material);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            
            // Alpha Cutout
            showAlpha = EditorGUILayout.Foldout(showAlpha, Styles.alphaText, true);
            if (showAlpha)
            {
                EditorGUI.indentLevel++;
                DrawAlphaProperties(materialEditor, material);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            
            // Toon Shading
            showToonShading = EditorGUILayout.Foldout(showToonShading, Styles.toonShadingText, true);
            if (showToonShading)
            {
                EditorGUI.indentLevel++;
                DrawToonShadingProperties(materialEditor);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            
            // Rim Light
            showRimLight = EditorGUILayout.Foldout(showRimLight, Styles.rimLightText, true);
            if (showRimLight)
            {
                EditorGUI.indentLevel++;
                DrawRimLightProperties(materialEditor);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            
            // Highlight
            showHighlight = EditorGUILayout.Foldout(showHighlight, Styles.highlightText, true);
            if (showHighlight)
            {
                EditorGUI.indentLevel++;
                DrawHighlightProperties(materialEditor);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            
            // Ambient
            showAmbient = EditorGUILayout.Foldout(showAmbient, Styles.ambientText, true);
            if (showAmbient)
            {
                EditorGUI.indentLevel++;
                DrawAmbientProperties(materialEditor);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            
            // Wind Animation
            showWindAnimation = EditorGUILayout.Foldout(showWindAnimation, Styles.windAnimationText, true);
            if (showWindAnimation)
            {
                EditorGUI.indentLevel++;
                DrawWindAnimationProperties(materialEditor);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            
            // Bush Properties
            showBushProperties = EditorGUILayout.Foldout(showBushProperties, Styles.bushPropertiesText, true);
            if (showBushProperties)
            {
                EditorGUI.indentLevel++;
                DrawBushProperties(materialEditor);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            
            // Rendering
            showRendering = EditorGUILayout.Foldout(showRendering, Styles.renderingText, true);
            if (showRendering)
            {
                EditorGUI.indentLevel++;
                DrawRenderingProperties(materialEditor, material);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            
            // Material State - always visible for debugging
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(Styles.debugText, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            DrawDebugInfo(materialEditor, material);
            EditorGUI.indentLevel--;
            
            if (EditorGUI.EndChangeCheck())
            {
                UpdateMaterialKeywords(material);
            }
        }

        private void FindProperties(MaterialProperty[] props)
        {
            baseMap = FindProperty("_BaseMap", props);
            baseColor = FindProperty("_BaseColor", props);
            normalMap = FindProperty("_NormalMap", props);
            normalScale = FindProperty("_NormalScale", props);
            
            cutoff = FindProperty("_Cutoff", props);
            
            shadowThreshold = FindProperty("_ShadowThreshold", props);
            shadowSmoothness = FindProperty("_ShadowSmoothness", props);
            shadowColor = FindProperty("_ShadowColor", props);
            
            rimColor = FindProperty("_RimColor", props);
            rimPower = FindProperty("_RimPower", props);
            rimIntensity = FindProperty("_RimIntensity", props);
            
            highlightColor = FindProperty("_HighlightColor", props);
            highlightThreshold = FindProperty("_HighlightThreshold", props);
            highlightSmoothness = FindProperty("_HighlightSmoothness", props);
            highlightIntensity = FindProperty("_HighlightIntensity", props);
            
            ambientStrength = FindProperty("_AmbientStrength", props);
            
            enableWind = FindProperty("_EnableWind", props);
            windStrength = FindProperty("_WindStrength", props);
            windSpeed = FindProperty("_WindSpeed", props);
            windDirection = FindProperty("_WindDirection", props);
            windTurbulence = FindProperty("_WindTurbulence", props);
            windPhaseVariation = FindProperty("_WindPhaseVariation", props);
            
            vertexColorInfluence = FindProperty("_VertexColorInfluence", props);
            bushDensityMask = FindProperty("_BushDensityMask", props);
            densityInfluence = FindProperty("_DensityInfluence", props);
            
            cullMode = FindProperty("_Cull", props);
        }

        private void DrawHeader(string title, string subtitle)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            
            GUIStyle subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic
            };
            
            EditorGUILayout.LabelField(title, titleStyle);
            EditorGUILayout.LabelField(subtitle, subtitleStyle);
            
            GUILayout.EndVertical();
        }

        private void DrawMainTextureProperties(MaterialEditor materialEditor, Material material)
        {
            // Base Map with color
            materialEditor.TexturePropertySingleLine(Styles.baseMapText, baseMap, baseColor);
            
            // Normal Map with scale
            if (normalMap.textureValue != null)
            {
                materialEditor.TexturePropertySingleLine(Styles.normalMapText, normalMap, normalScale);
            }
            else
            {
                materialEditor.TexturePropertySingleLine(Styles.normalMapText, normalMap);
            }
            
            materialEditor.TextureScaleOffsetProperty(baseMap);
        }

        private void DrawAlphaProperties(MaterialEditor materialEditor, Material material)
        {
            materialEditor.ShaderProperty(cutoff, Styles.cutoffText);
            
            // Show alpha cutoff visualization
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Cutoff Preview:", GUILayout.Width(100));
            
            // Visual representation of cutoff threshold
            Rect cutoffRect = EditorGUILayout.GetControlRect(GUILayout.Height(20));
            float cutoffValue = cutoff.floatValue;
            
            // Draw background (transparent part)
            EditorGUI.DrawRect(cutoffRect, new Color(0.8f, 0.8f, 0.8f, 0.3f));
            
            // Draw opaque part
            Rect opaqueRect = new Rect(cutoffRect.x + cutoffRect.width * cutoffValue, cutoffRect.y, 
                                     cutoffRect.width * (1 - cutoffValue), cutoffRect.height);
            EditorGUI.DrawRect(opaqueRect, new Color(0.2f, 0.7f, 0.2f, 1.0f));
            
            // Draw cutoff line
            Rect cutoffLine = new Rect(cutoffRect.x + cutoffRect.width * cutoffValue, cutoffRect.y, 2, cutoffRect.height);
            EditorGUI.DrawRect(cutoffLine, Color.red);
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox("CUTOUT SHADER: Pixels are either fully opaque or fully transparent. " +
                                   "No blending occurs - this ensures clean edges without transparency artifacts. " +
                                   "Red line shows cutoff threshold. Gray = discarded, Green = rendered.", 
                                   MessageType.Info);
        }

        private void DrawToonShadingProperties(MaterialEditor materialEditor)
        {
            materialEditor.ShaderProperty(shadowThreshold, Styles.shadowThresholdText);
            materialEditor.ShaderProperty(shadowSmoothness, Styles.shadowSmoothnessText);
            materialEditor.ShaderProperty(shadowColor, Styles.shadowColorText);
            
            EditorGUILayout.HelpBox("Shadow Threshold controls where shadows begin. " +
                                   "Shadow Smoothness controls the transition between light and shadow. " +
                                   "Lower smoothness = harder shadows.", 
                                   MessageType.Info);
        }

        private void DrawRimLightProperties(MaterialEditor materialEditor)
        {
            materialEditor.ShaderProperty(rimColor, Styles.rimColorText);
            materialEditor.ShaderProperty(rimPower, Styles.rimPowerText);
            materialEditor.ShaderProperty(rimIntensity, Styles.rimIntensityText);
            
            EditorGUILayout.HelpBox("Rim lighting highlights the edges of leaves. " +
                                   "Higher Rim Power = thinner rim. " +
                                   "Great for making foliage pop against backgrounds.", 
                                   MessageType.Info);
        }

        private void DrawHighlightProperties(MaterialEditor materialEditor)
        {
            materialEditor.ShaderProperty(highlightColor, Styles.highlightColorText);
            materialEditor.ShaderProperty(highlightThreshold, Styles.highlightThresholdText);
            materialEditor.ShaderProperty(highlightSmoothness, Styles.highlightSmoothnessText);
            materialEditor.ShaderProperty(highlightIntensity, Styles.highlightIntensityText);
            
            EditorGUILayout.HelpBox("Highlights simulate glossy reflections on leaves. " +
                                   "Higher threshold = smaller, more focused highlights.", 
                                   MessageType.Info);
        }

        private void DrawAmbientProperties(MaterialEditor materialEditor)
        {
            materialEditor.ShaderProperty(ambientStrength, Styles.ambientStrengthText);
            
            EditorGUILayout.HelpBox("Controls how much ambient lighting affects the material. " +
                                   "Lower values make shadows darker.", 
                                   MessageType.Info);
        }

        private void DrawWindAnimationProperties(MaterialEditor materialEditor)
        {
            bool windEnabled = enableWind.floatValue > 0.5f;
            
            EditorGUI.BeginChangeCheck();
            windEnabled = EditorGUILayout.Toggle(Styles.enableWindText, windEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                enableWind.floatValue = windEnabled ? 1.0f : 0.0f;
            }
            
            if (windEnabled)
            {
                EditorGUI.indentLevel++;
                
                materialEditor.ShaderProperty(windStrength, Styles.windStrengthText);
                materialEditor.ShaderProperty(windSpeed, Styles.windSpeedText);
                
                // Wind direction as Vector3
                Vector4 windDir = windDirection.vectorValue;
                Vector3 windDir3 = new Vector3(windDir.x, windDir.y, windDir.z);
                
                EditorGUI.BeginChangeCheck();
                windDir3 = EditorGUILayout.Vector3Field(Styles.windDirectionText, windDir3);
                if (EditorGUI.EndChangeCheck())
                {
                    windDirection.vectorValue = new Vector4(windDir3.x, windDir3.y, windDir3.z, 0);
                }
                
                materialEditor.ShaderProperty(windTurbulence, Styles.windTurbulenceText);
                materialEditor.ShaderProperty(windPhaseVariation, Styles.windPhaseVariationText);
                
                EditorGUI.indentLevel--;
                
                EditorGUILayout.HelpBox("Wind animation uses vertex colors (red channel) to control movement intensity. " +
                                       "Phase Variation prevents synchronized movement across different bushes. " +
                                       "Turbulence adds secondary motion for more natural movement.", 
                                       MessageType.Info);
            }
        }

        private void DrawBushProperties(MaterialEditor materialEditor)
        {
            materialEditor.ShaderProperty(vertexColorInfluence, Styles.vertexColorInfluenceText);
            materialEditor.TexturePropertySingleLine(Styles.bushDensityMaskText, bushDensityMask);
            materialEditor.ShaderProperty(densityInfluence, Styles.densityInfluenceText);
            
            EditorGUILayout.HelpBox("Vertex Color Influence: Red channel controls wind movement intensity. " +
                                   "Density Mask: Texture to vary wind influence across the surface. " +
                                   "Paint vertex colors with red = high wind influence, black = no wind.", 
                                   MessageType.Info);
        }

        private void DrawRenderingProperties(MaterialEditor materialEditor, Material material)
        {
            materialEditor.ShaderProperty(cullMode, Styles.cullModeText);
            
            // Use Unity's standard render queue control
            materialEditor.RenderQueueField();
            
            string cullModeHelp = "";
            int cullValue = (int)cullMode.floatValue;
            switch (cullValue)
            {
                case 0: cullModeHelp = "Off - Renders both sides (recommended for leaves)"; break;
                case 1: cullModeHelp = "Front - Only back faces rendered"; break;
                case 2: cullModeHelp = "Back - Only front faces rendered"; break;
            }
            
            EditorGUILayout.HelpBox($"Cull Mode: {cullModeHelp}\n\nThis is a CUTOUT shader with NO BLENDING. " +
                                   "Use 'AlphaTest' render queue for proper cutout behavior.", 
                                   MessageType.Info);
            
            // Force cutout button
            if (GUILayout.Button("Force Cutout Settings"))
            {
                // ForceCutoutSettings(material);
                materialEditor.Repaint();
            }
        }

        private void DrawDebugInfo(MaterialEditor materialEditor, Material material)
        {
            EditorGUILayout.LabelField("Current Render Queue:", material.renderQueue.ToString());
            EditorGUILayout.LabelField("Render Type Tag:", material.GetTag("RenderType", false));
            
            if (material.HasProperty("_SrcBlend"))
                EditorGUILayout.LabelField("Src Blend:", material.GetFloat("_SrcBlend").ToString());
            if (material.HasProperty("_DstBlend"))
                EditorGUILayout.LabelField("Dst Blend:", material.GetFloat("_DstBlend").ToString());
            if (material.HasProperty("_ZWrite"))
                EditorGUILayout.LabelField("Z Write:", material.GetFloat("_ZWrite").ToString());
                
            EditorGUILayout.LabelField("Alpha Test Enabled:", material.IsKeywordEnabled("_ALPHATEST_ON").ToString());
            EditorGUILayout.LabelField("Alpha Blend Enabled:", material.IsKeywordEnabled("_ALPHABLEND_ON").ToString());
            
            // Color-coded status
            if (material.renderQueue >= 3000)
            {
                EditorGUILayout.HelpBox("WARNING: Render queue is in transparent range! Use 'AlphaTest' for cutout.", 
                                       MessageType.Error);
            }
            else if (material.renderQueue == 2450)
            {
                EditorGUILayout.HelpBox("Good: Render queue is set for alpha test/cutout.", 
                                       MessageType.Info);
            }
            else if (material.renderQueue == 2000)
            {
                EditorGUILayout.HelpBox("Render queue is opaque. This might work but 'AlphaTest' is better for cutout.", 
                                       MessageType.Warning);
            }
        }

        private void UpdateMaterialKeywords(Material material)
        {
            // AGGRESSIVELY force cutout material settings
            material.EnableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            
            // Set proper render settings for cutout - be very explicit
            material.renderQueue = 2450; // AlphaTest queue explicitly
            material.SetOverrideTag("RenderType", "TransparentCutout");
            
            // FORCE blend mode settings - these should match our Blend One Zero in the shader
            material.SetInt("_Surface", 1);  // Cutout
            material.SetInt("_Blend", 0);    // Alpha
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            
            // Additional URP-specific properties that might interfere
            if (material.HasProperty("_AlphaClip"))
                material.SetFloat("_AlphaClip", 1.0f);
                
            // Force the material to update immediately
            EditorUtility.SetDirty(material);
        }
    }
}