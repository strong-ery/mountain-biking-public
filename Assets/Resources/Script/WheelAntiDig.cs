using UnityEngine;

public class WheelAntiDig : MonoBehaviour
{
    [Header("References")]
    public Rigidbody bikeRb;
    public WheelCollider wc;
    
    [Header("Anti-Dig Settings")]
    [Range(0f, 2f)]
    public float rayOffsetDistance = 0.3f; // 0 = straight down, 1 = 45 degrees, 2 = 90 degrees (horizontal)
    [Range(100f, 2000f)]
    public float antiDigForce = 500f; // Force applied when rays hit
    [Range(0f, 1f)]
    public float dampingFactor = 0.5f; // Damping for the applied 
    public float raySubtraction = 0.1f;
    public bool invert = false;

    public bool useXRotation;
    public bool useYRotation;
    public bool useZRotation;
    
    [Header("Layer Filtering")]
    public LayerMask groundLayerMask = 1; // Default to layer 0 (Default layer)
    
    [Header("Debug")]
    public bool showDebugRays = true;
    public Color rayColor = Color.red;
    
    private Vector3 wheelCenter;
    private Quaternion wheelRotation;
    private Vector3 downDirection;
    private float wheelRadius;
    private float suspensionDistance;
    
    // For gizmo visualization
    private float lastForwardForce = 0f;
    private float lastBackwardForce = 0f;
    private Vector3 forwardRayDir;
    private Vector3 backwardRayDir;
    private float rayDistance;
    
    void Start()
    {
        if (wc == null)
            wc = GetComponent<WheelCollider>();
            
        wheelRadius = wc.radius;
        suspensionDistance = wc.suspensionDistance;
    }

    void FixedUpdate()
    {
        if (bikeRb == null || wc == null) return;
        
        UpdateWheelTransform();
        PerformAntiDigRaycasts();
    }
    
    void UpdateWheelTransform()
    {
        // Get the wheel's accurate world position and rotation
        if (wc != null)
        {
            wc.GetWorldPose(out wheelCenter, out wheelRotation);
        }
        else
        {
            wheelCenter = transform.position;
            wheelRotation = transform.rotation;
        }
    }
    
    void PerformAntiDigRaycasts()
    {
        // Use the wheel's actual rotation for directions
        Vector3 wheelUp = wheelRotation * Vector3.up;
        Vector3 wheelForward = wheelRotation * Vector3.forward;
        
        // Base downward direction based on wheel's actual orientation
        if (!invert)
        {
            downDirection = -wheelUp;
        }
        else
        {
            downDirection = wheelUp;            
        }
        
        // Calculate angle in degrees (0 to 90 degrees)
        float angleInDegrees = rayOffsetDistance * 45f; // 0-2 maps to 0-90 degrees
        
        // Create angled ray directions using wheel's actual orientation
        // Forward ray: rotates down vector toward the front of the wheel
        forwardRayDir = Quaternion.AngleAxis(angleInDegrees, Vector3.Cross(downDirection, wheelForward)) * downDirection;
        forwardRayDir = forwardRayDir.normalized;
        
        // Backward ray: rotates down vector toward the back of the wheel  
        backwardRayDir = Quaternion.AngleAxis(angleInDegrees, Vector3.Cross(downDirection, -wheelForward)) * downDirection;
        backwardRayDir = backwardRayDir.normalized;
        
        // Cast rays from wheel center in these angled directions
        RaycastHit forwardHit, backwardHit;
        // Ray length should only go to the wheel surface, not beyond
        rayDistance = Mathf.Max(0.01f, wheelRadius - raySubtraction);
        
        // Use layer mask for raycasting to only hit ground objects
        bool forwardHitDetected = Physics.Raycast(wheelCenter, forwardRayDir, out forwardHit, rayDistance, groundLayerMask);
        bool backwardHitDetected = Physics.Raycast(wheelCenter, backwardRayDir, out backwardHit, rayDistance, groundLayerMask);
        
        // Reset forces
        lastForwardForce = 0f;
        lastBackwardForce = 0f;
        
        // Process forward ray collision
        if (forwardHitDetected)
        {
            lastForwardForce = ProcessRayHit(forwardHit, wheelCenter, wheelForward, true);
        }
        
        // Process backward ray collision
        if (backwardHitDetected)
        {
            lastBackwardForce = ProcessRayHit(backwardHit, wheelCenter, wheelForward, false);
        }
        
        // Debug visualization
        if (showDebugRays)
        {
            Debug.DrawRay(wheelCenter, forwardRayDir * rayDistance, forwardHitDetected ? Color.green : rayColor);
            Debug.DrawRay(wheelCenter, backwardRayDir * rayDistance, backwardHitDetected ? Color.green : rayColor);
        }
    }
    
