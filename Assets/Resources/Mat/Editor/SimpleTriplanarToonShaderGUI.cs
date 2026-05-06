using UnityEngine;
using UnityEditor;

namespace MilklessCereal.Editor
{
    public class SimpleTriplanarToonShaderGUI : ShaderGUI
    {
        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;
        
        // Foldout states
        private static bool showTextures = true;
        private static bool showTriplanar = true;
        private static bool showToonShading = true;
        private static bool showRimLight = true;
        private static bool showHighlight = true;
        private static bool showRenderSettings = false;

        // Color scheme
        private static readonly Color headerColor = new Color(0.3f, 0.7f, 1f, 0.8f);
        private static readonly Color textureColor = new Color(0.6f, 0.8f, 1f, 0.3f);
        private static readonly Color toonColor = new Color(1f, 0.8f, 0.2f, 0.3f);
        private static readonly Color lightingColor = new Color(0.8f, 1f, 0.2f, 0.3f);
        private static readonly Color settingsColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            this.materialEditor = materialEditor;
            this.properties = properties;

            EditorGUILayout.Space(10);
            DrawHeader("🎨 Simple Triplanar Toon Shader");
            EditorGUILayout.Space(10);

            // Info box
            EditorGUILayout.HelpBox("A simplified toon shader with triplanar projection - perfect for objects that need seamless texture mapping without UV coordinates!\n\n" +
                                   "Features:\n" +
                                   "• Base color with texture multiplying\n" +
                                   "• Single albedo and normal map\n" +
                                   "• Triplanar projection (no UV stretching)\n" +
                                   "• Toon shading with hard shadows\n" +
                                   "• Rim lighting and specular highlights\n" +
                                   "• Full normal map support", MessageType.Info);
            EditorGUILayout.Space(10);

            // Main Textures
            DrawSection("📸 Textures & Color", ref showTextures, textureColor, () =>
            {
                // Base color and texture on the same line (like Unity's built-in materials)
                var baseMapProperty = FindProperty("_BaseMap", properties, false);
                var baseColorProperty = FindProperty("_BaseColor", properties, false);
                
                if (baseMapProperty != null && baseColorProperty != null)
                {
                    materialEditor.TexturePropertySingleLine(
                        new GUIContent("Base Map", "Base texture (multiplied with Base Color)"),
                        baseMapProperty,
                        baseColorProperty
                    );
                    
                    // Show texture info if available
                    if (baseMapProperty.textureValue != null)
                    {
                        var texture = baseMapProperty.textureValue;
                        EditorGUI.indentLevel++;
                        var infoStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            normal = { textColor = Color.gray }
                        };
                        EditorGUILayout.LabelField($"Size: {texture.width}x{texture.height}", infoStyle);
                        EditorGUI.indentLevel--;
                    }
                }
                
                EditorGUILayout.Space(8);
                
                // Normal Map with scale
                var normalMapProperty = FindProperty("_NormalMap", properties, false);
                var normalScaleProperty = FindProperty("_NormalScale", properties, false);
                
                if (normalMapProperty != null && normalScaleProperty != null)
                {
                    materialEditor.TexturePropertySingleLine(
                        new GUIContent("Normal Map", "Normal map for surface detail - works with triplanar projection!"),
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
                                EditorGUILayout.HelpBox("⚠️ Texture should be set to 'Normal map' in import settings for best results.", MessageType.Warning);
                                if (GUILayout.Button("Fix Import Settings"))
                                {
                                    textureImporter.textureType = TextureImporterType.NormalMap;
                                    textureImporter.SaveAndReimport();
                                }
                            }
                        }
                    }
                }
                
