#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FriendSlop.Splines
{
    [CustomEditor(typeof(MountainGenerator))]
    public class MountainGeneratorEditor : Editor
    {
        MountainGenerator _generator;
        bool _showDebugInfo = false;
        bool _showScalingInfo = true;

        void OnEnable()
        {
            _generator = (MountainGenerator)target;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space();
            
            // Scaling Information Panel
            _showScalingInfo = EditorGUILayout.Foldout(_showScalingInfo, "Density Scaling Info", true, EditorStyles.foldoutHeader);
            if (_showScalingInfo)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Actual Feature Counts (Based on Terrain Size):", EditorStyles.boldLabel);
                
                float terrainArea = _generator.terrainSize.x * _generator.terrainSize.y;
                EditorGUILayout.LabelField($"Terrain Area: {terrainArea:F0} units² ({terrainArea/10000f:F2}x reference)");
                EditorGUILayout.LabelField($"Mountain Peaks: {_generator.GetActualPeakCount()} (density: {_generator.peakDensity:F1}/10k units²)");
                if (_generator.generateRidges)
                    EditorGUILayout.LabelField($"Ridges: {_generator.GetActualRidgeCount()} (density: {_generator.ridgeDensity:F1}/10k units²)");
                EditorGUILayout.LabelField($"Scaled Mountain Radius: {_generator.GetScaledMountainRadius():F1} units");
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Reference: 200x200 terrain = 40,000 units²", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate New Mountains", GUILayout.Height(30)))
            {
                _generator.GenerateTerrain();
                SceneView.RepaintAll();
            }
            
            if (GUILayout.Button("Re-carve Paths Only", GUILayout.Height(30)))
            {
                // This would need a public method in MountainGenerator
                _generator.GenerateTerrain(); // For now, just regenerate everything
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Quick terrain size presets
            EditorGUILayout.LabelField("Quick Size Presets:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Small\n200x200", GUILayout.Height(40)))
            {
                _generator.terrainSize = new Vector2(200, 200);
                EditorUtility.SetDirty(_generator);
            }
            if (GUILayout.Button("Medium\n500x500", GUILayout.Height(40)))
            {
                _generator.terrainSize = new Vector2(500, 500);
                EditorUtility.SetDirty(_generator);
            }
            if (GUILayout.Button("Large\n1000x1000", GUILayout.Height(40)))
            {
                _generator.terrainSize = new Vector2(1000, 1000);
                EditorUtility.SetDirty(_generator);
            }
            if (GUILayout.Button("Huge\n2000x2000", GUILayout.Height(40)))
            {
                _generator.terrainSize = new Vector2(2000, 2000);
                EditorUtility.SetDirty(_generator);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Mountain generation preview
            if (GUILayout.Button("Preview Mountain Layout (Gizmos)", EditorStyles.miniButton))
            {
                SceneView.RepaintAll();
            }
            
            EditorGUILayout.Space();
            
            // Debug info
            _showDebugInfo = EditorGUILayout.Foldout(_showDebugInfo, "Debug Information");
            if (_showDebugInfo)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                var splines = FindObjectsOfType<Spline>();
                int validSplines = 0;
                foreach (var spline in splines)
                {
                    if (spline.PointCount >= 2) validSplines++;
                }
                
                EditorGUILayout.LabelField($"Splines in Scene: {splines.Length}");
                EditorGUILayout.LabelField($"Valid Splines: {validSplines}");
                EditorGUILayout.LabelField($"Terrain Resolution: {_generator.resolution}x{_generator.resolution}");
                EditorGUILayout.LabelField($"Vertex Count: {(_generator.resolution + 1) * (_generator.resolution + 1)}");
                EditorGUILayout.LabelField($"Triangle Count: {_generator.resolution * _generator.resolution * 2}");
                EditorGUILayout.LabelField($"Erosion: {(_generator.simulateErosion ? "Enabled" : "Disabled")}");
                
                EditorGUILayout.EndVertical();
            }
            
            // Workflow tips
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "DENSITY-BASED SCALING:\n" +
                "• Peak/Ridge Density = features per 10,000 units² (100x100 reference)\n" +
                "• All noise scales automatically adjust with terrain size\n" +
                "• Mountain radius scales proportionally with terrain area\n" +
                "• Maintains consistent look regardless of terrain size\n\n" +
                "WORKFLOW:\n" +
                "1. Set terrain size first\n" +
                "2. Adjust peak/ridge density for desired feature count\n" +
                "3. Create spline paths for roads\n" +
                "4. Roads automatically carve through terrain\n\n" +
                "PERFORMANCE TIPS:\n" +
                "• Higher resolution = more detail but much slower\n" +
                "• For huge terrains (>2000x2000), keep resolution ≤256\n" +
                "• Erosion simulation adds realism but costs performance", 
                MessageType.Info
            );

            if (changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(_generator);
            }
        }

        void OnSceneGUI()
        {
            // Draw terrain bounds in scene view
            Handles.color = Color.green;
            Vector3 center = _generator.transform.position;
            Vector3 size = new Vector3(_generator.terrainSize.x, 0, _generator.terrainSize.y);
            
            // Draw terrain boundary
            Vector3[] corners = new Vector3[4];
            corners[0] = center + new Vector3(-size.x * 0.5f, 0, -size.z * 0.5f);
            corners[1] = center + new Vector3(size.x * 0.5f, 0, -size.z * 0.5f);
            corners[2] = center + new Vector3(size.x * 0.5f, 0, size.z * 0.5f);
            corners[3] = center + new Vector3(-size.x * 0.5f, 0, size.z * 0.5f);
            
            Handles.DrawLine(corners[0], corners[1]);
            Handles.DrawLine(corners[1], corners[2]);
            Handles.DrawLine(corners[2], corners[3]);
            Handles.DrawLine(corners[3], corners[0]);
            
            // Label with size info
            string sizeLabel = $"Mountain Terrain\n{_generator.terrainSize.x}x{_generator.terrainSize.y}\n{_generator.GetActualPeakCount()} peaks";
            Handles.Label(center + Vector3.up * 10, sizeLabel, EditorStyles.boldLabel);
        }
    }
}
#endif