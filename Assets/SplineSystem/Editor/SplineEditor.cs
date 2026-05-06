#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace FriendSlop.Splines
{
    [CustomEditor(typeof(Spline))]
    public class SplineEditor : Editor
    {
        Spline _spline;
        List<Transform> _pts = new List<Transform>();

        void OnEnable()
        {
            _spline = (Spline)target;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Control Points", EditorStyles.boldLabel);

            if (GUILayout.Button("Add Point (End)")) 
            {
                AddPointAtEnd();
                changed = true;
            }
            if (GUILayout.Button("Insert Point (Before Selected)")) 
            {
                InsertBeforeSelected();
                changed = true;
            }
            if (GUILayout.Button("Remove Selected Point")) 
            {
                RemoveSelectedPoint();
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(_spline);
                RebuildIfGeneratorPresent();
                // Force scene view to repaint
                SceneView.RepaintAll();
            }
        }

        void AddPointAtEnd()
        {
            _spline.GetControlPoints(_pts);
            Vector3 pos = _pts.Count > 0 ? _pts[_pts.Count - 1].position + _spline.transform.forward : _spline.transform.position;
            CreatePoint(pos, _pts.Count);
        }

        void InsertBeforeSelected()
        {
            _spline.GetControlPoints(_pts);
            if (Selection.activeTransform == null) return;
            int idx = -1;
            for (int i = 0; i < _pts.Count; i++)
                if (_pts[i] == Selection.activeTransform) { idx = i; break; }
            if (idx < 0) return;

            Vector3 pos = _pts[idx].position + Vector3.right * 1f;
            CreatePoint(pos, idx);
        }

        void RemoveSelectedPoint()
        {
            _spline.GetControlPoints(_pts);
            if (Selection.activeTransform == null) return;
            for (int i = 0; i < _pts.Count; i++)
            {
                if (_pts[i] == Selection.activeTransform)
                {
                    Undo.DestroyObjectImmediate(_pts[i].gameObject);
                    break;
                }
            }
            RebuildIfGeneratorPresent();
        }

        void CreatePoint(Vector3 position, int index)
        {
            GameObject g = new GameObject("Point " + index.ToString("00"));
            Undo.RegisterCreatedObjectUndo(g, "Create Spline Point");
            g.transform.SetParent(_spline.transform, true);
            g.transform.position = position;
            g.transform.rotation = Quaternion.identity;
            g.transform.localScale = Vector3.one;

            // Reorder children to match desired index
            g.transform.SetSiblingIndex(Mathf.Clamp(index, 0, _spline.transform.childCount));
            RebuildIfGeneratorPresent();
        }

        void RebuildIfGeneratorPresent()
        {
            var gen = _spline.GetComponent<TrailMeshGenerator>();
            if (gen != null)
            {
                gen.Rebuild();
            }
        }

        void OnSceneGUI()
        {
            _spline.GetControlPoints(_pts);
            Handles.color = Color.cyan;

            bool anyPointMoved = false;

            for (int i = 0; i < _pts.Count; i++)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(_pts[i].position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_pts[i], "Move Spline Point");
                    _pts[i].position = newPos;
                    anyPointMoved = true;
                }

                // Label
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = Color.cyan;
                Handles.Label(_pts[i].position + Vector3.up * 0.25f, $"{i}", style);
            }

            // Rebuild mesh if any point was moved
            if (anyPointMoved)
            {
                RebuildIfGeneratorPresent();
                // Mark the spline as dirty for undo/redo
                EditorUtility.SetDirty(_spline);
            }

            // Draw spline preview - now the spline handles open vs closed correctly
            Handles.color = new Color(1, 1, 0, 0.7f);
            int samples = Mathf.Clamp(_spline.gizmoSamplesPerSegment, 4, 64);
            
            if (_spline.PointCount >= 2)
            {
                int segments = _spline.closed ? _spline.PointCount : (_spline.PointCount - 1);
                int total = segments * samples;
                
                Vector3 prev = _spline.GetPoint(0);
                for (int i = 1; i <= total; i++)
                {
                    float t = i / (float)total;
                    Vector3 p = _spline.GetPoint(t);
                    Handles.DrawLine(prev, p);
                    prev = p;
                }
            }
        }
    }
}
#endif