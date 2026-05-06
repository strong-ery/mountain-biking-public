using System.Collections.Generic;
using UnityEngine;

namespace FriendSlop.Splines
{
    [ExecuteAlways]
    [RequireComponent(typeof(Spline))]
    [AddComponentMenu("FriendSlop/Trail Mesh Generator")]
    public class TrailMeshGenerator : MonoBehaviour
    {
        [Header("Geometry")]
        [Min(0.1f)] public float width = 3f;
        [Range(2, 64)] public int samplesPerSegment = 12;
        [Tooltip("Extra resolution multiplier for very curvy sections.")]
        [Range(1f, 4f)] public float curvatureBoost = 1f;
        [Tooltip("Adds subtle vertical noise to the surface for roughness.")]
        public float surfaceNoiseAmplitude = 0.0f;
        public float surfaceNoiseFrequency = 1.0f;
        public int seed = 12345;

        [Header("UVs")]
        public float uvTilingX = 1f; // across width
        public float uvTilingY = 1f; // along length per meter

        [Header("Banking/Camber")]
        [Tooltip("Banking across width in degrees. Positive banks right side down the slope.")]
        public float camberDegrees = 0f;

        [Header("Output")]
        public Material material;
        public bool generateCollider = true;
        public bool markStatic = true;

        MeshFilter _mf;
        MeshRenderer _mr;
        MeshCollider _mc;
        Spline _spline;

        Mesh _mesh;
        System.Random _rng;

        // For editor updates
        private Vector3[] _lastPointPositions;
        private int _lastPointCount;
        private bool _lastClosedState;

        void Reset()
        {
            EnsureComponents();
        }

        void OnEnable()
        {
            EnsureComponents();
            Rebuild();
        }

        void OnValidate()
        {
            EnsureComponents();
            Rebuild();
        }

        void Update()
        {
            // Check for changes in editor and rebuild if needed
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                CheckForSplineChanges();
            }
            #endif
        }

        #if UNITY_EDITOR
        void CheckForSplineChanges()
        {
            if (_spline == null) return;

            bool needsRebuild = false;

            // Check if point count changed
            if (_lastPointCount != _spline.PointCount)
            {
                needsRebuild = true;
                _lastPointCount = _spline.PointCount;
            }

            // Check if closed state changed
            if (_lastClosedState != _spline.closed)
            {
                needsRebuild = true;
                _lastClosedState = _spline.closed;
            }

            // Check if any point positions changed
            if (_lastPointPositions == null || _lastPointPositions.Length != _spline.PointCount)
            {
                _lastPointPositions = new Vector3[_spline.PointCount];
                needsRebuild = true;
            }

            for (int i = 0; i < _spline.PointCount; i++)
            {
                var currentPos = _spline.GetPointTransform(i).position;
                if (Vector3.Distance(_lastPointPositions[i], currentPos) > 0.001f)
                {
                    _lastPointPositions[i] = currentPos;
                    needsRebuild = true;
                }
            }

            if (needsRebuild)
            {
                Rebuild();
            }
        }
        #endif

        void EnsureComponents()
        {
            if (_spline == null) _spline = GetComponent<Spline>();
            if (_mf == null) _mf = GetComponent<MeshFilter>();
            if (_mr == null) _mr = GetComponent<MeshRenderer>();
            if (generateCollider && _mc == null) _mc = gameObject.GetComponent<MeshCollider>();
            if (_mc == null && generateCollider) _mc = gameObject.AddComponent<MeshCollider>();
            if (_mr != null && material != null) _mr.sharedMaterial = material;
            if (_mf == null) _mf = gameObject.AddComponent<MeshFilter>();
            if (_mr == null) _mr = gameObject.AddComponent<MeshRenderer>();
            if (markStatic) gameObject.isStatic = true;
        }

