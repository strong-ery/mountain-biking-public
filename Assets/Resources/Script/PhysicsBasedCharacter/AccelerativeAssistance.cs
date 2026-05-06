using UnityEngine;

public class AccelerativeAssistance : MonoBehaviour
{
    public DualGroundFootHandler footHandler;
    public Rigidbody pelvis;

    [Header("Directional Force Settings")]
    [Tooltip("Force multiplier for forward movement (most efficient)")]
    public float forwardMultiplier = 10f;
    [Tooltip("Force multiplier for backward movement (less efficient)")]
    public float backwardMultiplier = 6f;
    [Tooltip("Force multiplier for left movement")]
    public float leftMultiplier = 7f;
    [Tooltip("Force multiplier for right movement")]
    public float rightMultiplier = 7f;
    
    [Header("General Force Settings")]
    public float maxForce = 50f;
    public ForceMode forceMode = ForceMode.Force;

    [Header("Movement Direction Setup")]
    [Tooltip("Define which direction is 'forward' for your character")]
    public Vector3 forwardDirection = Vector3.up; // Changed from Vector3.forward to Vector3.up
    [Tooltip("Define which direction is 'right' for your character")]
    public Vector3 rightDirection = Vector3.right;

    [Header("Debug Info")]
    public Quaternion averageRotationDirection; // For debugging
    public Vector3 lastAppliedForce; // For debugging
    public Vector3 currentInputDirection; // For debugging

    void Update()
    {
        // Check if at least one foot is grounded
        bool anyFootGrounded = footHandler.leftGrounded || footHandler.rightGrounded;

        if (anyFootGrounded && pelvis != null)
        {
            CalculateAverageRotation();
        }

        CheckForInputNApplyForces();
    }

    void CalculateAverageRotation()
    {
        if (footHandler.leftGrounded && footHandler.rightGrounded)
        {
            // Both feet grounded - average both target rotations
            averageRotationDirection = Quaternion.Slerp(
                footHandler.LeftTargetRotation,
                footHandler.RightTargetRotation,
                0.5f
            );
        }
        else if (footHandler.leftGrounded)
        {
            // Only left foot grounded
            averageRotationDirection = footHandler.LeftTargetRotation;
        }
        else if (footHandler.rightGrounded)
        {
            // Only right foot grounded
            averageRotationDirection = footHandler.RightTargetRotation;
        }
    }

    void CheckForInputNApplyForces()
    {
        // Only apply forces if at least one foot is grounded and pelvis exists
        bool anyFootGrounded = footHandler.leftGrounded || footHandler.rightGrounded;
        if (!anyFootGrounded || pelvis == null)
        {
            lastAppliedForce = Vector3.zero;
            currentInputDirection = Vector3.zero;
            return;
        }

        // Get input direction using configurable directions
        Vector3 inputDirection = Vector3.zero;
        Vector3 appliedForce = Vector3.zero;

        // Forward movement
        if (Input.GetKey(KeyCode.W))
        {
            Vector3 forwardForce = forwardDirection * forwardMultiplier;
            inputDirection += forwardDirection;
            appliedForce += forwardForce;
        }
        
        // Backward movement
        if (Input.GetKey(KeyCode.S))
        {
            Vector3 backwardForce = -forwardDirection * backwardMultiplier;
            inputDirection -= forwardDirection;
            appliedForce += backwardForce;
        }
        
        // Left movement
        if (Input.GetKey(KeyCode.A))
        {
            Vector3 leftForce = -rightDirection * leftMultiplier;
            inputDirection -= rightDirection;
            appliedForce += leftForce;
        }
        
        // Right movement
        if (Input.GetKey(KeyCode.D))
        {
            Vector3 rightForce = rightDirection * rightMultiplier;
            inputDirection += rightDirection;
            appliedForce += rightForce;
        }

        // Store for debugging
        currentInputDirection = inputDirection.normalized;

        // If no input, don't apply forces
        if (appliedForce == Vector3.zero)
        {
            lastAppliedForce = Vector3.zero;
            return;
        }

        // Transform the force direction by the average foot rotation
        // This makes movement relative to where the feet are pointing
        Vector3 worldForceDirection = averageRotationDirection * appliedForce;

        // Clamp force to maximum
        if (worldForceDirection.magnitude > maxForce)
        {
            worldForceDirection = worldForceDirection.normalized * maxForce;
        }

        // Apply the force to the pelvis
        pelvis.AddForce(worldForceDirection, forceMode);

        // Store for debugging
        lastAppliedForce = worldForceDirection;
    }

    // Optional: Visualize force direction in Scene view
    void OnDrawGizmos()
    {
        if (pelvis != null)
        {
            Vector3 pelvisPos = pelvis.transform.position;
            
            // Draw applied force (red)
            if (lastAppliedForce != Vector3.zero)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(pelvisPos, lastAppliedForce.normalized * 2f);
            }
            
            // Draw foot-relative forward direction (blue)
            Gizmos.color = Color.blue;
            Vector3 forwardDir = averageRotationDirection * forwardDirection;
            Gizmos.DrawRay(pelvisPos, forwardDir * 1.5f);
            
            // Draw foot-relative right direction (green)
            Gizmos.color = Color.green;
            Vector3 rightDir = averageRotationDirection * rightDirection;
            Gizmos.DrawRay(pelvisPos, rightDir * 1.2f);
            
            // Draw input direction (yellow)
            if (currentInputDirection != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Vector3 inputDir = averageRotationDirection * currentInputDirection;
                Gizmos.DrawRay(pelvisPos, inputDir * 1f);
            }
        }
    }
}