                EditorGUILayout.Space(8);
                DrawProperty("_Tiling", "Texture Tiling");
                
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("💡 Base Color is multiplied with the Base Map texture.\n" +
                                       "If no texture is assigned, only the Base Color is used.\n" +
                                       "Tiling affects both albedo and normal maps.\n" +
                                       "Higher values = smaller, more detailed textures.", MessageType.Info);
            });

            // Triplanar Settings
            DrawSection("🔄 Triplanar Projection", ref showTriplanar, headerColor, () =>
            {
                DrawProperty("_TriplanarBlendSharpness", "Blend Sharpness");
                
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Triplanar projection eliminates UV stretching by projecting textures from 3 directions (X, Y, Z).\n\n" +
                                       "• Lower sharpness = smoother blending between projections\n" +
                                       "• Higher sharpness = sharper transitions\n" +
                                       "• Works perfectly for rocks, terrain, and organic shapes!", MessageType.Info);
            });

            // Toon Shading Settings
            DrawSection("🌟 Toon Shading", ref showToonShading, toonColor, () =>
            {
                DrawProperty("_ShadowThreshold", "Shadow Threshold");
                EditorGUILayout.Space(2);
                DrawProperty("_ShadowSmoothness", "Shadow Edge Softness");
                EditorGUILayout.Space(2);
                DrawColorProperty("_ShadowColor", "Shadow Tint");
                EditorGUILayout.Space(5);
                DrawProperty("_AmbientStrength", "Ambient Light");
                
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox("🎭 Toon Shading Controls:\n" +
                                       "• Shadow Threshold: Where shadows begin (higher = less shadow)\n" +
                                       "• Shadow Softness: How hard/soft the shadow edges are\n" +
                                       "• Shadow Tint: Color mixed into shadowed areas\n" +
                                       "• Ambient: Base lighting in all areas\n\n" +
                                       "✨ Normal maps will create detailed shadow patterns!", MessageType.Info);
            });

            // Rim Light Settings
            DrawSection("✨ Rim Lighting", ref showRimLight, lightingColor, () =>
            {
                DrawColorProperty("_RimColor", "Rim Color");
                EditorGUILayout.Space(2);
                DrawProperty("_RimPower", "Rim Width");
                EditorGUILayout.Space(2);
                DrawProperty("_RimIntensity", "Rim Intensity");
                
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox("🌅 Rim Lighting adds glow around object edges:\n" +
                                       "• Higher Width values = thinner rim\n" +
                                       "• Lower Width values = wider rim\n" +
                                       "• Normal maps affect rim lighting calculations!", MessageType.Info);
            });

            // Highlight Settings
            DrawSection("💫 Specular Highlights", ref showHighlight, lightingColor, () =>
            {
                DrawColorProperty("_HighlightColor", "Highlight Color");
                EditorGUILayout.Space(2);
                DrawProperty("_HighlightThreshold", "Highlight Size");
                EditorGUILayout.Space(2);
                DrawProperty("_HighlightSmoothness", "Highlight Edge Softness");
                EditorGUILayout.Space(2);
                DrawProperty("_HighlightIntensity", "Highlight Intensity");
                
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox("⚡ Specular Highlights simulate shiny reflections:\n" +
                                       "• Higher Threshold = smaller, sharper highlights\n" +
                                       "• Lower Threshold = larger, softer highlights\n" +
                                       "• Normal maps create detailed highlight patterns!\n" +
                                       "• Only visible in lit areas (not in shadows)", MessageType.Info);
            });

            // Advanced Render Settings (collapsed by default)
            DrawSection("⚙️ Advanced Settings", ref showRenderSettings, settingsColor, () =>
            {
                materialEditor.RenderQueueField();
                materialEditor.EnableInstancingField();
                materialEditor.DoubleSidedGIField();
                
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("These settings control how Unity renders this material.\n" +
                                       "Usually you don't need to change these.", MessageType.Info);
            });

            EditorGUILayout.Space(15);
            DrawFooter();
            
            // Quick Tips Section
            EditorGUILayout.Space(10);
            DrawQuickTips();
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
            EditorGUILayout.LabelField("Simple Triplanar Toon Shader - Created with ❤️ by MilklessCereal", style);
        }

        private void DrawQuickTips()
        {
            EditorGUILayout.BeginVertical("box");
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.8f, 0.9f, 1f) }
            };
            EditorGUILayout.LabelField("🚀 Quick Tips", style);
            EditorGUILayout.Space(3);
            
            var tipStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                normal = { textColor = new Color(0.7f, 0.8f, 0.9f) }
            };
            
            EditorGUILayout.LabelField("• Set Base Color for solid colors, or combine with textures", tipStyle);
            EditorGUILayout.LabelField("• Perfect for rocks, cliffs, and organic shapes without UVs", tipStyle);
            EditorGUILayout.LabelField("• Use normal maps to add surface detail and enhance toon shading", tipStyle);
            EditorGUILayout.LabelField("• Adjust Shadow Threshold to control the cartoon look", tipStyle);
            EditorGUILayout.LabelField("• Rim lighting makes objects pop against backgrounds", tipStyle);
            EditorGUILayout.LabelField("• Higher tiling values work great for detailed stone/bark textures", tipStyle);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawSection(string title, ref bool foldout, Color color, System.Action content)
        {
            EditorGUILayout.Space(5);
            
            var rect = EditorGUILayout.GetControlRect(false, 28);
            EditorGUI.DrawRect(rect, color);
            
            // Add a subtle border
            var borderRect = new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1);
            EditorGUI.DrawRect(borderRect, new Color(0, 0, 0, 0.3f));
            
            var foldoutRect = new Rect(rect.x + 8, rect.y, rect.width - 16, rect.height);
            
            var style = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                normal = { textColor = Color.white },
                onNormal = { textColor = Color.white },
                hover = { textColor = Color.white },
                onHover = { textColor = Color.white },
                focused = { textColor = Color.white },
                onFocused = { textColor = Color.white }
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

        private void DrawProperty(string propertyName, string displayName, string tooltip = "")
        {
            var property = FindProperty(propertyName, properties, false);
            if (property != null)
            {
                var content = string.IsNullOrEmpty(tooltip) ? 
                    new GUIContent(displayName) : 
                    new GUIContent(displayName, tooltip);
                materialEditor.ShaderProperty(property, content);
            }
        }

        private void DrawColorProperty(string propertyName, string displayName, string tooltip = "")
        {
            var property = FindProperty(propertyName, properties, false);
            if (property != null)
            {
                var content = string.IsNullOrEmpty(tooltip) ? 
                    new GUIContent(displayName) : 
                    new GUIContent(displayName, tooltip);
                
                // Use ShaderProperty instead of ColorProperty to support GUIContent with tooltips
                materialEditor.ShaderProperty(property, content);
            }
        }

        private void DrawTextureProperty(string propertyName, string displayName, string tooltip = "")
        {
            var property = FindProperty(propertyName, properties, false);
            if (property != null)
            {
                var content = string.IsNullOrEmpty(tooltip) ? 
                    new GUIContent(displayName) : 
                    new GUIContent(displayName, tooltip);
                materialEditor.TexturePropertySingleLine(content, property);
                
                // Show texture info if available
                if (property.textureValue != null)
                {
                    var texture = property.textureValue;
                    EditorGUI.indentLevel++;
                    var infoStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = Color.gray }
                    };
                    EditorGUILayout.LabelField($"Size: {texture.width}x{texture.height}", infoStyle);
                    EditorGUI.indentLevel--;
                }
            }
        }

        // Helper method to show material preview
        private void ShowMaterialPreview()
        {
            EditorGUILayout.Space(10);
            
            if (materialEditor.target != null)
            {
                var previewRect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true));
                materialEditor.OnPreviewGUI(previewRect, "box");
            }
        }
    }
}