    float ProcessRayHit(RaycastHit hit, Vector3 rayOrigin, Vector3 wheelForward, bool isForwardRay)
    {
        // Calculate how much the ray penetrated into the surface
        float distanceToSurface = hit.distance;
        float penetration = wheelRadius - distanceToSurface;
        
        if (penetration > 0)
        {
            // Calculate force based on penetration depth
            float forceMultiplier = Mathf.Clamp01(penetration / wheelRadius);
            Vector3 forceDirection = hit.normal;
            
            // Apply upward force to counteract the "digging"
            Vector3 antiDigForceVector = forceDirection * antiDigForce * forceMultiplier;
            
            // Add some forward/backward bias based on which ray hit, using actual wheel forward direction
            if (isForwardRay)
            {
                // If front of tire is hitting, add slight backward force
                antiDigForceVector += -wheelForward * (antiDigForce * 0.2f * forceMultiplier);
            }
            else
            {
                // If back of tire is hitting, add slight forward force
                antiDigForceVector += wheelForward * (antiDigForce * 0.2f * forceMultiplier);
            }
            
            // Apply damping based on velocity
            Vector3 wheelVelocity = bikeRb.GetPointVelocity(wheelCenter);
            Vector3 dampingForce = -wheelVelocity * dampingFactor * forceMultiplier;
            
            // Apply the final force to the rigidbody at the wheel position
            bikeRb.AddForceAtPosition(antiDigForceVector + dampingForce, wheelCenter);
            
            // Debug visualization for force direction
            if (showDebugRays)
            {
                Debug.DrawRay(hit.point, antiDigForceVector.normalized * 0.5f, Color.yellow);
            }
            
            // Return the force magnitude for visualization
            return antiDigForceVector.magnitude;
        }
        
        return 0f;
    }
    
    void Update()
    {
        // Update for editor visualization
        if (!Application.isPlaying)
        {
            if (wc == null)
                wc = GetComponent<WheelCollider>();
                
            if (wc != null)
            {
                wheelRadius = wc.radius;
                UpdateWheelTransform();
                CalculateRayDirections();
            }
        }
        
        // Keep debug rays visible in Update for better visualization
        if (showDebugRays && Application.isPlaying)
        {
            // Apply rotation filtering for debug rays
            Vector3 wheelUp, wheelForward;
            Vector3 eulerAngles = wheelRotation.eulerAngles;
            Vector3 filteredEuler = Vector3.zero;
            
            if (useXRotation) filteredEuler.x = eulerAngles.x;
            if (useYRotation) filteredEuler.y = eulerAngles.y;
            if (useZRotation) filteredEuler.z = eulerAngles.z;
            
            Quaternion filteredRotation = Quaternion.Euler(filteredEuler);
            wheelUp = filteredRotation * Vector3.up;
            wheelForward = filteredRotation * Vector3.forward;
            
            Vector3 downDirection = !invert ? -wheelUp : wheelUp;
            
            // Calculate angle in degrees (0 to 90 degrees)
            float angleInDegrees = rayOffsetDistance * 45f; // 0-2 maps to 0-90 degrees
            
            // Create angled ray directions using filtered rotation
            Vector3 forwardRayDir = Quaternion.AngleAxis(angleInDegrees, Vector3.Cross(downDirection, wheelForward)) * downDirection;
            forwardRayDir = forwardRayDir.normalized;
            
            Vector3 backwardRayDir = Quaternion.AngleAxis(angleInDegrees, Vector3.Cross(downDirection, -wheelForward)) * downDirection;
            backwardRayDir = backwardRayDir.normalized;
            
            float rayDistance = wheelRadius;
            
            Debug.DrawRay(wheelCenter, forwardRayDir * rayDistance, rayColor, Time.deltaTime);
            Debug.DrawRay(wheelCenter, backwardRayDir * rayDistance, rayColor, Time.deltaTime);
        }
    }
    
