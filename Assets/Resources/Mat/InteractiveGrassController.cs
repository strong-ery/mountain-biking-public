using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[System.Serializable]
public class GrassInteraction
{
    public Vector3 position;
    public float startTime;
    public bool isActive;
}

public class InteractiveGrassController : MonoBehaviour
{
    [Header("Grass Mesh Setup")]
    public Mesh grassBladeMesh;
    public Material grassMaterial;
    public MeshRenderer[] terrainMeshes; // Your terrain mesh renderers
    
    [Header("Spawning Parameters")]
    public int grassPerUnit = 50;
    public float minDistance = 0.1f;
    public float maxSlope = 45f;
    public float sampleResolution = 0.5f; // How densely to sample vertices
    
    [Header("Interaction Settings")]
    public LayerMask interactionLayers = -1;
    public float interactionCheckRadius = 1f;
    public string[] interactionTags = { "Player", "Enemy" };
    
    [Header("Performance")]
    public bool useGPUInstancing = true;
    public int maxInstancesPerBatch = 1023;
    
    private List<Matrix4x4> grassMatrices = new List<Matrix4x4>();
    private List<Vector4> grassColors = new List<Vector4>();
    
    private GrassInteraction[] interactions = new GrassInteraction[4];
    
    // Property IDs for shader
    private int interactionPos1ID;
    private int interactionPos2ID;
    private int interactionPos3ID;
    private int interactionPos4ID;
    
    void Start()
    {
        InitializeGrassSystem();
        SpawnGrassOnMeshes();
        
        // Cache shader property IDs
        interactionPos1ID = Shader.PropertyToID("_InteractionPos1");
        interactionPos2ID = Shader.PropertyToID("_InteractionPos2");
        interactionPos3ID = Shader.PropertyToID("_InteractionPos3");
        interactionPos4ID = Shader.PropertyToID("_InteractionPos4");
    }
    
    void InitializeGrassSystem()
    {
        if (terrainMeshes == null || terrainMeshes.Length == 0)
        {
            Debug.LogError("No terrain meshes assigned!");
            return;
        }
        
        // Initialize interactions array
        for (int i = 0; i < interactions.Length; i++)
        {
            interactions[i] = new GrassInteraction();
        }
    }
    
    void SpawnGrassOnMeshes()
    {
        grassMatrices.Clear();
        grassColors.Clear();
        
        foreach (var meshRenderer in terrainMeshes)
        {
            if (meshRenderer == null) continue;
            
            MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;
            
            SpawnGrassOnSingleMesh(meshFilter, meshRenderer.transform);
        }
        
        Debug.Log($"Spawned {grassMatrices.Count} grass instances across {terrainMeshes.Length} terrain meshes");
    }
    
    void SpawnGrassOnSingleMesh(MeshFilter meshFilter, Transform meshTransform)
    {
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Color[] colors = mesh.colors;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;
        
        // If no vertex colors, skip this mesh
        if (colors == null || colors.Length == 0)
        {
            Debug.LogWarning($"Mesh {meshFilter.name} has no vertex colors - skipping grass spawn");
            return;
        }
        
        // Sample points across the mesh surface
        for (int i = 0; i < triangles.Length; i += 3)
        {
            // Get triangle vertices
            int v1 = triangles[i];
            int v2 = triangles[i + 1]; 
            int v3 = triangles[i + 2];
            
            Vector3 vert1 = vertices[v1];
            Vector3 vert2 = vertices[v2];
            Vector3 vert3 = vertices[v3];
            
            Color color1 = colors[v1];
            Color color2 = colors[v2];
            Color color3 = colors[v3];
            
            Vector3 normal1 = normals[v1];
            Vector3 normal2 = normals[v2];
            Vector3 normal3 = normals[v3];
            
            // Sample multiple points within this triangle
            int samplesPerTriangle = Mathf.RoundToInt(CalculateTriangleArea(vert1, vert2, vert3) / (sampleResolution * sampleResolution));
            samplesPerTriangle = Mathf.Clamp(samplesPerTriangle, 1, 10);
            
            for (int s = 0; s < samplesPerTriangle; s++)
            {
                // Generate random barycentric coordinates
                float r1 = Random.Range(0f, 1f);
                float r2 = Random.Range(0f, 1f);
                
                if (r1 + r2 > 1f)
                {
                    r1 = 1f - r1;
                    r2 = 1f - r2;
                }
                float r3 = 1f - r1 - r2;
                
                // Interpolate position, color, and normal
                Vector3 localPos = r1 * vert1 + r2 * vert2 + r3 * vert3;
                Color interpolatedColor = r1 * color1 + r2 * color2 + r3 * color3;
                Vector3 interpolatedNormal = (r1 * normal1 + r2 * normal2 + r3 * normal3).normalized;
                
                // Transform to world space
                Vector3 worldPos = meshTransform.TransformPoint(localPos);
                Vector3 worldNormal = meshTransform.TransformDirection(interpolatedNormal);
                
                // Check if this point should have grass (green channel dominant)
                float greenWeight = interpolatedColor.g;
                float redWeight = interpolatedColor.r;
                float blueWeight = interpolatedColor.b;
                
                if (greenWeight > 0.3f && greenWeight > redWeight && greenWeight > blueWeight)
                {
                    // Check slope
                    float slope = Vector3.Angle(worldNormal, Vector3.up);
                    if (slope <= maxSlope)
                    {
                        SpawnGrassClusterAt(worldPos, worldNormal, greenWeight, redWeight, blueWeight);
                    }
                }
            }
        }
    }
    
    float CalculateTriangleArea(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        Vector3 side1 = v2 - v1;
        Vector3 side2 = v3 - v1;
        return Vector3.Cross(side1, side2).magnitude * 0.5f;
    }
    
