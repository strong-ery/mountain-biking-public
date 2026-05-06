using System.Collections.Generic;
using UnityEngine;

namespace FriendSlop.Splines
{
    [System.Serializable]
    public class HeightmapBrush
    {
        public enum BrushType
        {
            Raise,
            Lower,
            Smooth,
            Flatten,
            Noise,
            SetHeight
        }

        public BrushType brushType = BrushType.Raise;
        public float radius = 10f;
        public float strength = 5f;
        public float targetHeight = 0f;
        public AnimationCurve falloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    }

    [ExecuteAlways]
    [AddComponentMenu("FriendSlop/Terrain Heightmap Editor")]
    public class TerrainHeightmapEditor : MonoBehaviour
    {
        [Header("Source Mountain Generator")]
        [Tooltip("The MountainGenerator to read heightmap data from")]
        public MountainGenerator sourceGenerator;

        [Header("Height Editing")]
        [Tooltip("Brush settings for height modifications")]
        public HeightmapBrush brush = new HeightmapBrush();

        [Header("Global Operations")]
        [Tooltip("Add this value to all heights")]
        [Range(-50f, 50f)] public float globalHeightOffset = 0f;

        [Tooltip("Multiply all heights by this value")]
        [Range(0.1f, 3f)] public float globalHeightMultiplier = 1f;

        [Tooltip("Apply global operations")]
        public bool applyGlobalChanges = false;

        [Header("Smoothing")]
        [Tooltip("Apply smoothing to entire heightmap")]
        public bool applySmoothingToAll = false;
        [Range(1, 10)] public int smoothingIterations = 3;
        [Range(0f, 1f)] public float smoothingStrength = 0.5f;

        [Header("Noise Operations")]
        [Tooltip("Add noise to entire heightmap")]
        public bool addNoiseToAll = false;
        public float noiseScale = 0.1f;
        [Range(0f, 10f)] public float noiseStrength = 2f;
        public int noiseSeed = 0;

        [Header("Debug & Preview")]
        public bool showBrushGizmo = true;
        public bool showBrushPreview = true;
        public bool showHeightPreview = true;
        [Range(0.1f, 1f)] public float previewOpacity = 0.7f;
        public Color brushColor = Color.yellow;

        // Private data - using actual mesh data instead of manual calculations
        private Mesh _terrainMesh;
        private Vector3[] _originalVertices;
        private Vector3[] _modifiedVertices;
        private int[] _triangles;
        private MeshCollider _meshCollider;
        
        // Improved spatial data structures
        private Dictionary<Vector2, int> _vertexLookup;
        private SpatialGrid _spatialGrid;
        
        // Cache data
        private Vector2 _terrainSize;
        private int _resolution;
        private bool _hasValidData = false;
        private float _vertexSpacing = 1f;

        // Undo support for editor operations
        private Vector3[] _undoVertices;

        void Start()
        {
            if (sourceGenerator == null)
            {
                sourceGenerator = GetComponent<MountainGenerator>();
            }
            RefreshMeshData();
        }

        void OnValidate()
        {
            if (sourceGenerator != null)
            {
                RefreshMeshData();

                if (applyGlobalChanges)
                {
                    ApplyGlobalTransforms();
                    applyGlobalChanges = false;
                }

                if (applySmoothingToAll)
                {
                    ApplyGlobalSmoothing();
                    applySmoothingToAll = false;
                }

                if (addNoiseToAll)
                {
                    ApplyGlobalNoise();
                    addNoiseToAll = false;
                }
            }
        }

        /// <summary>
        /// Refresh mesh data from the source generator
        /// </summary>
        public bool RefreshMeshData()
        {
            if (sourceGenerator == null) return false;

            // Get the actual mesh from the MeshFilter
            MeshFilter meshFilter = sourceGenerator.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) return false;

            _terrainMesh = meshFilter.sharedMesh;
            _originalVertices = _terrainMesh.vertices;
            _modifiedVertices = new Vector3[_originalVertices.Length];
            System.Array.Copy(_originalVertices, _modifiedVertices, _originalVertices.Length);
            _triangles = _terrainMesh.triangles;

            // Get mesh collider for raycasting
            _meshCollider = sourceGenerator.GetComponent<MeshCollider>();

            // Cache terrain data from generator
            _terrainSize = sourceGenerator.terrainSize;
            _resolution = sourceGenerator.resolution;

