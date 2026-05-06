using UnityEngine;

public class WheelColliderExtension : MonoBehaviour
{
    [Header("References")]
    public Rigidbody bikeRb;
    public WheelCollider wc;
    
    [Header("Ray Configuration")]
    [Range(3, 16)]
    public int rayCount = 8; // Number of rays around the wheel circumference
    [Range(0.05f, 0.5f)]
    public float rayLength = 0.2f; // Length of rays from wheel center
    [Range(0f, 0.2f)]
    public float raySubtraction = 0.05f; // Subtract from wheel radius for ray length
    
    [Header("Anti-Dig Forces")]
    [Range(100f, 3000f)]
    public float antiDigForce = 800f; // Base force for preventing clipping
    [Range(0f, 1f)]
    public float antiDigDamping = 0.3f; // Damping for anti-dig forces
    
    [Header("Friction Model")]
    [Range(0f, 5f)]
    public float staticFriction = 1.2f; // Static friction coefficient
    [Range(0f, 5f)]
    public float dynamicFriction = 0.8f; // Dynamic friction coefficient
    [Range(0.1f, 10f)]
    public float frictionTransitionSpeed = 2f; // Speed at which static->dynamic transition occurs
    [Range(100f, 2000f)]
    public float maxFrictionForce = 1000f; // Maximum friction force that can be applied
    
    [Header("Surface Interaction")]
    [Range(0f, 2f)]
    public float surfaceGripMultiplier = 1f; // Multiplier for different surface types
    [Range(0.01f, 1f)]
    public float minimumNormalForce = 0.1f; // Minimum normal force required for friction
    
    [Header("Layer Filtering")]
    public LayerMask groundLayerMask = 1; // Layers to interact with
    
    [Header("Debug Visualization")]
    public bool showDebugRays = true;
    public bool showForceVectors = true;
    public Color rayColor = Color.red;
    public Color hitRayColor = Color.green;
    public Color frictionForceColor = Color.blue;
    public Color normalForceColor = Color.yellow;
    
    // Internal variables
    private Vector3 wheelCenter;
    private Quaternion wheelRotation;
    private float wheelRadius;
    private RayHitData[] rayHits;
    private Vector3 totalFrictionForce;
    private Vector3 totalNormalForce;
    private float wheelSpeed;
    private Vector3 wheelVelocity;
    
    // Data structure for ray hit information
    [System.Serializable]
    private class RayHitData
    {
        public bool hasHit;
        public RaycastHit hitInfo;
        public Vector3 rayDirection;
        public Vector3 worldPosition;
        public float penetrationDepth;
        public Vector3 normalForce;
        public Vector3 frictionForce;
        public float forceMultiplier;
        public Vector3 contactVelocity;
        public bool isSliding;
    }
    
    void Start()
    {
        InitializeComponent();
    }
    
    void InitializeComponent()
    {
        if (wc == null)
            wc = GetComponent<WheelCollider>();
            
        if (wc != null)
        {
            wheelRadius = wc.radius;
        }
        
        // Initialize ray hit data array
        rayHits = new RayHitData[rayCount];
        for (int i = 0; i < rayCount; i++)
        {
            rayHits[i] = new RayHitData();
        }
    }
    
    void FixedUpdate()
    {
        if (bikeRb == null || wc == null) return;
        
        UpdateWheelTransform();
        PerformRaycastScan();
        ProcessContactPhysics();
        ApplyForcesToRigidbody();
    }
    
    void UpdateWheelTransform()
    {
        // Get accurate wheel position and rotation
        wc.GetWorldPose(out wheelCenter, out wheelRotation);
        
        // Calculate wheel velocity and speed
        wheelVelocity = bikeRb.GetPointVelocity(wheelCenter);
        wheelSpeed = wheelVelocity.magnitude;
    }