        public void Rebuild()
        {
            if (_spline == null) _spline = GetComponent<Spline>();
            if (_spline.PointCount < 2)
            {
                if (_mf) _mf.sharedMesh = null;
                if (_mc) _mc.sharedMesh = null;
                return;
            }

            _rng = new System.Random(seed);

            if (_mesh == null)
            {
                _mesh = new Mesh();
                _mesh.name = name + "_TrailMesh";
            }
            else
            {
                _mesh.Clear();
            }

            GenerateMesh(_mesh);
            _mf.sharedMesh = _mesh;
            if (generateCollider)
            {
                if (_mc == null) _mc = gameObject.AddComponent<MeshCollider>();
                _mc.sharedMesh = null; // force refresh
                _mc.sharedMesh = _mesh;
            }

            // Update cached values
            #if UNITY_EDITOR
            _lastPointCount = _spline.PointCount;
            _lastClosedState = _spline.closed;
            if (_lastPointPositions == null || _lastPointPositions.Length != _spline.PointCount)
            {
                _lastPointPositions = new Vector3[_spline.PointCount];
            }
            for (int i = 0; i < _spline.PointCount; i++)
            {
                _lastPointPositions[i] = _spline.GetPointTransform(i).position;
            }
            #endif
        }

        void GenerateMesh(Mesh mesh)
        {
            var points = new List<Vector3>();
            var rights = new List<Vector3>();
            var ups = new List<Vector3>();

            // Calculate segments correctly based on whether spline is closed
            int segments = _spline.closed ? _spline.PointCount : (_spline.PointCount - 1);
            int totalSamples = Mathf.Max(2, Mathf.RoundToInt(samplesPerSegment * segments * curvatureBoost));

            // Sample t from 0 to 1 - the spline itself now handles open vs closed correctly
            for (int i = 0; i <= totalSamples; i++)
            {
                float t = i / (float)totalSamples;

                var p = _spline.GetPoint(t);
                _spline.GetFrame(t, out var right, out var up);

                // Apply camber (bank across width) around tangent axis
                if (Mathf.Abs(camberDegrees) > 0.0001f)
                {
                    var tangent = _spline.GetTangent(t);
                    var q = Quaternion.AngleAxis(camberDegrees, tangent);
                    right = q * right;
                    up = q * up;
                }

                // Add micro-noise to position for roughness
                if (surfaceNoiseAmplitude > 0f && surfaceNoiseFrequency > 0f)
                {
                    float n = (float)_rng.NextDouble() * 2f - 1f;
                    p += up * (n * surfaceNoiseAmplitude);
                }

                points.Add(p);
                rights.Add(right);
                ups.Add(up);
            }

            int ringCount = points.Count;
            int vertCount = ringCount * 2; // left + right per ring
            var vertices = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tangents = new Vector4[vertCount];

            float halfW = width * 0.5f;
            float vAccum = 0f;

            for (int i = 0; i < ringCount; i++)
            {
                Vector3 p = points[i];
                Vector3 right = rights[i];
                Vector3 up = ups[i];
                Vector3 tangent = (i < ringCount - 1) ? (points[i + 1] - p).normalized : (p - points[i - 1]).normalized;

                Vector3 leftPos = p - right * halfW;
                Vector3 rightPos = p + right * halfW;

                int li = i * 2 + 0;
                int ri = i * 2 + 1;
                vertices[li] = leftPos;
                vertices[ri] = rightPos;

                normals[li] = up;
                normals[ri] = up;

                // Accumulate v based on distance from previous ring to keep texels roughly square
                if (i > 0)
                {
                    vAccum += Vector3.Distance(points[i - 1], points[i]);
                }

                uvs[li] = new Vector2(0f * uvTilingX, vAccum * uvTilingY);
                uvs[ri] = new Vector2(1f * uvTilingX, vAccum * uvTilingY);

                Vector4 tan = new Vector4(tangent.x, tangent.y, tangent.z, 1f);
                tangents[li] = tan;
                tangents[ri] = tan;
            }

            // Triangles
            int quadCount = ringCount - 1;
            var indices = new int[quadCount * 6];
            int idx = 0;
            for (int i = 0; i < quadCount; i++)
            {
                int li0 = i * 2;
                int ri0 = i * 2 + 1;
                int li1 = (i + 1) * 2;
                int ri1 = (i + 1) * 2 + 1;

                // First tri
                indices[idx++] = li0;
                indices[idx++] = li1;
                indices[idx++] = ri1;

                // Second tri
                indices[idx++] = li0;
                indices[idx++] = ri1;
                indices[idx++] = ri0;
            }

            mesh.indexFormat = (vertCount > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, new List<Vector2>(uvs));
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
        }
    }
}