            // Calculate average vertex spacing for better neighbor finding
            CalculateVertexSpacing();

            // Build improved spatial lookup structures
            BuildVertexLookup();
            BuildSpatialGrid();

            _hasValidData = true;
            return true;
        }

        /// <summary>
        /// Calculate the average spacing between vertices
        /// </summary>
        private void CalculateVertexSpacing()
        {
            if (_originalVertices.Length < 2) return;

            float totalDistance = 0f;
            int sampleCount = 0;

            // Sample a subset of vertices to calculate average spacing
            int stepSize = Mathf.Max(1, _originalVertices.Length / 100);
            
            for (int i = 0; i < _originalVertices.Length - stepSize; i += stepSize)
            {
                Vector3 v1 = _originalVertices[i];
                Vector3 v2 = _originalVertices[i + stepSize];
                
                float distance = Vector3.Distance(
                    new Vector3(v1.x, 0, v1.z),
                    new Vector3(v2.x, 0, v2.z)
                );
                
                if (distance > 0.001f)
                {
                    totalDistance += distance;
                    sampleCount++;
                }
            }

            _vertexSpacing = sampleCount > 0 ? totalDistance / sampleCount : 1f;
            
            // Clamp to reasonable values
            _vertexSpacing = Mathf.Clamp(_vertexSpacing, 0.1f, 10f);
        }

        /// <summary>
        /// Build a spatial lookup for quick vertex finding
        /// </summary>
        private void BuildVertexLookup()
        {
            _vertexLookup = new Dictionary<Vector2, int>();
            
            for (int i = 0; i < _originalVertices.Length; i++)
            {
                Vector3 vertex = _originalVertices[i];
                Vector2 key = new Vector2(vertex.x, vertex.z);
                
                // Use a small tolerance for floating point comparisons
                key.x = Mathf.Round(key.x * 1000f) / 1000f;
                key.y = Mathf.Round(key.y * 1000f) / 1000f;
                
                if (!_vertexLookup.ContainsKey(key))
                {
                    _vertexLookup[key] = i;
                }
            }
        }

        /// <summary>
        /// Build spatial grid for efficient nearest neighbor searches
        /// </summary>
        private void BuildSpatialGrid()
        {
            _spatialGrid = new SpatialGrid(_originalVertices, _vertexSpacing);
        }

        /// <summary>
        /// Apply the modified mesh back to the generator
        /// </summary>
        public bool ApplyMeshToGenerator()
        {
            if (!_hasValidData || sourceGenerator == null) return false;

            MeshFilter meshFilter = sourceGenerator.GetComponent<MeshFilter>();
            if (meshFilter == null) return false;

#if UNITY_EDITOR
            // In edit mode, we need to handle mesh modification properly to avoid leaks
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                // Get the shared mesh
                Mesh sharedMesh = meshFilter.sharedMesh;
                if (sharedMesh == null) return false;

                // Check if this is an asset (don't modify assets directly)
                string assetPath = UnityEditor.AssetDatabase.GetAssetPath(sharedMesh);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    // Create an instance copy for editing
                    Mesh editableMesh = Object.Instantiate(sharedMesh);
                    editableMesh.name = sharedMesh.name + " (Editable)";
                    meshFilter.sharedMesh = editableMesh;
                    
                    // Update our cached mesh reference
                    _terrainMesh = editableMesh;
                }

                // Now modify the mesh
                Mesh currentMesh = meshFilter.sharedMesh;
                currentMesh.vertices = _modifiedVertices;
                currentMesh.RecalculateNormals();
                currentMesh.RecalculateBounds();

                // Update mesh collider
                if (_meshCollider != null)
                {
                    _meshCollider.sharedMesh = currentMesh;
                }

                // Mark scene as dirty
                UnityEditor.EditorUtility.SetDirty(sourceGenerator);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sourceGenerator.gameObject.scene);
            }
            else
