#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace FriendSlop.Splines
{
    [CustomEditor(typeof(TerrainHeightmapEditor))]
    public class TerrainHeightmapEditorInspector : Editor
    {
        TerrainHeightmapEditor _editor;
        bool _showUtilities = true;
        bool _showBrushPresets = true;
        bool _paintMode = false;

        // Paint mode state
        private bool _isPainting = false;
        private Tool _previousTool;
        
        // Brush preview state
        private Vector3 _lastBrushPosition = Vector3.zero;
        private bool _validBrushPosition = false;

        void OnEnable()
        {
            _editor = (TerrainHeightmapEditor)target;
        }

        void OnDisable()
        {
            if (_paintMode)
            {
                ExitPaintMode();
            }
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space();

            // Data Management - Updated for mesh-based approach
            EditorGUILayout.LabelField("Data Management", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Refresh from Generator", GUILayout.Height(25)))
            {
                bool success = _editor.RefreshMeshData();
                if (success)
                {
                    EditorUtility.DisplayDialog("Success", "Mesh data refreshed from Mountain Generator.", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Failed to refresh mesh data. Make sure a Mountain Generator is assigned and has generated terrain with a valid mesh.", "OK");
                }
            }

            if (GUILayout.Button("Apply to Generator", GUILayout.Height(25)))
            {
                _editor.RecordUndoState("Apply Mesh Changes");
                bool success = _editor.ApplyMeshToGenerator();
                if (success)
                {
                    EditorUtility.DisplayDialog("Success", "Modified mesh applied to Mountain Generator.", "OK");
                    SceneView.RepaintAll();
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Failed to apply mesh data.", "OK");
                }
            }

            EditorGUILayout.EndHorizontal();

            // Status info - Updated for mesh data
            if (_editor.HasValidData())
            {
                var bounds = _editor.GetTerrainBounds();
                EditorGUILayout.HelpBox($"✓ Mesh loaded with vertex data\n" +
                                      $"Terrain size: {_editor.GetTerrainSize().x}x{_editor.GetTerrainSize().y}\n" +
                                      $"Mesh bounds: {bounds.size.x:F1}x{bounds.size.z:F1}x{bounds.size.y:F1}", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("No mesh data loaded. Click 'Refresh from Generator' to load mesh data.", MessageType.Warning);
            }

            EditorGUILayout.Space();

            // Interactive Painting
            EditorGUILayout.LabelField("Interactive Painting", EditorStyles.boldLabel);

            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = _paintMode ? Color.green : originalColor;

            if (GUILayout.Button(_paintMode ? "Exit Paint Mode" : "Enter Paint Mode", GUILayout.Height(30)))
            {
                if (_paintMode)
                {
                    ExitPaintMode();
                }
                else
                {
                    EnterPaintMode();
                }
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = originalColor;

            if (_paintMode)
            {
                EditorGUILayout.HelpBox(
                    "Paint Mode Active!\n\n" +
                    "Controls:\n" +
                    "• Left Click + Drag: Paint with brush\n" +
                    "• Alt + Left Drag: Camera controls\n" +
                    "• Right Click: Context menu\n" +
                    "• ESC: Exit paint mode\n" +
                    "• Scroll Wheel: Adjust brush size\n" +
                    "• Shift + Scroll: Adjust brush strength\n\n" +
                    "Now using direct mesh raycasting for pixel-perfect accuracy!", 
                    MessageType.Info);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Paint Controls", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Force Refresh View"))
                {
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Reset Camera Control"))
                {
                    Tools.current = Tool.View;
                    Tools.current = Tool.None;
                }
                EditorGUILayout.EndHorizontal();
                
                // Brush preview settings
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Preview Settings", EditorStyles.boldLabel);
                _editor.showBrushPreview = EditorGUILayout.Toggle("Show Brush Preview", _editor.showBrushPreview);
                _editor.showHeightPreview = EditorGUILayout.Toggle("Show Height Preview", _editor.showHeightPreview);
                _editor.previewOpacity = EditorGUILayout.Slider("Preview Opacity", _editor.previewOpacity, 0.1f, 1f);

                // Real-time brush info display
                if (_validBrushPosition)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Current Brush Status:", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Position: {_lastBrushPosition}");
                    EditorGUILayout.LabelField($"Radius: {_editor.brush.radius:F1}");
                    EditorGUILayout.LabelField($"Strength: {_editor.brush.strength:F1}");
                    EditorGUILayout.LabelField($"Type: {_editor.brush.brushType}");
                }
            }

            EditorGUILayout.Space();

            // Brush Presets
            _showBrushPresets = EditorGUILayout.Foldout(_showBrushPresets, "Brush Presets", true, EditorStyles.foldoutHeader);
            if (_showBrushPresets)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Raise Hill\n(R=20, S=10)"))
                {
                    SetBrushPreset(HeightmapBrush.BrushType.Raise, 20f, 10f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
                }

                if (GUILayout.Button("Carve Valley\n(R=15, S=8)"))
                {
                    SetBrushPreset(HeightmapBrush.BrushType.Lower, 15f, 8f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Smooth Terrain\n(R=25, S=5)"))
                {
                    SetBrushPreset(HeightmapBrush.BrushType.Smooth, 25f, 5f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
                }

                if (GUILayout.Button("Add Detail\n(R=8, S=3)"))
                {
                    SetBrushPreset(HeightmapBrush.BrushType.Noise, 8f, 3f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();

            // Utilities - Updated for mesh-based operations
            _showUtilities = EditorGUILayout.Foldout(_showUtilities, "Utilities", true, EditorStyles.foldoutHeader);
            if (_showUtilities)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Mesh analysis instead of height analysis
                EditorGUILayout.LabelField("Mesh Analysis", EditorStyles.boldLabel);
                if (GUILayout.Button("Analyze Vertex Heights"))
                {
                    AnalyzeVertexHeights();
                }

                EditorGUILayout.Space();

                // Export/Import - Updated for mesh format
                EditorGUILayout.LabelField("Export/Import", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Export OBJ"))
                {
                    ExportMeshAsOBJ();
                }

                if (GUILayout.Button("Export Vertices"))
                {
                    ExportVertexData();
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                // Mesh operations
                EditorGUILayout.LabelField("Mesh Operations", EditorStyles.boldLabel);

                if (GUILayout.Button("Recalculate Normals"))
                {
                    RecalculateNormals();
                }

                if (GUILayout.Button("Reset to Original"))
                {
                    if (EditorUtility.DisplayDialog("Reset Mesh",
                        "This will reset all vertex modifications. This cannot be undone.",
                        "Reset", "Cancel"))
                    {
                        ResetToOriginal();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();

            // Usage instructions - Updated
            EditorGUILayout.HelpBox(
                "MESH-BASED HEIGHTMAP EDITOR WORKFLOW:\n\n" +
                "1. Assign a MountainGenerator in 'Source Mountain Generator'\n" +
                "2. Click 'Refresh from Generator' to load mesh data\n" +
                "3. Use global operations for broad changes\n" +
                "4. Enter Paint Mode for detailed brush editing\n" +
                "5. Click 'Apply to Generator' to update the mesh\n\n" +
                "PAINT MODE ADVANTAGES:\n" +
                "• Direct mesh raycasting - no coordinate conversion errors\n" +
                "• Works with actual vertex positions\n" +
                "• Automatic mesh collider updates\n" +
                "• Pixel-perfect brush positioning\n" +
                "• Real-time visual feedback",
                MessageType.Info
            );

            if (changed)
            {
                EditorUtility.SetDirty(_editor);
            }
        }

        private void SetBrushPreset(HeightmapBrush.BrushType type, float radius, float strength, AnimationCurve curve)
        {
            _editor.brush.brushType = type;
            _editor.brush.radius = radius;
            _editor.brush.strength = strength;
            _editor.brush.falloffCurve = curve;
            EditorUtility.SetDirty(_editor);
        }

        private void EnterPaintMode()
        {
            _paintMode = true;
            _previousTool = Tools.current;
            Tools.current = Tool.None;
            
            SceneView.duringSceneGui += OnSceneGUICallback;
            
            EditorUtility.DisplayDialog("Paint Mode Enabled",
                "Paint Mode Active!\n\n" +
                "Controls:\n" +
                "• Left Click + Drag: Paint terrain\n" +
                "• Alt + Left Click + Drag: Move camera\n" +
                "• Right Click: Context menu\n" +
                "• ESC Key: Exit paint mode\n" +
                "• Scroll Wheel: Adjust brush size\n" +
                "• Shift + Scroll: Adjust brush strength\n\n" +
                "Now using direct mesh vertex manipulation for perfect accuracy!", "Got it!");
        }

        private void ExitPaintMode()
        {
            _paintMode = false;
            _isPainting = false;
            Tools.current = _previousTool;
            
            SceneView.duringSceneGui -= OnSceneGUICallback;
        }

        private void OnSceneGUICallback(SceneView sceneView)
        {
            if (_paintMode)
            {
                HandlePaintModeSceneGUI();
            }
        }

        private void HandlePaintModeSceneGUI()
        {
            if (!_paintMode || !_editor.HasValidData()) return;

            Event e = Event.current;
            
            // Handle ESC key to exit paint mode
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                ExitPaintMode();
                e.Use();
                return;
            }

            // Handle scroll wheel for brush size and strength adjustment
            if (e.type == EventType.ScrollWheel && !e.alt)
            {
                float scrollDelta = -e.delta.y;
                
                if (e.shift)
                {
                    _editor.brush.strength = Mathf.Max(0.1f, _editor.brush.strength + scrollDelta * 0.5f);
                    Debug.Log($"Brush Strength: {_editor.brush.strength:F1}");
                }
                else
                {
                    _editor.brush.radius = Mathf.Max(0.5f, _editor.brush.radius + scrollDelta * 0.5f);
                    Debug.Log($"Brush Radius: {_editor.brush.radius:F1}");
                }
                
                EditorUtility.SetDirty(_editor);
                e.Use();
                SceneView.RepaintAll();
                // Force inspector to repaint
                Repaint();
                return;
            }

            // Don't interfere with Alt+drag (camera controls) or right mouse button
            if (e.alt || e.button == 1) return;

            // Get mouse world position using direct mesh raycasting
            Vector3 mouseWorldPos = GetMouseWorldPositionOnTerrain(e.mousePosition);
            
            if (mouseWorldPos != Vector3.zero)
            {
                _validBrushPosition = true;
                _lastBrushPosition = mouseWorldPos;
            }
            else
            {
                _validBrushPosition = false;
            }

            // Draw brush preview
            if (_validBrushPosition && _editor.showBrushPreview)
            {
                DrawBrushPreview(_lastBrushPosition);
            }

            // Handle painting input
            bool shouldPaint = false;

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                if (_validBrushPosition)
                {
                    _isPainting = true;
                    shouldPaint = true;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && _isPainting && !e.alt)
            {
                if (_validBrushPosition)
                {
                    shouldPaint = true;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                _isPainting = false;
            }

            if (shouldPaint && _validBrushPosition)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                
                // Record undo state before first brush stroke
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    _editor.RecordUndoState("Terrain Paint");
                }
                
                _editor.ApplyBrushAtWorldPosition(_lastBrushPosition);
                _editor.ApplyMeshToGenerator();
                
                EditorUtility.SetDirty(_editor);
                if (_editor.sourceGenerator != null)
                {
                    EditorUtility.SetDirty(_editor.sourceGenerator);
                }
            }

            // Force repaint for smooth brush following
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                SceneView.RepaintAll();
            }
        }

        private Vector3 GetMouseWorldPositionOnTerrain(Vector2 mousePosition)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            
            // Use the new mesh-based raycasting method
            if (_editor.RaycastAgainstTerrain(ray, out Vector3 terrainHit))
            {
                return terrainHit;
            }
            
            return Vector3.zero;
        }

        private void DrawBrushPreview(Vector3 position)
        {
            if (!_editor.showBrushPreview) return;

            // Draw main brush circle using terrain projection
            DrawTerrainFollowingCircle(position, _editor.brush.radius, _editor.brushColor * new Color(1, 1, 1, _editor.previewOpacity), 32);
            
            // Draw inner falloff circle
            DrawTerrainFollowingCircle(position, _editor.brush.radius * 0.5f, _editor.brushColor * new Color(1, 1, 1, _editor.previewOpacity * 0.5f), 16);
            
            // Draw center crosshair
            Handles.color = Color.red * new Color(1, 1, 1, _editor.previewOpacity);
            float crossSize = Mathf.Min(3f, _editor.brush.radius * 0.2f);
            Vector3 right = Vector3.right * crossSize;
            Vector3 forward = Vector3.forward * crossSize;
            Handles.DrawLine(position - right, position + right);
            Handles.DrawLine(position - forward, position + forward);
            
            // Draw brush type indicator
            DrawBrushTypeIndicator(position);

            // Show brush info label
            string brushInfo = $"{_editor.brush.brushType} | R:{_editor.brush.radius:F1} | S:{_editor.brush.strength:F1}";
            Vector3 labelPos = position + Vector3.up * 5f + Vector3.right * (_editor.brush.radius * 0.7f);
            
            Handles.color = Color.white;
            Handles.Label(labelPos, brushInfo, EditorStyles.whiteLargeLabel);
        }

        private void DrawTerrainFollowingCircle(Vector3 center, float radius, Color color, int segments)
        {
            Handles.color = color;
            Vector3[] points = new Vector3[segments + 1];
            
            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * 2 * Mathf.PI;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Vector3 worldPoint = center + offset;
                
                // Project point to terrain surface using the new method
                worldPoint = _editor.ProjectToTerrain(worldPoint);
                worldPoint.y += 0.1f; // Slightly above terrain
                points[i] = worldPoint;
            }
            
            Handles.DrawPolyLine(points);
        }

        private void DrawBrushTypeIndicator(Vector3 position)
        {
            Handles.color = _editor.brushColor * new Color(1, 1, 1, _editor.previewOpacity);
            float indicatorHeight = _editor.brush.strength * 0.5f;
            
            switch (_editor.brush.brushType)
            {
                case HeightmapBrush.BrushType.Raise:
                    Vector3 arrowTop = position + Vector3.up * indicatorHeight;
                    Handles.DrawLine(position, arrowTop);
                    Handles.ConeHandleCap(0, arrowTop, Quaternion.LookRotation(Vector3.down), 1f, EventType.Repaint);
                    break;
                    
                case HeightmapBrush.BrushType.Lower:
                    Vector3 arrowBottom = position + Vector3.down * indicatorHeight;
                    Handles.DrawLine(position, arrowBottom);
                    Handles.ConeHandleCap(0, arrowBottom, Quaternion.LookRotation(Vector3.up), 1f, EventType.Repaint);
                    break;
                    
                case HeightmapBrush.BrushType.Smooth:
                    // Draw wavy line
                    int wavePoints = 8;
                    Vector3[] wave = new Vector3[wavePoints];
                    for (int i = 0; i < wavePoints; i++)
                    {
                        float t = i / (float)(wavePoints - 1);
                        float waveHeight = Mathf.Sin(t * Mathf.PI * 3) * 2f;
                        Vector3 wavePos = position + Vector3.right * (t - 0.5f) * _editor.brush.radius * 1.2f;
                        wavePos = _editor.ProjectToTerrain(wavePos);
                        wavePos.y += waveHeight + 1f;
                        wave[i] = wavePos;
                    }
                    Handles.DrawPolyLine(wave);
                    break;
                    
                case HeightmapBrush.BrushType.SetHeight:
                case HeightmapBrush.BrushType.Flatten:
                    Handles.color = Color.cyan * new Color(1, 1, 1, _editor.previewOpacity * 0.5f);
                    Vector3 targetPos = position;
                    targetPos.y = _editor.brush.targetHeight;
                    Handles.DrawWireDisc(targetPos, Vector3.up, _editor.brush.radius * 0.7f);
                    Handles.DrawLine(position, targetPos);
                    break;
                    
                case HeightmapBrush.BrushType.Noise:
                    System.Random rand = new System.Random(42);
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = (i / 6f) * 2 * Mathf.PI;
                        float distance = _editor.brush.radius * 0.4f;
                        Vector3 spikeBase = position + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                        spikeBase = _editor.ProjectToTerrain(spikeBase);
                        
                        float spikeHeight = ((float)rand.NextDouble() - 0.5f) * indicatorHeight;
                        Vector3 spikeTop = spikeBase + Vector3.up * spikeHeight;
                        
                        Handles.DrawLine(spikeBase, spikeTop);
                        if (spikeHeight > 0)
                        {
                            Handles.ConeHandleCap(0, spikeTop, Quaternion.LookRotation(Vector3.down), 0.5f, EventType.Repaint);
                        }
                    }
                    break;
            }
        }

        // Updated utility methods for mesh-based operations
        private void AnalyzeVertexHeights()
        {
            if (!_editor.HasValidData()) return;

            // This would need to be implemented in the main class as a public method
            EditorUtility.DisplayDialog("Mesh Analysis", 
                "Vertex height analysis would be implemented here.\n" +
                "This requires access to the vertex data from the main editor class.", "OK");
        }

        private void ExportMeshAsOBJ()
        {
            if (!_editor.HasValidData())
            {
                EditorUtility.DisplayDialog("Error", "No mesh data to export.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel("Export Mesh as OBJ", "", "terrain_mesh", "obj");
            if (string.IsNullOrEmpty(path)) return;

            EditorUtility.DisplayDialog("Export", "OBJ export functionality would be implemented here.", "OK");
        }

        private void ExportVertexData()
        {
            if (!_editor.HasValidData())
            {
                EditorUtility.DisplayDialog("Error", "No vertex data to export.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel("Export Vertex Data", "", "vertex_data", "txt");
            if (string.IsNullOrEmpty(path)) return;

            EditorUtility.DisplayDialog("Export", "Vertex data export functionality would be implemented here.", "OK");
        }

        private void RecalculateNormals()
        {
            if (_editor.ApplyMeshToGenerator())
            {
                EditorUtility.DisplayDialog("Success", "Mesh normals recalculated.", "OK");
                SceneView.RepaintAll();
            }
        }

        private void ResetToOriginal()
        {
            if (_editor.RefreshMeshData())
            {
                _editor.ApplyMeshToGenerator();
                EditorUtility.DisplayDialog("Reset Complete", "Mesh reset to original state.", "OK");
                SceneView.RepaintAll();
            }
        }
    }
}
#endif