    void PerformRaycastScan()
    {
        // For a wheel, we want rays to go outward from the center in the wheel's rolling plane
        // The wheel's right direction is the axle direction
        Vector3 wheelRight = wheelRotation * Vector3.right;  // This is the axle direction
        Vector3 wheelUp = wheelRotation * Vector3.up;        // This points "up" from the wheel center
        Vector3 wheelForward = wheelRotation * Vector3.forward; // This points "forward" in rolling direction

        float effectiveRayLength = Mathf.Max(0.01f, wheelRadius - raySubtraction + rayLength);

        for (int i = 0; i < rayCount; i++)
        {
            RayHitData rayData = rayHits[i];

            // Calculate angle for this ray (distributed evenly around circumference)
            float angle = (float)i / rayCount * 360f * Mathf.Deg2Rad;

            // Create ray direction in the wheel's rolling plane (perpendicular to the axle)
            // We rotate around the wheel's right axis (axle) to get radial directions
            Vector3 radialDirection = (Mathf.Cos(angle) * wheelUp + Mathf.Sin(angle) * wheelForward).normalized;

            rayData.rayDirection = radialDirection;
            rayData.worldPosition = wheelCenter;

            // Perform raycast from wheel center outward
            RaycastHit hit;
            rayData.hasHit = Physics.Raycast(wheelCenter, radialDirection, out hit, effectiveRayLength, groundLayerMask);

            if (rayData.hasHit)
            {
                rayData.hitInfo = hit;
                rayData.penetrationDepth = Mathf.Max(0, wheelRadius - hit.distance);
                rayData.contactVelocity = bikeRb.GetPointVelocity(hit.point);

                // Calculate force multiplier based on penetration
                rayData.forceMultiplier = Mathf.Clamp01(rayData.penetrationDepth / (wheelRadius * 0.3f));
            }
            else
            {
                rayData.penetrationDepth = 0;
                rayData.forceMultiplier = 0;
                rayData.contactVelocity = Vector3.zero;
            }
        }
    }
    
    void ProcessContactPhysics()
    {
        totalFrictionForce = Vector3.zero;
        totalNormalForce = Vector3.zero;
        
        for (int i = 0; i < rayCount; i++)
        {
            RayHitData rayData = rayHits[i];
            
            if (!rayData.hasHit || rayData.forceMultiplier < 0.01f)
            {
                rayData.normalForce = Vector3.zero;
                rayData.frictionForce = Vector3.zero;
                rayData.isSliding = false;
                continue;
            }
            
            // Calculate normal force (anti-penetration)
            Vector3 normalDirection = rayData.hitInfo.normal;
            float normalForceMagnitude = antiDigForce * rayData.forceMultiplier;
            rayData.normalForce = normalDirection * normalForceMagnitude;
            
            // Add damping to normal force
            Vector3 normalVelocity = Vector3.Project(rayData.contactVelocity, normalDirection);
            Vector3 dampingForce = -normalVelocity * antiDigDamping * rayData.forceMultiplier;
            rayData.normalForce += dampingForce;
            
            // Calculate friction force
            CalculateFrictionForce(rayData);
            
            // Accumulate total forces
            totalNormalForce += rayData.normalForce;
            totalFrictionForce += rayData.frictionForce;
        }
    }
    