#endif
            {
                // Runtime - use the standard approach
                Mesh currentMesh = meshFilter.mesh;
                if (currentMesh == null) return false;

                currentMesh.vertices = _modifiedVertices;
                currentMesh.RecalculateNormals();
                currentMesh.RecalculateBounds();
                currentMesh.MarkDynamic();

                if (_meshCollider != null)
                {
                    _meshCollider.sharedMesh = currentMesh;
                }
            }

            return true;
        }

        /// <summary>
        /// Get height at world position using direct mesh vertex lookup
        /// </summary>
        public float GetHeightAtWorldPosition(Vector3 worldPos)
        {
            if (!_hasValidData) return 0f;

            // Convert world position to local space
            Vector3 localPos = sourceGenerator.transform.InverseTransformPoint(worldPos);
            
            // Find the nearest vertex or interpolate between vertices
            return GetHeightAtLocalPosition(localPos);
        }

        /// <summary>
        /// Get height at local position using mesh data
        /// </summary>
        public float GetHeightAtLocalPosition(Vector3 localPos)
        {
            if (!_hasValidData) return 0f;

            // Use spatial grid to find nearest vertices for interpolation
            var nearestIndices = _spatialGrid.FindNearestVertices(localPos, 4);
            
            if (nearestIndices.Length == 0) return 0f;
            if (nearestIndices.Length == 1) return _modifiedVertices[nearestIndices[0]].y;

            // Perform inverse distance weighted interpolation
            float totalWeight = 0f;
            float weightedHeight = 0f;

            foreach (int index in nearestIndices)
            {
                Vector3 vertex = _modifiedVertices[index];
                float distance = Vector3.Distance(new Vector3(localPos.x, 0, localPos.z), 
                                                new Vector3(vertex.x, 0, vertex.z));
                
                if (distance < 0.001f) return vertex.y; // Exact match
                
                float weight = 1f / (distance * distance);
                totalWeight += weight;
                weightedHeight += vertex.y * weight;
            }

            return totalWeight > 0 ? weightedHeight / totalWeight : 0f;
        }

        /// <summary>
        /// Apply brush operation at world position - improved version
        /// </summary>
        public void ApplyBrushAtWorldPosition(Vector3 worldPos)
        {
            if (!_hasValidData) return;

            // Convert to local space
            Vector3 localPos = sourceGenerator.transform.InverseTransformPoint(worldPos);
            
            // Find all vertices within brush radius
            var verticesInRange = _spatialGrid.FindVerticesInRadius(localPos, brush.radius);
            
            // For smoothing, we need to capture the current heights before any modifications
            Dictionary<int, float> preModificationHeights = new Dictionary<int, float>();
            if (brush.brushType == HeightmapBrush.BrushType.Smooth)
            {
                foreach (int index in verticesInRange)
                {
                    preModificationHeights[index] = _modifiedVertices[index].y;
                }
            }

            // Apply brush effect to each vertex
            foreach (int vertexIndex in verticesInRange)
            {
                Vector3 vertex = _modifiedVertices[vertexIndex];
                float distance = Vector3.Distance(new Vector3(localPos.x, 0, localPos.z), 
                                                new Vector3(vertex.x, 0, vertex.z));
                
                if (distance <= brush.radius)
                {
                    float falloff = brush.falloffCurve.Evaluate(distance / brush.radius);
                    ApplyBrushEffectToVertex(vertexIndex, falloff, localPos, preModificationHeights);
                }
            }
        }

        private void ApplyBrushEffectToVertex(int vertexIndex, float falloff, Vector3 brushCenter, Dictionary<int, float> preModificationHeights = null)
        {
            Vector3 vertex = _modifiedVertices[vertexIndex];
            float currentHeight = vertex.y;
            
            // Fixed: Remove Time.deltaTime dependency for consistent brush strength
            float effectStrength = brush.strength * falloff * 0.02f; // Fixed multiplier instead of deltaTime

            switch (brush.brushType)
            {
                case HeightmapBrush.BrushType.Raise:
                    vertex.y = currentHeight + effectStrength;
                    break;

                case HeightmapBrush.BrushType.Lower:
                    vertex.y = currentHeight - effectStrength;
                    break;

                case HeightmapBrush.BrushType.SetHeight:
                    vertex.y = Mathf.Lerp(currentHeight, brush.targetHeight, falloff * 0.05f);
                    break;

                case HeightmapBrush.BrushType.Flatten:
                    float targetHeight = brush.targetHeight;
                    if (targetHeight == 0f) 
                    {
                        // Use the height at the brush center if no target height is specified
                        targetHeight = GetHeightAtLocalPosition(brushCenter);
                    }
                    vertex.y = Mathf.Lerp(currentHeight, targetHeight, falloff * 0.05f);
                    break;

                case HeightmapBrush.BrushType.Smooth:
                    float smoothedHeight = GetSmoothedHeightForVertex(vertexIndex, preModificationHeights);
                    vertex.y = Mathf.Lerp(currentHeight, smoothedHeight, falloff * 0.1f);
                    break;

                case HeightmapBrush.BrushType.Noise:
                    Vector3 worldVertex = sourceGenerator.transform.TransformPoint(vertex);
                    float noise = Mathf.PerlinNoise(
                        worldVertex.x * 0.1f + Time.time * 0.1f, 
                        worldVertex.z * 0.1f + Time.time * 0.1f
                    ) - 0.5f;
                    vertex.y = currentHeight + noise * effectStrength;
                    break;
            }

            _modifiedVertices[vertexIndex] = vertex;
        }

        private float GetSmoothedHeightForVertex(int vertexIndex, Dictionary<int, float> preModificationHeights)
        {
            Vector3 centerVertex = _modifiedVertices[vertexIndex];
            
            // Use adaptive radius based on vertex spacing
            float neighborRadius = _vertexSpacing * 2.5f; // Increased for better smoothing
            var neighbors = _spatialGrid.FindVerticesInRadius(centerVertex, neighborRadius);
            
            float sum = 0f;
            int count = 0;

            foreach (int neighborIndex in neighbors)
            {
                float heightToUse;
                
                // Use pre-modification heights if available (prevents feedback loops)
                if (preModificationHeights != null && preModificationHeights.ContainsKey(neighborIndex))
                {
                    heightToUse = preModificationHeights[neighborIndex];
                }
                else
                {
                    heightToUse = _modifiedVertices[neighborIndex].y;
                }
                
                // Weight by distance for better smoothing
                float distance = Vector3.Distance(
                    new Vector3(centerVertex.x, 0, centerVertex.z),
                    new Vector3(_modifiedVertices[neighborIndex].x, 0, _modifiedVertices[neighborIndex].z)
                );
                
                float weight = 1f / (1f + distance); // Inverse distance weighting
                sum += heightToUse * weight;
                count++;
            }

            return count > 0 ? sum / count : centerVertex.y;
        }

        /// <summary>
        /// Raycast against the actual mesh for accurate world position conversion
        /// </summary>
        public bool RaycastAgainstTerrain(Ray ray, out Vector3 hitPoint, float maxDistance = Mathf.Infinity)
        {
            hitPoint = Vector3.zero;
            
            if (!_hasValidData || _meshCollider == null) return false;

            // Use the actual mesh collider for precise raycasting
            if (_meshCollider.Raycast(ray, out RaycastHit hit, maxDistance))
            {
                hitPoint = hit.point;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Project world position onto terrain surface using mesh data
        /// </summary>
        public Vector3 ProjectToTerrain(Vector3 worldPos)
        {
            // Create a ray from above the position downward
            Vector3 rayStart = new Vector3(worldPos.x, worldPos.y + 1000f, worldPos.z);
            Ray ray = new Ray(rayStart, Vector3.down);
            
            if (RaycastAgainstTerrain(ray, out Vector3 hitPoint))
            {
                return hitPoint;
            }
            
            // Fallback: use vertex interpolation
            Vector3 localPos = sourceGenerator.transform.InverseTransformPoint(worldPos);
            float height = GetHeightAtLocalPosition(localPos);
            return sourceGenerator.transform.TransformPoint(new Vector3(localPos.x, height, localPos.z));
        }

        // Global operations now work directly with vertex data
        private void ApplyGlobalTransforms()
        {
            if (!_hasValidData) return;

            for (int i = 0; i < _modifiedVertices.Length; i++)
            {
                Vector3 vertex = _modifiedVertices[i];
                vertex.y = (vertex.y * globalHeightMultiplier) + globalHeightOffset;
                _modifiedVertices[i] = vertex;
            }

            ApplyMeshToGenerator();
        }

        private void ApplyGlobalSmoothing()
        {
            if (!_hasValidData) return;

            for (int iteration = 0; iteration < smoothingIterations; iteration++)
            {
                Vector3[] smoothedVertices = new Vector3[_modifiedVertices.Length];
                System.Array.Copy(_modifiedVertices, smoothedVertices, _modifiedVertices.Length);

                // Use current heights for the smoothing calculation
                Dictionary<int, float> currentHeights = new Dictionary<int, float>();
                for (int i = 0; i < _modifiedVertices.Length; i++)
                {
                    currentHeights[i] = _modifiedVertices[i].y;
                }

                for (int i = 0; i < _modifiedVertices.Length; i++)
                {
                    float smoothedHeight = GetSmoothedHeightForVertex(i, currentHeights);
                    Vector3 vertex = smoothedVertices[i];
                    vertex.y = Mathf.Lerp(vertex.y, smoothedHeight, smoothingStrength);
                    smoothedVertices[i] = vertex;
                }

                _modifiedVertices = smoothedVertices;
            }

            ApplyMeshToGenerator();
        }

        private void ApplyGlobalNoise()
        {
            if (!_hasValidData) return;

            System.Random rng = new System.Random(noiseSeed);

            for (int i = 0; i < _modifiedVertices.Length; i++)
            {
                Vector3 vertex = _modifiedVertices[i];
                Vector3 worldVertex = sourceGenerator.transform.TransformPoint(vertex);
                
                float noise = Mathf.PerlinNoise(
                    (worldVertex.x + (float)rng.NextDouble() * 1000) * noiseScale,
                    (worldVertex.z + (float)rng.NextDouble() * 1000) * noiseScale
                ) - 0.5f;

                vertex.y += noise * noiseStrength;
                _modifiedVertices[i] = vertex;
            }

            ApplyMeshToGenerator();
        }

        // Public utility methods
        public Vector2 GetTerrainSize() => _terrainSize;
        public int GetResolution() => _resolution;
        public bool HasValidData() => _hasValidData;
        public Vector3 GetTerrainPosition() => sourceGenerator.transform.position;

        public Bounds GetTerrainBounds()
        {
            if (_terrainMesh != null)
            {
                return _terrainMesh.bounds;
            }
            
            Vector3 center = sourceGenerator.transform.position;
            Vector3 size = new Vector3(_terrainSize.x, 200f, _terrainSize.y);
            return new Bounds(center, size);
        }

        public bool IsPositionInTerrain(Vector3 worldPos)
        {
            Bounds bounds = GetTerrainBounds();
            return bounds.Contains(worldPos);
        }

        void OnDrawGizmosSelected()
        {
            if (!showBrushGizmo || !_hasValidData) return;

            Gizmos.color = Color.white * 0.3f;
            Bounds bounds = GetTerrainBounds();
            Gizmos.DrawWireCube(bounds.center, bounds.size);

#if UNITY_EDITOR
            if (UnityEditor.SceneView.lastActiveSceneView != null)
            {
                var sceneView = UnityEditor.SceneView.lastActiveSceneView;
                if (sceneView.camera != null)
                {
                    Vector2 mousePos = Event.current?.mousePosition ?? Vector2.zero;
                    Ray ray = UnityEditor.HandleUtility.GUIPointToWorldRay(mousePos);

                    if (RaycastAgainstTerrain(ray, out Vector3 hitPoint))
                    {
                        Gizmos.color = brushColor;
                        Gizmos.DrawWireSphere(hitPoint, brush.radius);

                        switch (brush.brushType)
                        {
                            case HeightmapBrush.BrushType.Raise:
                                Gizmos.DrawLine(hitPoint, hitPoint + Vector3.up * 5f);
                                break;
                            case HeightmapBrush.BrushType.Lower:
                                Gizmos.DrawLine(hitPoint, hitPoint + Vector3.down * 5f);
                                break;
                        }
                    }
                }
            }
#endif
        }

        /// <summary>
        /// Record current state for undo before making changes
        /// </summary>
        public void RecordUndoState(string operationName)
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                // Record the current vertices for undo
                _undoVertices = new Vector3[_modifiedVertices.Length];
                System.Array.Copy(_modifiedVertices, _undoVertices, _modifiedVertices.Length);
                
                // Register undo for the component
                UnityEditor.Undo.RecordObject(this, operationName);
                
                // Register undo for the mesh
                MeshFilter meshFilter = sourceGenerator.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    UnityEditor.Undo.RecordObject(meshFilter.sharedMesh, operationName);
                }
                
                // Register undo for mesh collider if it exists
                if (_meshCollider != null)
                {
                    UnityEditor.Undo.RecordObject(_meshCollider, operationName);
                }
            }
