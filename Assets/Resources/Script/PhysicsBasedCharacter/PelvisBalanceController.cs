using UnityEngine;

public class PelvisBalanceController : MonoBehaviour
{
    [Header("Pelvis Settings")]
    public Rigidbody pelvisRigidbody;
    public Transform pelvisTransform;
    
    [Header("Balance Forces")]
    [Range(0f, 10000f)]
    public float uprightForce = 1000f;
    
    [Range(0f, 500f)]
    public float uprightDamping = 50f;
    
    [Range(0f, 5000f)]
    public float stabilityForce = 500f;
    
    [Range(0f, 100f)]
    public float stabilityDamping = 20f;
    
    [Header("Movement Awareness")]
    [Range(0f, 1f)]
    public float movementDetectionThreshold = 0.1f;
    [Range(0f, 1f)]
    public float stabilityReductionFactor = 0.2f; // How much to reduce stability when moving
    
    [Header("Balance Limits")]
    [Range(0f, 90f)]
    public float maxTiltAngle = 45f;
    
    [Range(0f, 5f)]
    public float balanceHeight = 1.0f;
    
    [Header("Ground Detection")]
    public LayerMask groundLayer = 1;
    public float groundCheckDistance = 0.1f;
    
    [Header("Debug")]
    public bool showDebugGizmos = true;
    
    private Vector3 targetUpDirection = Vector3.up;
    private Vector3 lastPosition;
    private bool isGrounded;
    private Vector3 intendedMovement = Vector3.zero;
    private float movementMagnitude = 0f;
    
    // Public method to be called by movement controller
    public void SetIntendedMovement(Vector3 movement)
    {
        intendedMovement = movement;
        movementMagnitude = movement.magnitude;
    }
    
    void Start()
    {
        // Auto-assign if not set
        if (pelvisRigidbody == null)
            pelvisRigidbody = GetComponent<Rigidbody>();
        
        if (pelvisTransform == null)
            pelvisTransform = transform;
            
        lastPosition = pelvisTransform.position;
    }
    
    void FixedUpdate()
    {
        if (pelvisRigidbody == null) return;
        
        CheckGrounded();
        ApplyBalanceForces();
    }
    
    void CheckGrounded()
    {
        // Raycast downward to check if ragdoll is grounded
        RaycastHit hit;
        Vector3 rayOrigin = pelvisTransform.position;
        
        isGrounded = Physics.Raycast(rayOrigin, Vector3.down, out hit, 
            balanceHeight + groundCheckDistance, groundLayer);
    }
    
    void ApplyBalanceForces()
    {
        if (!isGrounded) return;
        
        // Get current rotation without Y component
        Vector3 currentUp = pelvisTransform.up;
        Vector3 currentForward = pelvisTransform.forward;
        Vector3 currentRight = pelvisTransform.right;
        
        // Project current up vector onto XZ plane to ignore Y rotation
        Vector3 projectedUp = new Vector3(currentUp.x, currentUp.y, currentUp.z);
        Vector3 projectedForward = new Vector3(currentForward.x, 0f, currentForward.z).normalized;
        
        // Calculate tilt angles
        float tiltAngleX = Vector3.SignedAngle(Vector3.up, 
            Vector3.ProjectOnPlane(currentUp, Vector3.right), Vector3.right);
        float tiltAngleZ = Vector3.SignedAngle(Vector3.up, 
            Vector3.ProjectOnPlane(currentUp, Vector3.forward), Vector3.forward);
        
        // Only apply forces if tilt is within limits (AND condition - both axes must be within limits)
        if (Mathf.Abs(tiltAngleX) < maxTiltAngle && Mathf.Abs(tiltAngleZ) < maxTiltAngle)
        {
            ApplyUprightForce(currentUp, tiltAngleX, tiltAngleZ);
            ApplyStabilityForce();
        }
    }
    