    void CalculateFrictionForce(RayHitData rayData)
    {
        if (rayData.normalForce.magnitude < minimumNormalForce)
        {
            rayData.frictionForce = Vector3.zero;
            rayData.isSliding = true;
            return;
        }
        
        // Get tangential velocity (velocity parallel to the surface)
        Vector3 surfaceNormal = rayData.hitInfo.normal;
        Vector3 tangentialVelocity = rayData.contactVelocity - Vector3.Project(rayData.contactVelocity, surfaceNormal);
        float tangentialSpeed = tangentialVelocity.magnitude;
        
        // Determine if we're in static or dynamic friction regime
        bool isStatic = tangentialSpeed < frictionTransitionSpeed;
        float frictionCoefficient = isStatic ? staticFriction : dynamicFriction;
        
        // Apply surface grip multiplier
        frictionCoefficient *= surfaceGripMultiplier;
        
        // Calculate maximum friction force available
        float normalForceMagnitude = rayData.normalForce.magnitude;
        float maxFriction = frictionCoefficient * normalForceMagnitude;
        maxFriction = Mathf.Min(maxFriction, maxFrictionForce * rayData.forceMultiplier);
        
        if (isStatic && tangentialSpeed < 0.1f)
        {
            // Static friction: oppose the tendency to move
            Vector3 appliedForce = GetAppliedTangentialForce(rayData);
            float appliedForceMagnitude = appliedForce.magnitude;
            
            if (appliedForceMagnitude <= maxFriction)
            {
                // Static friction can handle the applied force
                rayData.frictionForce = -appliedForce;
                rayData.isSliding = false;
            }
            else
            {
                // Break into dynamic friction
                rayData.frictionForce = -tangentialVelocity.normalized * maxFriction;
                rayData.isSliding = true;
            }
        }
        else
        {
            // Dynamic friction: oppose the direction of motion
            if (tangentialSpeed > 0.01f)
            {
                Vector3 frictionDirection = -tangentialVelocity.normalized;
                rayData.frictionForce = frictionDirection * maxFriction;
                rayData.isSliding = true;
            }
            else
            {
                rayData.frictionForce = Vector3.zero;
                rayData.isSliding = false;
            }
        }
        
        // Apply smooth transition between static and dynamic friction
        if (!isStatic && tangentialSpeed < frictionTransitionSpeed * 2f)
        {
            float transitionFactor = Mathf.InverseLerp(frictionTransitionSpeed, frictionTransitionSpeed * 2f, tangentialSpeed);
            float staticFrictionMag = staticFriction * normalForceMagnitude;
            float dynamicFrictionMag = rayData.frictionForce.magnitude;
            float blendedMagnitude = Mathf.Lerp(staticFrictionMag, dynamicFrictionMag, transitionFactor);
            
            if (tangentialSpeed > 0.01f)
            {
                rayData.frictionForce = -tangentialVelocity.normalized * blendedMagnitude;
            }
        }
    }
    
    Vector3 GetAppliedTangentialForce(RayHitData rayData)
    {
        // Estimate the tangential force that would be applied to this contact point
        // This is a simplified approximation - in a real scenario, this would involve
        // more complex analysis of the wheel's driving forces
        
        Vector3 wheelForward = wheelRotation * Vector3.forward;
        Vector3 surfaceNormal = rayData.hitInfo.normal;
        Vector3 tangentDirection = Vector3.Cross(Vector3.Cross(surfaceNormal, wheelForward), surfaceNormal).normalized;
        
        // Use wheel collider's motor torque if available
        float estimatedTangentialForce = 0f;
        if (wc.motorTorque != 0)
        {
            estimatedTangentialForce = wc.motorTorque / wheelRadius;
        }
        
        // Add braking forces
        if (wc.brakeTorque != 0)
        {
            estimatedTangentialForce += wc.brakeTorque / wheelRadius;
        }
        
        return tangentDirection * estimatedTangentialForce * rayData.forceMultiplier;
    }
    
    void ApplyForcesToRigidbody()
    {
        if (bikeRb == null) return;
        
        // Apply normal forces (anti-dig)
        if (totalNormalForce.magnitude > 0.01f)
        {
            bikeRb.AddForceAtPosition(totalNormalForce / rayCount, wheelCenter);
        }
        
        // Apply friction forces
        if (totalFrictionForce.magnitude > 0.01f)
        {
            bikeRb.AddForceAtPosition(totalFrictionForce / rayCount, wheelCenter);
        }
    }
    
    void Update()
    {
        // Update visualization in editor
        if (!Application.isPlaying && wc != null)
        {
            UpdateWheelTransform();
        }
    }

