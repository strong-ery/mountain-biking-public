using UnityEngine;
using UnityEditor;

namespace MilklessCereal.Editor
{
    public class TerrainTriplanarToonShaderGUI : ShaderGUI
    {
        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;
        
        // Foldout states
        private static bool showLayer1 = true;
        private static bool showLayer2 = true;
        private static bool showLayer3 = true;
        private static bool showLayer4 = true;
        private static bool showTriplanar = true;
        private static bool showToonShading = true;
        private static bool showRimLight = true;
        private static bool showHighlight = true;
        private static bool showRenderSettings = true;

        // Color scheme
        private static readonly Color headerColor = new Color(0.3f, 0.7f, 1f, 0.8f);
        private static readonly Color layerColor1 = new Color(1f, 0.4f, 0.4f, 0.3f);
        private static readonly Color layerColor2 = new Color(0.4f, 1f, 0.4f, 0.3f);
        private static readonly Color layerColor3 = new Color(0.4f, 0.4f, 1f, 0.3f);
        private static readonly Color layerColor4 = new Color(1f, 0.4f, 1f, 0.3f);
        private static readonly Color toonColor = new Color(1f, 0.8f, 0.2f, 0.3f);
        private static readonly Color lightingColor = new Color(0.8f, 1f, 0.2f, 0.3f);

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            this.materialEditor = materialEditor;
            this.properties = properties;

            EditorGUILayout.Space(10);
            DrawHeader("🏔️ Toon Terrain Multi-Texture Triplanar");
            EditorGUILayout.Space(10);

            // Info box
            EditorGUILayout.HelpBox("This shader blends 4 textures with toon shading and normal maps:\n" +
                                   "• Red channel = Layer 1\n" +
                                   "• Green channel = Layer 2\n" +
                                   "• Blue channel = Layer 3\n" +
                                   "• Black/White areas = Layer 4 (calculated from RGB)\n\n" +
                                   "Features: Hard shadows, rim lighting, highlights, triplanar normal mapping", MessageType.Info);
            EditorGUILayout.Space(10);

            // Render Settings
            DrawSection("⚙️ Render Settings", ref showRenderSettings, headerColor, () =>
            {
                materialEditor.RenderQueueField();
                materialEditor.EnableInstancingField();
                materialEditor.DoubleSidedGIField();
            });

            // Texture Layers
            DrawTextureLayer("🔴 Layer 1 (Red Channel)", "_Layer1_BaseMap", "_Layer1_NormalMap", "_Layer1_NormalScale", "_Layer1_Tiling", ref showLayer1, layerColor1);
            DrawTextureLayer("🟢 Layer 2 (Green Channel)", "_Layer2_BaseMap", "_Layer2_NormalMap", "_Layer2_NormalScale", "_Layer2_Tiling", ref showLayer2, layerColor2);
            DrawTextureLayer("🔵 Layer 3 (Blue Channel)", "_Layer3_BaseMap", "_Layer3_NormalMap", "_Layer3_NormalScale", "_Layer3_Tiling", ref showLayer3, layerColor3);
            DrawTextureLayer("⚪ Layer 4 (Black/White Areas)", "_Layer4_BaseMap", "_Layer4_NormalMap", "_Layer4_NormalScale", "_Layer4_Tiling", ref showLayer4, layerColor4);

            // Triplanar Settings
            DrawSection("🔄 Triplanar Settings", ref showTriplanar, headerColor, () =>
            {
                DrawProperty("_TriplanarBlendSharpness", "Blend Sharpness");
                EditorGUILayout.HelpBox("Higher values = sharper transitions between projection planes.\n" +
                                       "Applies to both albedo and normal map sampling.", MessageType.Info);
            });

            // Toon Shading Settings
            DrawSection("🎨 Toon Shading", ref showToonShading, toonColor, () =>
            {
                DrawProperty("_ShadowThreshold", "Shadow Threshold");
                DrawProperty("_ShadowSmoothness", "Shadow Smoothness");
                DrawColorProperty("_ShadowColor", "Shadow Tint");
                DrawProperty("_AmbientStrength", "Ambient Strength");
                
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Shadow Threshold controls where shadows appear.\n" +
                                       "Shadow Smoothness controls how hard/soft the shadow edges are.\n" +
                                       "Shadow Tint adds color to shadowed areas.\n" +
                                       "Normal maps will affect shadow calculations!", MessageType.Info);
            });