    void SpawnGrassClusterAt(Vector3 centerPos, Vector3 surfaceNormal, float green, float red, float blue)
    {
        // Spawn multiple grass blades in a small area
        int grassCount = Mathf.RoundToInt(grassPerUnit * green * 0.1f); // Scale down for reasonable count
        
        for (int i = 0; i < grassCount; i++)
        {
            // Random offset within a small radius
            Vector3 randomOffset = new Vector3(
                Random.Range(-0.3f, 0.3f),
                0,
                Random.Range(-0.3f, 0.3f)
            );
            
            Vector3 grassPos = centerPos + randomOffset;
            
            // Raycast down to find the exact surface position
            RaycastHit hit;
            if (Physics.Raycast(grassPos + Vector3.up * 2f, Vector3.down, out hit, 4f))
            {
                grassPos = hit.point;
                surfaceNormal = hit.normal;
            }
            
            // Orient grass to surface normal
            Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
            Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            Quaternion finalRotation = surfaceRotation * randomRotation;
            
            // Random scale
            float scale = Random.Range(0.8f, 1.2f);
            Vector3 scaleVec = new Vector3(scale, scale, scale);
            
            Matrix4x4 matrix = Matrix4x4.TRS(grassPos, finalRotation, scaleVec);
            grassMatrices.Add(matrix);
            
            // Store the color weights for this grass instance
            Vector4 colorWeights = new Vector4(red, green, blue, 0);
            grassColors.Add(colorWeights);
        }
    }
    
    void Update()
    {
        CheckForInteractions();
        UpdateShaderInteractions();
        RenderGrass();
    }
    
    void CheckForInteractions()
    {
        // Find objects that can interact with grass
        Collider[] interactors = Physics.OverlapSphere(transform.position, interactionCheckRadius, interactionLayers);
        
        foreach (var interactor in interactors)
        {
            bool shouldInteract = false;
            
            // Check if object has one of the interaction tags
            foreach (string tag in interactionTags)
            {
                if (interactor.CompareTag(tag))
                {
                    shouldInteract = true;
                    break;
                }
            }
            
            if (shouldInteract)
            {
                Vector3 interactionPos = interactor.bounds.center;
                AddInteraction(interactionPos);
            }
        }
    }
    
    void AddInteraction(Vector3 position)
    {
        // Find an available interaction slot or replace the oldest one
        int targetSlot = -1;
        float oldestTime = float.MaxValue;
        
        for (int i = 0; i < interactions.Length; i++)
        {
            if (!interactions[i].isActive)
            {
                targetSlot = i;
                break;
            }
            else if (interactions[i].startTime < oldestTime)
            {
                oldestTime = interactions[i].startTime;
                targetSlot = i;
            }
        }
        
        if (targetSlot >= 0)
        {
            interactions[targetSlot].position = position;
            interactions[targetSlot].startTime = Time.time;
            interactions[targetSlot].isActive = true;
        }
    }
    
    void UpdateShaderInteractions()
    {
        // Send interaction data to shader
        for (int i = 0; i < interactions.Length; i++)
        {
            Vector4 interactionData = Vector4.zero;
            
            if (interactions[i].isActive)
            {
                interactionData = new Vector4(
                    interactions[i].position.x,
                    interactions[i].position.y,
                    interactions[i].position.z,
                    interactions[i].startTime
                );
                
                // Deactivate old interactions
                if (Time.time - interactions[i].startTime > 5f) // 5 second max interaction time
                {
                    interactions[i].isActive = false;
                }
            }
            
            // Set shader properties based on slot
            switch (i)
            {
                case 0: grassMaterial.SetVector(interactionPos1ID, interactionData); break;
                case 1: grassMaterial.SetVector(interactionPos2ID, interactionData); break;
                case 2: grassMaterial.SetVector(interactionPos3ID, interactionData); break;
                case 3: grassMaterial.SetVector(interactionPos4ID, interactionData); break;
            }
        }
    }
    
    void RenderGrass()
    {
        if (grassMatrices.Count == 0 || grassMaterial == null || grassBladeMesh == null)
            return;
            
        // Render grass instances
        if (useGPUInstancing)
        {
            RenderGrassInstanced();
        }
        else
        {
            RenderGrassIndividual();
        }
    }
    
    void RenderGrassInstanced()
    {
        // Split into batches to stay under the instancing limit
        int batchCount = Mathf.CeilToInt((float)grassMatrices.Count / maxInstancesPerBatch);
        
        for (int batch = 0; batch < batchCount; batch++)
        {
            int startIndex = batch * maxInstancesPerBatch;
            int count = Mathf.Min(maxInstancesPerBatch, grassMatrices.Count - startIndex);
            
            Matrix4x4[] batchMatrices = new Matrix4x4[count];
            for (int i = 0; i < count; i++)
            {
                batchMatrices[i] = grassMatrices[startIndex + i];
            }
            
            Graphics.DrawMeshInstanced(
                grassBladeMesh,
                0,
                grassMaterial,
                batchMatrices,
                count
            );
        }
    }
    
    void RenderGrassIndividual()
    {
        // Fallback: render each grass blade individually (slower)
        for (int i = 0; i < grassMatrices.Count; i++)
        {
            Graphics.DrawMesh(
                grassBladeMesh,
                grassMatrices[i],
                grassMaterial,
                0
            );
        }
    }
    
    void OnDestroy()
    {
        // Clean up any buffers if we had them
    }
    
    void OnDrawGizmos()
    {
        // Draw interaction check radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionCheckRadius);
        
        // Draw active interactions
        if (interactions != null)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < interactions.Length; i++)
            {
                if (interactions[i] != null && interactions[i].isActive)
                {
                    Gizmos.DrawWireSphere(interactions[i].position, 1f);
                }
            }
        }
    }
}