    void CalculateRayDirections()
    {
        // Use wheel's actual rotation for calculations
        Vector3 wheelUp = (wc != null) ? wheelRotation * Vector3.up : transform.up;
        Vector3 wheelForward = (wc != null) ? wheelRotation * Vector3.forward : transform.forward;
        
        // Base downward direction
        downDirection = !invert ? -wheelUp : wheelUp;
        
        // Calculate angle in degrees (0 to 90 degrees)
        float angleInDegrees = rayOffsetDistance * 45f; // 0-2 maps to 0-90 degrees
        
        // Create angled ray directions
        forwardRayDir = Quaternion.AngleAxis(angleInDegrees, Vector3.Cross(downDirection, wheelForward)) * downDirection;
        forwardRayDir = forwardRayDir.normalized;
        
        backwardRayDir = Quaternion.AngleAxis(angleInDegrees, Vector3.Cross(downDirection, -wheelForward)) * downDirection;
        backwardRayDir = backwardRayDir.normalized;
        
        rayDistance = wheelRadius;
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugRays) return;
        
        // Ensure we have valid data for editor visualization
        if (!Application.isPlaying)
        {
            if (wc == null)
                wc = GetComponent<WheelCollider>();
                
            if (wc != null)
            {
                wheelRadius = wc.radius;
                UpdateWheelTransform();
                CalculateRayDirections();
            }
            else
            {
                wheelCenter = transform.position;
                wheelRotation = transform.rotation;
                wheelRadius = 0.3f; // Default radius if no wheel collider
                CalculateRayDirections();
            }
        }
        
        // Draw rays with force-based coloring
        if (Application.isPlaying)
        {
            // In play mode, use actual force values
            Color forwardColor = GetForceColor(lastForwardForce);
            Color backwardColor = GetForceColor(lastBackwardForce);
            
            Gizmos.color = forwardColor;
            Gizmos.DrawRay(wheelCenter, forwardRayDir * rayDistance);
            
            Gizmos.color = backwardColor;
            Gizmos.DrawRay(wheelCenter, backwardRayDir * rayDistance);
        }
        else
        {
            // In edit mode, show potential force based on settings
            float potentialForce = antiDigForce;
            Color potentialColor = GetForceColor(potentialForce);
            
            Gizmos.color = potentialColor;
            Gizmos.DrawRay(wheelCenter, forwardRayDir * rayDistance);
            Gizmos.DrawRay(wheelCenter, backwardRayDir * rayDistance);
        }
        
        // Draw wheel center
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(wheelCenter, 0.02f);
        
        // Draw layer mask info in scene view (editor only)
        if (!Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(wheelCenter + Vector3.up * 0.1f, Vector3.one * 0.05f);
        }
    }
    
    Color GetForceColor(float force)
    {
        // Normalize force from 0-2000 to 0-1
        float normalizedForce = Mathf.Clamp01(force / 2000f);
        
        // Lerp from green (low force) to red (high force)
        return Color.Lerp(Color.green, Color.red, normalizedForce);
    }
}