#endif
        }

        /// <summary>
        /// Restore from undo state
        /// </summary>
        public void RestoreFromUndo()
        {
            if (_undoVertices != null && _undoVertices.Length == _modifiedVertices.Length)
            {
                System.Array.Copy(_undoVertices, _modifiedVertices, _modifiedVertices.Length);
                ApplyMeshToGenerator();
            }
        }
    }

    // Improved spatial grid implementation for efficient spatial queries
    public class SpatialGrid
    {
        private Vector3[] _vertices;
        private Dictionary<Vector2Int, List<int>> _grid;
        private float _cellSize;
        private Vector2 _minBounds;
        private Vector2 _maxBounds;

        public SpatialGrid(Vector3[] vertices, float cellSize = 2f)
        {
            _vertices = vertices;
            _cellSize = cellSize;
            BuildGrid();
        }

        private void BuildGrid()
        {
            _grid = new Dictionary<Vector2Int, List<int>>();
            
            if (_vertices.Length == 0) return;

            // Calculate bounds
            _minBounds = new Vector2(_vertices[0].x, _vertices[0].z);
            _maxBounds = new Vector2(_vertices[0].x, _vertices[0].z);
            
            foreach (var vertex in _vertices)
            {
                _minBounds.x = Mathf.Min(_minBounds.x, vertex.x);
                _minBounds.y = Mathf.Min(_minBounds.y, vertex.z);
                _maxBounds.x = Mathf.Max(_maxBounds.x, vertex.x);
                _maxBounds.y = Mathf.Max(_maxBounds.y, vertex.z);
            }

            // Populate grid
            for (int i = 0; i < _vertices.Length; i++)
            {
                Vector2Int gridPos = WorldToGrid(_vertices[i]);
                
                if (!_grid.ContainsKey(gridPos))
                {
                    _grid[gridPos] = new List<int>();
                }
                
                _grid[gridPos].Add(i);
            }
        }

        private Vector2Int WorldToGrid(Vector3 worldPos)
        {
            int x = Mathf.FloorToInt((worldPos.x - _minBounds.x) / _cellSize);
            int z = Mathf.FloorToInt((worldPos.z - _minBounds.y) / _cellSize);
            return new Vector2Int(x, z);
        }

        public int[] FindNearestVertices(Vector3 position, int count)
        {
            if (_vertices.Length == 0) return new int[0];

            var candidates = new List<(int index, float distance)>();
            Vector2Int centerCell = WorldToGrid(position);
            
            // Check cells in expanding square pattern
            int radius = 0;
            while (candidates.Count < count && radius < 10)
            {
                for (int x = centerCell.x - radius; x <= centerCell.x + radius; x++)
                {
                    for (int z = centerCell.y - radius; z <= centerCell.y + radius; z++)
                    {
                        // Only check edge cells after first iteration
                        if (radius > 0)
                        {
                            bool isEdge = (x == centerCell.x - radius || x == centerCell.x + radius ||
                                         z == centerCell.y - radius || z == centerCell.y + radius);
                            if (!isEdge) continue;
                        }

                        Vector2Int cellPos = new Vector2Int(x, z);
                        if (_grid.ContainsKey(cellPos))
                        {
                            foreach (int vertexIndex in _grid[cellPos])
                            {
                                float distance = Vector3.Distance(position, _vertices[vertexIndex]);
                                candidates.Add((vertexIndex, distance));
                            }
                        }
                    }
                }
                radius++;
            }

            candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

            int resultCount = Mathf.Min(count, candidates.Count);
            int[] result = new int[resultCount];
            
            for (int i = 0; i < resultCount; i++)
            {
                result[i] = candidates[i].index;
            }

            return result;
        }

        public int[] FindVerticesInRadius(Vector3 position, float radius)
        {
            var result = new List<int>();
            Vector2Int centerCell = WorldToGrid(position);
            
            int cellRadius = Mathf.CeilToInt(radius / _cellSize) + 1;
            
            for (int x = centerCell.x - cellRadius; x <= centerCell.x + cellRadius; x++)
            {
                for (int z = centerCell.y - cellRadius; z <= centerCell.y + cellRadius; z++)
                {
                    Vector2Int cellPos = new Vector2Int(x, z);
                    if (_grid.ContainsKey(cellPos))
                    {
                        foreach (int vertexIndex in _grid[cellPos])
                        {
                            float distance = Vector3.Distance(position, _vertices[vertexIndex]);
                            if (distance <= radius)
                            {
                                result.Add(vertexIndex);
                            }
                        }
                    }
                }
            }

            return result.ToArray();
        }
    }
}