            // Rim Light Settings
            DrawSection("✨ Rim Light", ref showRimLight, lightingColor, () =>
            {
                DrawColorProperty("_RimColor", "Rim Color");
                DrawProperty("_RimPower", "Rim Power");
                DrawProperty("_RimIntensity", "Rim Intensity");
                
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Rim Power controls the width of the rim effect.\n" +
                                       "Higher values = thinner rim.\n" +
                                       "Normal maps will affect rim lighting!", MessageType.Info);
            });

            // Highlight Settings
            DrawSection("💫 Highlight", ref showHighlight, lightingColor, () =>
            {
                DrawColorProperty("_HighlightColor", "Highlight Color");
                DrawProperty("_HighlightThreshold", "Highlight Threshold");
                DrawProperty("_HighlightSmoothness", "Highlight Smoothness");
                DrawProperty("_HighlightIntensity", "Highlight Intensity");
                
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Highlight Threshold controls where highlights appear.\n" +
                                       "Higher values = smaller, sharper highlights.\n" +
                                       "Normal maps will create more detailed highlight patterns!", MessageType.Info);
            });

            EditorGUILayout.Space(10);
            DrawFooter();
        }

        private void DrawHeader(string title)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                normal = { textColor = Color.white }
            };

            var rect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1f));
            
            var gradient = new Rect(rect.x, rect.y, rect.width, 4);
            EditorGUI.DrawRect(gradient, headerColor);
            
            EditorGUI.LabelField(rect, title, style);
        }

        private void DrawFooter()
        {
            var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                normal = { textColor = Color.gray }
            };
            EditorGUILayout.LabelField("Enhanced Toon Terrain Shader with Normal Maps - Created with ❤️ by MilklessCereal", style);
        }

        private void DrawTextureLayer(string layerName, string baseMapProp, string normalMapProp, string normalScaleProp, string tilingProp, ref bool foldout, Color color)
        {
            DrawSection(layerName, ref foldout, color, () =>
            {
                // Base Map
                DrawTextureProperty(baseMapProp, "Base Map");
                
                EditorGUILayout.Space(3);
                
                // Normal Map with scale
                var normalMapProperty = FindProperty(normalMapProp, properties, false);
                var normalScaleProperty = FindProperty(normalScaleProp, properties, false);
                
                if (normalMapProperty != null && normalScaleProperty != null)
                {
                    materialEditor.TexturePropertySingleLine(
                        new GUIContent("Normal Map", "Normal map for surface detail"),
                        normalMapProperty,
                        normalScaleProperty
                    );
                    
                    // Show warning if normal map is not set to Normal map type
                    if (normalMapProperty.textureValue != null)
                    {
                        var texture = normalMapProperty.textureValue as Texture2D;
                        if (texture != null)
                        {
                            var assetPath = AssetDatabase.GetAssetPath(texture);
                            var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                            if (textureImporter != null && textureImporter.textureType != TextureImporterType.NormalMap)
                            {
                                EditorGUILayout.HelpBox("Texture should be set to 'Normal map' in import settings for best results.", MessageType.Warning);
                                if (GUILayout.Button("Fix Import Settings"))
                                {
                                    textureImporter.textureType = TextureImporterType.NormalMap;
                                    textureImporter.SaveAndReimport();
                                }
                            }
                        }
                    }
                }
                
                EditorGUILayout.Space(5);
                DrawProperty(tilingProp, "Tiling");
                
                // Show helpful info about this layer
                EditorGUILayout.Space(3);
                string layerInfo = "";
                if (layerName.Contains("Red"))
                    layerInfo = "This layer appears where vertex colors have strong red values";
                else if (layerName.Contains("Green"))
                    layerInfo = "This layer appears where vertex colors have strong green values";
                else if (layerName.Contains("Blue"))
                    layerInfo = "This layer appears where vertex colors have strong blue values";
                else if (layerName.Contains("White"))
                    layerInfo = "This layer appears in black areas and blended white areas of vertex colors";
                
                if (!string.IsNullOrEmpty(layerInfo))
                {
                    EditorGUILayout.HelpBox(layerInfo, MessageType.None);
                }
            });
        }

        private void DrawSection(string title, ref bool foldout, Color color, System.Action content)
        {
            EditorGUILayout.Space(5);
            
            var rect = EditorGUILayout.GetControlRect(false, 25);
            EditorGUI.DrawRect(rect, color);
            
            var foldoutRect = new Rect(rect.x + 5, rect.y, rect.width - 10, rect.height);
            
            var style = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            
            foldout = EditorGUI.Foldout(foldoutRect, foldout, title, style);
            
            if (foldout)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUI.indentLevel++;
                content?.Invoke();
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.Space(2);
        }

        private void DrawProperty(string propertyName, string displayName)
        {
            var property = FindProperty(propertyName, properties, false);
            if (property != null)
            {
                materialEditor.ShaderProperty(property, displayName);
            }
        }

        private void DrawColorProperty(string propertyName, string displayName)
        {
            var property = FindProperty(propertyName, properties, false);
            if (property != null)
            {
                materialEditor.ColorProperty(property, displayName);
            }
        }

        private void DrawTextureProperty(string propertyName, string displayName)
        {
            var property = FindProperty(propertyName, properties, false);
            if (property != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent(displayName), property);
            }
        }

        // Additional helper methods for better UI feedback
        private void DrawNormalMapValidation()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("💡 Normal Map Tips:\n" +
                                   "• Set normal maps to 'Normal map' type in import settings\n" +
                                   "• Use Normal Scale to control bump intensity\n" +
                                   "• Normal maps blend with vertex color weights\n" +
                                   "• Triplanar projection eliminates UV stretching", MessageType.Info);
        }
    }
}