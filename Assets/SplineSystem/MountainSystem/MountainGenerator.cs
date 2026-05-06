using System.Collections.Generic;
using UnityEngine;

namespace FriendSlop.Splines
{
    [ExecuteAlways]
    [AddComponentMenu("FriendSlop/Mountain Generator")]
    public class MountainGenerator : MonoBehaviour
    {
        [Header("Terrain Size")]
        [Tooltip("Size of the terrain in world units")]
        public Vector2 terrainSize = new Vector2(200f, 200f);
        
        [Tooltip("Resolution of the heightmap (higher = more detail)")]
        [Range(64, 512)] public int resolution = 256;
        
        [Header("Mountain Generation - Density Based")]
        [Tooltip("Base height of the terrain")]
        public float baseHeight = 10f;
        
        [Tooltip("Maximum height of mountains")]
        public float maxHeight = 50f;
        
        [Header("Mountain Peak Density")]
        [Tooltip("Number of mountain peaks per 10000 square units (100x100)")]
        [Range(0.1f, 5f)] public float peakDensity = 0.8f;
        
        [Tooltip("Size of mountain influence areas")]
        [Range(10f, 100f)] public float mountainRadius = 60f;
        
        [Tooltip("How sharp or rounded the mountains are")]
        [Range(0.5f, 3f)] public float mountainSharpness = 1.2f;
        
        [Header("Mountain Ridge Density")]
        [Tooltip("Create mountain ridges")]
        public bool generateRidges = true;
        
        [Tooltip("Number of ridges per 10000 square units")]
        [Range(0.1f, 2f)] public float ridgeDensity = 0.4f;
        
        [Tooltip("Length of ridges (scales with terrain)")]
        [Range(0.2f, 2f)] public float ridgeLengthScale = 0.8f;
        
        [Tooltip("Height of ridges")]
        [Range(5f, 30f)] public float ridgeHeight = 20f;
        
        [Header("Terrain Layers")]
        [Tooltip("Large scale terrain features")]
        public float largeNoiseScale = 0.02f;
        [Range(0f, 20f)] public float largeNoiseStrength = 15f;
        
        [Tooltip("Medium detail terrain variation")]
        public float mediumNoiseScale = 0.05f;
        [Range(0f, 10f)] public float mediumNoiseStrength = 8f;
        
        [Tooltip("Fine detail surface noise")]
        public float fineNoiseScale = 0.2f;
        [Range(0f, 3f)] public float fineNoiseStrength = 2f;
        
        [Header("Path Carving")]
        [Tooltip("How much paths cut into the terrain")]
        [Range(0f, 20f)] public float pathCarvingDepth = 8f;
        
        [Tooltip("Width of carved paths")]
        [Range(2f, 30f)] public float pathCarvingWidth = 12f;
        
        [Tooltip("How smoothly paths blend into terrain")]
        [Range(1f, 10f)] public float pathBlendDistance = 6f;
        
        [Tooltip("Prevent paths from going too steep")]
        [Range(0f, 45f)] public float maxPathSlope = 25f;
        
        [Header("Erosion Simulation")]
        [Tooltip("Simulate water erosion")]
        public bool simulateErosion = true;
        [Range(1, 10)] public int erosionIterations = 3;
        [Range(0f, 0.1f)] public float erosionStrength = 0.02f;
        
        [Header("Generation")]
        public int seed = 12345;
        public Material terrainMaterial;
        
        [Header("Auto-Update")]
        public bool autoUpdate = true;
        [Range(0.1f, 2f)] public float updateInterval = 0.5f;

        private Mesh _terrainMesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;
        private float _lastUpdateTime;
        private List<Spline> _cachedSplines = new List<Spline>();
        private Dictionary<Spline, Vector3[]> _splinePointsCache = new Dictionary<Spline, Vector3[]>();
        
        // Generated mountain data
        private Vector2[] _mountainPeakPositions;
        private Vector2[] _ridgeLines;
        private float[,] _heightMap;

        void OnEnable()
        {
            EnsureComponents();
            GenerateTerrain();
        }

        void OnValidate()
        {
            if (Application.isPlaying || !autoUpdate) return;
            GenerateTerrain();
        }

        void Update()
        {
            if (!autoUpdate) return;
            
            if (Time.time - _lastUpdateTime > updateInterval)
            {
                if (CheckForSplineChanges())
                {
                    // CarvePaths(); // Only re-carve paths, don't regenerate mountains
                    BuildMeshFromHeightmap();
                }
                _lastUpdateTime = Time.time;
            }
        }