    // Fixed OnDrawGizmos section with correct ray directions
    void OnDrawGizmos()
    {
        if (!showDebugRays) return;

        // Initialize if needed
        if (rayHits == null || rayHits.Length != rayCount)
        {
            if (wc != null)
            {
                wheelRadius = wc.radius;
                UpdateWheelTransform();
            }
            else
            {
                wheelCenter = transform.position;
                wheelRotation = transform.rotation;
                wheelRadius = 0.3f;
            }

            rayHits = new RayHitData[rayCount];
            for (int i = 0; i < rayCount; i++)
            {
                rayHits[i] = new RayHitData();
            }
        }

        // Draw rays using the same calculation as in PerformRaycastScan
        Vector3 wheelRight = wheelRotation * Vector3.right;    // Axle direction
        Vector3 wheelUp = wheelRotation * Vector3.up;          // Wheel up
        Vector3 wheelForward = wheelRotation * Vector3.forward; // Rolling direction
        float effectiveRayLength = wheelRadius - raySubtraction + rayLength;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = (float)i / rayCount * 360f * Mathf.Deg2Rad;
            // Create radial direction in the wheel's rolling plane (perpendicular to axle)
            Vector3 rayDirection = (Mathf.Cos(angle) * wheelUp + Mathf.Sin(angle) * wheelForward).normalized;

            // Choose color based on hit status and force
            Color currentRayColor = rayColor;
            if (Application.isPlaying && rayHits[i] != null && rayHits[i].hasHit)
            {
                float forceIntensity = Mathf.Clamp01(rayHits[i].forceMultiplier);
                currentRayColor = Color.Lerp(hitRayColor, Color.red, forceIntensity);
            }

            Gizmos.color = currentRayColor;
            Gizmos.DrawRay(wheelCenter, rayDirection * effectiveRayLength);

            // Draw force vectors if enabled and in play mode
            if (showForceVectors && Application.isPlaying && rayHits[i] != null && rayHits[i].hasHit)
            {
                Vector3 hitPoint = rayHits[i].hitInfo.point;

                // Draw normal force
                Gizmos.color = normalForceColor;
                Gizmos.DrawRay(hitPoint, rayHits[i].normalForce.normalized * 0.2f);

                // Draw friction force
                Gizmos.color = frictionForceColor;
                Gizmos.DrawRay(hitPoint, rayHits[i].frictionForce.normalized * 0.2f);

                // Draw contact point
                Gizmos.color = rayHits[i].isSliding ? Color.red : Color.green;
                Gizmos.DrawWireSphere(hitPoint, 0.02f);
            }
        }

        // Draw wheel center and axle direction for reference
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(wheelCenter, 0.03f);
        
        // Draw axle direction (should be perpendicular to the ray plane)
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(wheelCenter, wheelRight * 0.1f);

        // Draw total force vectors
        if (showForceVectors && Application.isPlaying)
        {
            Gizmos.color = normalForceColor;
            Gizmos.DrawRay(wheelCenter, totalNormalForce.normalized * 0.3f);

            Gizmos.color = frictionForceColor;
            Gizmos.DrawRay(wheelCenter, totalFrictionForce.normalized * 0.3f);
        }
    }
    
    // Public methods for external access
    public bool IsWheelSliding()
    {
        if (rayHits == null) return false;
        
        foreach (var ray in rayHits)
        {
            if (ray.hasHit && ray.isSliding) return true;
        }
        return false;
    }
    
    public float GetTotalContactForce()
    {
        return (totalNormalForce + totalFrictionForce).magnitude;
    }
    
    public Vector3 GetTotalFrictionForce()
    {
        return totalFrictionForce;
    }
    
    public Vector3 GetTotalNormalForce()
    {
        return totalNormalForce;
    }
    
    public int GetActiveContactCount()
    {
        if (rayHits == null) return 0;
        
        int count = 0;
        foreach (var ray in rayHits)
        {
            if (ray.hasHit && ray.forceMultiplier > 0.01f) count++;
        }
        return count;
    }
}