    void ApplyUprightForce(Vector3 currentUp, float tiltX, float tiltZ)
    {
        // Calculate the torque needed to right the pelvis
        Vector3 torqueAxis = Vector3.Cross(currentUp, targetUpDirection);
        
        // Remove Y component from torque to preserve Y rotation
        torqueAxis.y = 0f;
        
        if (torqueAxis.magnitude > 0.01f)
        {
            // Apply proportional torque
            Vector3 uprightTorque = torqueAxis.normalized * uprightForce * torqueAxis.magnitude;
            
            // Add damping to prevent oscillation
            Vector3 angularVelocity = pelvisRigidbody.angularVelocity;
            Vector3 dampingTorque = new Vector3(
                -angularVelocity.x * uprightDamping,
                0f, // Don't damp Y rotation
                -angularVelocity.z * uprightDamping
            );
            
            pelvisRigidbody.AddTorque(uprightTorque + dampingTorque, ForceMode.Force);
        }
    }
    
    void ApplyStabilityForce()
    {
        // Calculate velocity-based stability
        Vector3 velocity = pelvisRigidbody.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        
        // Determine if we're intentionally moving
        bool isIntentionallyMoving = movementMagnitude > movementDetectionThreshold;
        
        // Reduce stability force when moving intentionally
        float currentStabilityForce = stabilityForce;
        float currentStabilityDamping = stabilityDamping;
        
        if (isIntentionallyMoving)
        {
            currentStabilityForce *= stabilityReductionFactor;
            currentStabilityDamping *= stabilityReductionFactor;
            
            // If we have intended movement, don't counter it
            Vector3 intendedHorizontal = new Vector3(intendedMovement.x, 0f, intendedMovement.z);
            Vector3 unintendedVelocity = horizontalVelocity - intendedHorizontal;
            
            // Only apply stability force to unintended movement
            if (unintendedVelocity.magnitude > 0.1f)
            {
                Vector3 stabilityForceVector = -unintendedVelocity.normalized * 
                    currentStabilityForce * Mathf.Clamp01(unintendedVelocity.magnitude);
                    
                pelvisRigidbody.AddForce(stabilityForceVector, ForceMode.Force);
            }
        }
        else
        {
            // Apply normal stability force when not moving intentionally
            if (horizontalVelocity.magnitude > 0.1f)
            {
                Vector3 stabilityForceVector = -horizontalVelocity.normalized * 
                    currentStabilityForce * Mathf.Clamp01(horizontalVelocity.magnitude);
                    
                pelvisRigidbody.AddForce(stabilityForceVector, ForceMode.Force);
            }
        }
        
        // Add slight upward force to maintain height
        if (isGrounded)
        {
            float heightForce = currentStabilityForce * 0.3f;
            pelvisRigidbody.AddForce(Vector3.up * heightForce, ForceMode.Force);
        }
        
        // Apply stability damping (reduced when moving intentionally)
        Vector3 stabilityDampingForce = new Vector3(
            -velocity.x * currentStabilityDamping,
            -velocity.y * currentStabilityDamping * 0.5f, // Less Y damping
            -velocity.z * currentStabilityDamping
        );
        
        pelvisRigidbody.AddForce(stabilityDampingForce, ForceMode.Force);
        
        // Decay intended movement over time
        intendedMovement = Vector3.Lerp(intendedMovement, Vector3.zero, Time.fixedDeltaTime * 2f);
        movementMagnitude = intendedMovement.magnitude;
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos || pelvisTransform == null) return;
        
        // Draw upright direction
        Gizmos.color = Color.green;
        Gizmos.DrawRay(pelvisTransform.position, targetUpDirection * 2f);
        
        // Draw current up direction
        Gizmos.color = Color.red;
        Gizmos.DrawRay(pelvisTransform.position, pelvisTransform.up * 2f);
        
        // Draw intended movement
        if (intendedMovement.magnitude > 0.01f)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(pelvisTransform.position, intendedMovement * 3f);
        }
        
        // Draw ground check
        Gizmos.color = isGrounded ? Color.blue : Color.yellow;
        Vector3 groundCheckStart = pelvisTransform.position;
        Vector3 groundCheckEnd = groundCheckStart + Vector3.down * (balanceHeight + groundCheckDistance);
        Gizmos.DrawLine(groundCheckStart, groundCheckEnd);
        
        // Draw balance sphere
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pelvisTransform.position, balanceHeight);
    }
}