        void EnsureComponents()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshCollider == null) _meshCollider = GetComponent<MeshCollider>();
            
            if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            if (_meshCollider == null) _meshCollider = gameObject.AddComponent<MeshCollider>();
            
            if (_meshRenderer.sharedMaterial == null && terrainMaterial != null)
                _meshRenderer.sharedMaterial = terrainMaterial;
        }

        bool CheckForSplineChanges()
        {
            var currentSplines = new List<Spline>();
            FindAllSplines(currentSplines);
            
            if (currentSplines.Count != _cachedSplines.Count) return true;
            
            foreach (var spline in currentSplines)
            {
                if (!_splinePointsCache.ContainsKey(spline)) return true;
                
                var cachedPoints = _splinePointsCache[spline];
                if (cachedPoints.Length != spline.PointCount) return true;
                
                for (int i = 0; i < spline.PointCount; i++)
                {
                    var currentPos = spline.GetPointTransform(i).position;
                    if (Vector3.Distance(cachedPoints[i], currentPos) > 0.01f)
                        return true;
                }
            }
            
            return false;
        }

        void CacheSplineData(List<Spline> splines)
        {
            _cachedSplines.Clear();
            _splinePointsCache.Clear();
            
            foreach (var spline in splines)
            {
                _cachedSplines.Add(spline);
                var points = new Vector3[spline.PointCount];
                for (int i = 0; i < spline.PointCount; i++)
                {
                    points[i] = spline.GetPointTransform(i).position;
                }
                _splinePointsCache[spline] = points;
            }
        }

        [ContextMenu("Generate Terrain")]
        public void GenerateTerrain()
        {
            EnsureComponents();
            
            var splines = new List<Spline>();
            FindAllSplines(splines);
            CacheSplineData(splines);
            
            if (_terrainMesh == null)
            {
                _terrainMesh = new Mesh();
                _terrainMesh.name = "Procedural Mountain Terrain";
            }
            else
            {
                _terrainMesh.Clear();
            }
            
            GenerateProceduralMountains();
            // CarvePaths();
            if (simulateErosion) ApplyErosion();
            BuildMeshFromHeightmap();
        }

        void GenerateProceduralMountains()
        {
            System.Random rng = new System.Random(seed);
            
            // Initialize heightmap
            _heightMap = new float[resolution + 1, resolution + 1];
            Vector3 center = transform.position;
            Vector3 terrainMin = center - new Vector3(terrainSize.x * 0.5f, 0, terrainSize.y * 0.5f);
            
            // Calculate actual counts based on density and terrain area
            float terrainArea = terrainSize.x * terrainSize.y;
            float referenceArea = 10000f; // 100x100 reference area
            float areaMultiplier = terrainArea / referenceArea;
            
            int actualPeakCount = Mathf.RoundToInt(peakDensity * areaMultiplier);
            actualPeakCount = Mathf.Max(1, actualPeakCount); // At least one peak
            
            // Generate mountain peaks with scaled radius
            _mountainPeakPositions = new Vector2[actualPeakCount];
            float scaledMountainRadius = mountainRadius * Mathf.Sqrt(areaMultiplier * 0.5f); // Scale radius with terrain
            
            for (int i = 0; i < actualPeakCount; i++)
            {
                _mountainPeakPositions[i] = new Vector2(
                    (float)rng.NextDouble() * terrainSize.x,
                    (float)rng.NextDouble() * terrainSize.y
                );
            }
            
            // Generate ridge lines with density-based scaling
            if (generateRidges)
            {
                int actualRidgeCount = Mathf.RoundToInt(ridgeDensity * areaMultiplier);
                actualRidgeCount = Mathf.Max(0, actualRidgeCount);
                
                _ridgeLines = new Vector2[actualRidgeCount * 2]; // Start and end points
                float scaledRidgeLength = Mathf.Min(terrainSize.x, terrainSize.y) * ridgeLengthScale * 0.5f;
                
                for (int i = 0; i < actualRidgeCount; i++)
                {
                    Vector2 start = new Vector2(
                        (float)rng.NextDouble() * terrainSize.x,
                        (float)rng.NextDouble() * terrainSize.y
                    );
                    
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 end = start + direction * scaledRidgeLength;
                    
                    // Keep ridges within terrain bounds
                    end.x = Mathf.Clamp(end.x, 0, terrainSize.x);
                    end.y = Mathf.Clamp(end.y, 0, terrainSize.y);
                    
                    _ridgeLines[i * 2] = start;
                    _ridgeLines[i * 2 + 1] = end;
                }
            }
            
            // Generate base heightmap with scaled features
            for (int y = 0; y <= resolution; y++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    Vector2 localPos = new Vector2(
                        (float)x / resolution * terrainSize.x,
                        (float)y / resolution * terrainSize.y
                    );
                    
                    Vector3 worldPos = terrainMin + new Vector3(localPos.x, 0, localPos.y);
                    
                    float height = GenerateHeightAtPoint(localPos, worldPos, rng, scaledMountainRadius);
                    _heightMap[x, y] = height;
                }
            }
        }

        float GenerateHeightAtPoint(Vector2 localPos, Vector3 worldPos, System.Random rng, float scaledMountainRadius)
        {
            float height = baseHeight;
            
            // Large scale terrain noise (continental features) - scale with terrain size
            float scaledLargeNoise = largeNoiseScale / Mathf.Max(terrainSize.x, terrainSize.y) * 200f;
            height += Mathf.PerlinNoise(worldPos.x * scaledLargeNoise, worldPos.z * scaledLargeNoise) * largeNoiseStrength;
            
            // Mountain peaks influence
            foreach (var peak in _mountainPeakPositions)
            {
                float distToPeak = Vector2.Distance(localPos, peak);
                if (distToPeak < scaledMountainRadius)
                {
                    float influence = 1f - (distToPeak / scaledMountainRadius);
                    influence = Mathf.Pow(influence, mountainSharpness);
                    height += influence * maxHeight;
                }
            }
            
            // Ridge influence with scaled radius
            if (generateRidges && _ridgeLines != null)
            {
                float ridgeInfluenceRadius = 30f * Mathf.Sqrt(terrainSize.x / 200f); // Scale ridge influence
                
                for (int i = 0; i < _ridgeLines.Length; i += 2)
                {
                    Vector2 start = _ridgeLines[i];
                    Vector2 end = _ridgeLines[i + 1];
                    
                    float distToRidge = DistanceToLineSegment(localPos, start, end);
                    if (distToRidge < ridgeInfluenceRadius)
                    {
                        float ridgeInfluence = 1f - (distToRidge / ridgeInfluenceRadius);
                        ridgeInfluence = Mathf.Pow(ridgeInfluence, 2f);
                        height += ridgeInfluence * ridgeHeight;
                    }
                }
            }
            
            // Medium scale detail - auto-scale with terrain
            float scaledMediumNoise = mediumNoiseScale / Mathf.Max(terrainSize.x, terrainSize.y) * 200f;
            height += Mathf.PerlinNoise(worldPos.x * scaledMediumNoise, worldPos.z * scaledMediumNoise) * mediumNoiseStrength;
            
            // Fine detail - auto-scale with terrain
            float scaledFineNoise = fineNoiseScale / Mathf.Max(terrainSize.x, terrainSize.y) * 200f;
            height += Mathf.PerlinNoise(worldPos.x * scaledFineNoise, worldPos.z * scaledFineNoise) * fineNoiseStrength;
            
            // Add some fractal noise for more realism - scale with terrain
            float fractalScale = 0.1f / Mathf.Max(terrainSize.x, terrainSize.y) * 200f;
            height += FractalNoise(worldPos.x * fractalScale, worldPos.z * fractalScale, 4) * 3f;
            
            return height;
        }

        float FractalNoise(float x, float z, int octaves)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            
            for (int i = 0; i < octaves; i++)
            {
                value += Mathf.PerlinNoise(x * frequency, z * frequency) * amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            
            return value;
        }

        float DistanceToLineSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 line = end - start;
            Vector2 pointToStart = point - start;
            
            float t = Mathf.Clamp01(Vector2.Dot(pointToStart, line) / Vector2.Dot(line, line));
            Vector2 projection = start + t * line;
            
            return Vector2.Distance(point, projection);
        }

        void CarvePaths()
        {
            var splines = new List<Spline>();
            FindAllSplines(splines);
            
            foreach (var spline in splines)
            {
                CarveSplinePath(spline);
            }
        }

        void CarveSplinePath(Spline spline)
        {
            if (spline.PointCount < 2) return;
            
            Vector3 center = transform.position;
            Vector3 terrainMin = center - new Vector3(terrainSize.x * 0.5f, 0, terrainSize.y * 0.5f);
            
            // Scale sampling based on terrain resolution and size
            float pathDetailScale = Mathf.Max(terrainSize.x, terrainSize.y) / 200f;
            int samples = Mathf.RoundToInt(50 * pathDetailScale); // More samples for bigger terrains
            samples = Mathf.Clamp(samples, 25, 200); // Keep reasonable bounds
            
            int segments = spline.closed ? spline.PointCount : (spline.PointCount - 1);
            int totalSamples = segments * samples;
            
            for (int i = 0; i <= totalSamples; i++)
            {
                float t = i / (float)totalSamples;
                Vector3 worldPoint = spline.GetPoint(t);
                
                // Convert to heightmap coordinates
                Vector2 localPos = new Vector2(
                    worldPoint.x - terrainMin.x,
                    worldPoint.z - terrainMin.z
                );
                
                float u = localPos.x / terrainSize.x;
                float v = localPos.y / terrainSize.y;
                
                if (u >= 0 && u <= 1 && v >= 0 && v <= 1)
                {
                    // Calculate target height for the path (with slope constraints)
                    float pathTargetHeight = worldPoint.y - pathCarvingDepth;
                    
                    // Apply path carving in a circular area
                    int centerX = Mathf.RoundToInt(u * resolution);
                    int centerY = Mathf.RoundToInt(v * resolution);
                    
                    int radius = Mathf.RoundToInt((pathCarvingWidth * 0.5f) * resolution / terrainSize.x);
                    int blendRadius = Mathf.RoundToInt(pathBlendDistance * resolution / terrainSize.x);
                    
                    for (int dy = -blendRadius; dy <= blendRadius; dy++)
                    {
                        for (int dx = -blendRadius; dx <= blendRadius; dx++)
                        {
                            int x = centerX + dx;
                            int y = centerY + dy;
                            
                            if (x >= 0 && x <= resolution && y >= 0 && y <= resolution)
                            {
                                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                                
                                if (distance <= radius)
                                {
                                    // Inside path - carve to target height
                                    _heightMap[x, y] = Mathf.Min(_heightMap[x, y], pathTargetHeight);
                                }
                                else if (distance <= blendRadius)
                                {
                                    // Blend zone - smooth transition
                                    float blendFactor = 1f - ((distance - radius) / (blendRadius - radius));
                                    blendFactor = Mathf.SmoothStep(0f, 1f, blendFactor);
                                    
                                    float targetHeight = Mathf.Lerp(_heightMap[x, y], pathTargetHeight, blendFactor);
                                    _heightMap[x, y] = Mathf.Min(_heightMap[x, y], targetHeight);
                                }
                            }
                        }
                    }
                }
            }
        }

        void ApplyErosion()
        {
            // Simple thermal erosion simulation
            for (int iteration = 0; iteration < erosionIterations; iteration++)
            {
                float[,] newHeightMap = new float[resolution + 1, resolution + 1];
                
                for (int y = 0; y <= resolution; y++)
                {
                    for (int x = 0; x <= resolution; x++)
                    {
                        float currentHeight = _heightMap[x, y];
                        float totalDiff = 0f;
                        int neighborCount = 0;
                        
                        // Check all 8 neighbors
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                
                                int nx = x + dx;
                                int ny = y + dy;
                                
                                if (nx >= 0 && nx <= resolution && ny >= 0 && ny <= resolution)
                                {
                                    float heightDiff = currentHeight - _heightMap[nx, ny];
                                    if (heightDiff > 0)
                                    {
                                        totalDiff += heightDiff;
                                        neighborCount++;
                                    }
                                }
                            }
                        }
                        
                        // Apply erosion
                        if (neighborCount > 0)
                        {
                            float avgDiff = totalDiff / neighborCount;
                            float erosionAmount = avgDiff * erosionStrength;
                            newHeightMap[x, y] = currentHeight - erosionAmount;
                        }
                        else
                        {
                            newHeightMap[x, y] = currentHeight;
                        }
                    }
                }
                
                _heightMap = newHeightMap;
            }
        }

        void BuildMeshFromHeightmap()
        {
            int vertCount = (resolution + 1) * (resolution + 1);
            var vertices = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            
            Vector3 center = transform.position;
            Vector3 terrainMin = center - new Vector3(terrainSize.x * 0.5f, 0, terrainSize.y * 0.5f);
            
            // Build vertices from heightmap
            for (int y = 0; y <= resolution; y++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    int index = y * (resolution + 1) + x;
                    
                    float u = (float)x / resolution;
                    float v = (float)y / resolution;
                    
                    Vector3 worldPos = terrainMin + new Vector3(
                        u * terrainSize.x,
                        _heightMap[x, y],
                        v * terrainSize.y
                    );
                    
                    vertices[index] = worldPos;
                    uvs[index] = new Vector2(u, v);
                    normals[index] = CalculateNormal(x, y);
                }
            }
            
            // Generate triangles
            int triangleCount = resolution * resolution * 6;
            var triangles = new int[triangleCount];
            int triIndex = 0;
            
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int bottomLeft = y * (resolution + 1) + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = (y + 1) * (resolution + 1) + x;
                    int topRight = topLeft + 1;
                    
                    triangles[triIndex++] = bottomLeft;
                    triangles[triIndex++] = topLeft;
                    triangles[triIndex++] = bottomRight;
                    
                    triangles[triIndex++] = bottomRight;
                    triangles[triIndex++] = topLeft;
                    triangles[triIndex++] = topRight;
                }
            }
            
            _terrainMesh.Clear();
            _terrainMesh.indexFormat = (vertCount > 65000) ? 
                UnityEngine.Rendering.IndexFormat.UInt32 : 
                UnityEngine.Rendering.IndexFormat.UInt16;
                
            _terrainMesh.SetVertices(vertices);
            _terrainMesh.SetNormals(normals);
            _terrainMesh.SetUVs(0, new List<Vector2>(uvs));
            _terrainMesh.SetTriangles(triangles, 0);
            _terrainMesh.RecalculateBounds();
            
            _meshFilter.sharedMesh = _terrainMesh;
            _meshCollider.sharedMesh = null;
            _meshCollider.sharedMesh = _terrainMesh;
        }

        Vector3 CalculateNormal(int x, int y)
        {
            float heightL = (x > 0) ? _heightMap[x - 1, y] : _heightMap[x, y];
            float heightR = (x < resolution) ? _heightMap[x + 1, y] : _heightMap[x, y];
            float heightD = (y > 0) ? _heightMap[x, y - 1] : _heightMap[x, y];
            float heightU = (y < resolution) ? _heightMap[x, y + 1] : _heightMap[x, y];
            
            Vector3 normal = new Vector3(heightL - heightR, 2.0f, heightD - heightU);
            return normal.normalized;
        }

        void FindAllSplines(List<Spline> outSplines)
        {
            outSplines.Clear();
            var allSplines = FindObjectsOfType<Spline>();
            foreach (var spline in allSplines)
            {
                if (spline != null && spline.PointCount >= 2)
                {
                    outSplines.Add(spline);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            // Draw terrain bounds
            Gizmos.color = Color.green;
            Vector3 center = transform.position;
            Gizmos.DrawWireCube(center, new Vector3(terrainSize.x, 0, terrainSize.y));
            
            // Draw mountain peaks
            if (_mountainPeakPositions != null)
            {
                Gizmos.color = Color.red;
                Vector3 terrainMin = center - new Vector3(terrainSize.x * 0.5f, 0, terrainSize.y * 0.5f);
                float scaledRadius = mountainRadius * Mathf.Sqrt((terrainSize.x * terrainSize.y) / 40000f * 0.5f);
                
                foreach (var peak in _mountainPeakPositions)
                {
                    Vector3 peakWorld = terrainMin + new Vector3(peak.x, maxHeight, peak.y);
                    Gizmos.DrawWireSphere(peakWorld, scaledRadius * 0.1f);
                }
            }
            
            // Draw ridges
            if (_ridgeLines != null && generateRidges)
            {
                Gizmos.color = Color.yellow;
                Vector3 terrainMin = center - new Vector3(terrainSize.x * 0.5f, 0, terrainSize.y * 0.5f);
                
                for (int i = 0; i < _ridgeLines.Length; i += 2)
                {
                    Vector3 start = terrainMin + new Vector3(_ridgeLines[i].x, ridgeHeight, _ridgeLines[i].y);
                    Vector3 end = terrainMin + new Vector3(_ridgeLines[i + 1].x, ridgeHeight, _ridgeLines[i + 1].y);
                    Gizmos.DrawLine(start, end);
                }
            }
        }

        // Helper methods for getting actual calculated values (useful for debugging)
        public int GetActualPeakCount()
        {
            float terrainArea = terrainSize.x * terrainSize.y;
            float areaMultiplier = terrainArea / 10000f;
            return Mathf.Max(1, Mathf.RoundToInt(peakDensity * areaMultiplier));
        }

        public int GetActualRidgeCount()
        {
            if (!generateRidges) return 0;
            float terrainArea = terrainSize.x * terrainSize.y;
            float areaMultiplier = terrainArea / 10000f;
            return Mathf.Max(0, Mathf.RoundToInt(ridgeDensity * areaMultiplier));
        }

        public float GetScaledMountainRadius()
        {
            float terrainArea = terrainSize.x * terrainSize.y;
            float areaMultiplier = terrainArea / 10000f;
            return mountainRadius * Mathf.Sqrt(areaMultiplier * 0.5f);
        }
    }
}