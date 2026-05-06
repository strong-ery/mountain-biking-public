using UnityEngine;

public class DualGroundFootHandler : MonoBehaviour
{
    [Header("Foot References")]
    public Transform pelvis;
    public GameObject leftTargetFoot;
    public GameObject rightTargetFoot;
    public Transform leftRaycastOrigin;
    public Transform rightRaycastOrigin;
    
    [Header("Ground Detection")]
    public LayerMask groundLayer;
    public float castDistance = 2f;
    
    [Header("Left Foot Settings")]
    public Vector3 leftPositionOffset = Vector3.zero;
    public Vector3 leftRotationOffset = Vector3.zero;
    
    [Header("Right Foot Settings")]
    public Vector3 rightPositionOffset = Vector3.zero;
    public Vector3 rightRotationOffset = Vector3.zero;
    
    [Header("Smoothing")]
    [Range(0f, 1f)]
    public float rotationBlendWeight = 1f;
    public float rotationSpeed = 5f;
    
    [Header("Foot Status")]
    public bool leftGrounded;
    public bool rightGrounded;
    
    [Header("Debug Info - Read Only")]
    public Quaternion leftCurrentTargetRotation;
    public Quaternion rightCurrentTargetRotation;
    public Quaternion leftCurrentBaseRotation;
    public Quaternion rightCurrentBaseRotation;
    public Vector3 leftCurrentGroundNormal;
    public Vector3 rightCurrentGroundNormal;
    
    private Quaternion leftTargetRotation;
    private Quaternion rightTargetRotation;
    private Quaternion leftBaseRotation;
    private Quaternion rightBaseRotation;
    private Quaternion leftOriginalRotation;
    private Quaternion rightOriginalRotation;
    private Animator characterAnimator;
    
    // Store initial rotations to blend back to
    private bool hasStoredInitialRotations = false;
    
    // Public properties to access target rotations (for other scripts)
    public Quaternion LeftTargetRotation => leftTargetRotation;
    public Quaternion RightTargetRotation => rightTargetRotation;
    
    void Start()
    {
        characterAnimator = GetComponent<Animator>();
        
        if (characterAnimator == null)
        {
            Debug.LogWarning($"No Animator found on {gameObject.name}. OnAnimatorIK won't be called.");
        }
        
        // Check for required references
        if (leftRaycastOrigin == null)
            Debug.LogWarning($"Left Raycast Origin not assigned on {gameObject.name}");
        if (rightRaycastOrigin == null)
            Debug.LogWarning($"Right Raycast Origin not assigned on {gameObject.name}");
    }

    void LateUpdate()
    {
        // Store initial rotations on first frame after animations have been applied
        if (!hasStoredInitialRotations)
        {
            if (leftTargetFoot != null)
                leftOriginalRotation = leftTargetFoot.transform.rotation;
            if (rightTargetFoot != null)
                rightOriginalRotation = rightTargetFoot.transform.rotation;
            hasStoredInitialRotations = true;
        }
        
        ProcessFoot(
            leftRaycastOrigin, leftPositionOffset, leftRotationOffset,
            out leftGrounded, out leftTargetRotation, out leftBaseRotation, 
            out leftCurrentGroundNormal
        );
        
        ProcessFoot(
            rightRaycastOrigin, rightPositionOffset, rightRotationOffset,
            out rightGrounded, out rightTargetRotation, out rightBaseRotation, 
            out rightCurrentGroundNormal
        );
        
        // Apply rotations directly, not with slerp
        ApplyFootRotation(leftTargetFoot, leftGrounded, leftTargetRotation, leftOriginalRotation);
        ApplyFootRotation(rightTargetFoot, rightGrounded, rightTargetRotation, rightOriginalRotation);
        
        // Update debug info
        leftCurrentTargetRotation = leftTargetRotation;
        rightCurrentTargetRotation = rightTargetRotation;
        leftCurrentBaseRotation = leftBaseRotation;
        rightCurrentBaseRotation = rightBaseRotation;
    }
    
    private void ApplyFootRotation(GameObject footObject, bool grounded, Quaternion targetRot, Quaternion originalRot)
    {
        if (footObject == null) return;
        
        if (grounded)
        {
            // Method 1: Direct assignment (like FastIK)
            if (rotationSpeed <= 0f || rotationBlendWeight >= 1f)
            {
                footObject.transform.rotation = targetRot;
            }
            else
            {
                // Method 2: Smooth towards target but don't fight with animation
                // Only smooth the first frame, then commit to the rotation
                footObject.transform.rotation = Quaternion.Slerp(
                    footObject.transform.rotation, 
                    targetRot, 
                    rotationSpeed * Time.deltaTime
                );
            }
        }
        else
        {
            // Not grounded, restore original rotation
            if (rotationSpeed <= 0f)
            {
                footObject.transform.rotation = originalRot;
            }
            else
            {
                footObject.transform.rotation = Quaternion.Slerp(
                    footObject.transform.rotation, 
                    originalRot, 
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }
    
    private void ProcessFoot(Transform raycastOrigin, Vector3 positionOffset, Vector3 rotationOffset,
                           out bool grounded, out Quaternion targetRotation, out Quaternion baseRotation,
                           out Vector3 groundNormal)
    {
        grounded = false;
        targetRotation = Quaternion.identity;
        baseRotation = Quaternion.identity;
        groundNormal = Vector3.up;
        
        if (raycastOrigin == null) return;
        
        // Calculate the raycast origin with position offset
        Vector3 rayOrigin = raycastOrigin.position + raycastOrigin.TransformDirection(positionOffset);
        
        // Create the ray from the offset position
        Ray ray = new Ray(rayOrigin, Vector3.down);
        RaycastHit hit;
        
        // Cast the ray downward to check for ground
        if (Physics.Raycast(ray, out hit, castDistance, groundLayer))
        {
            grounded = true;
            groundNormal = hit.normal;
            
            // Get the pelvis forward direction (projected onto the ground plane)
            Vector3 pelvisForward = Vector3.ProjectOnPlane(pelvis.forward, groundNormal).normalized;
            
            // Calculate the right vector perpendicular to both forward and normal
            Vector3 rightVector = Vector3.Cross(groundNormal, pelvisForward).normalized;
            
            // Recalculate forward to ensure orthogonality
            Vector3 forwardVector = Vector3.Cross(rightVector, groundNormal).normalized;
            
            // Create rotation where:
            // - Up vector aligns with ground normal
            // - Forward vector maintains pelvis orientation but follows ground slope
            baseRotation = Quaternion.LookRotation(forwardVector, groundNormal);
            
            // Apply the euler rotation offset
            targetRotation = baseRotation * Quaternion.Euler(rotationOffset);
        }
    }

    public Vector3 GetLeftGroundNormal()
    {
        return leftGrounded ? leftCurrentGroundNormal : Vector3.up;
    }

    public Vector3 GetRightGroundNormal()
    {
        return rightGrounded ? rightCurrentGroundNormal : Vector3.up;
    }

    public Vector3 GetAverageGroundNormal()
    {
        if (leftGrounded && rightGrounded)
            return (leftCurrentGroundNormal + rightCurrentGroundNormal).normalized;
        else if (leftGrounded)
            return leftCurrentGroundNormal;
        else if (rightGrounded)
            return rightCurrentGroundNormal;
        else
            return Vector3.up;
    }

    void OnDrawGizmos()
    {
        // Draw left foot gizmos
        DrawFootGizmos(leftRaycastOrigin, leftPositionOffset, leftGrounded, 
                      leftTargetFoot, leftBaseRotation, leftTargetRotation, leftCurrentGroundNormal);
        
        // Draw right foot gizmos
        DrawFootGizmos(rightRaycastOrigin, rightPositionOffset, rightGrounded,
                      rightTargetFoot, rightBaseRotation, rightTargetRotation, rightCurrentGroundNormal);
    }
    
    private void DrawFootGizmos(Transform raycastOrigin, Vector3 positionOffset, bool grounded,
                               GameObject targetFoot, Quaternion baseRotation, Quaternion targetRotation,
                               Vector3 groundNormal)
    {
        if (raycastOrigin == null) return;
        
        // Calculate the raycast origin with position offset
        Vector3 rayStart = raycastOrigin.position + raycastOrigin.TransformDirection(positionOffset);
        Vector3 rayEnd = rayStart + Vector3.down * castDistance;
        
        // Draw the raycast line
        Gizmos.color = grounded ? Color.green : Color.red;
        Gizmos.DrawLine(rayStart, rayEnd);
        
        // Draw a small sphere at the start point (offset position)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(rayStart, 0.05f);
        
        // Optional: Draw a line from original transform position to offset position for visualization
        if (positionOffset != Vector3.zero)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(raycastOrigin.position, rayStart);
        }
        
        // Draw rotation visualization if grounded
        if (grounded && targetFoot != null)
        {
            Vector3 footPos = targetFoot.transform.position;
            
            // Draw the base rotation (without offset) in cyan
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(footPos, baseRotation * Vector3.forward * 0.2f);
            
            // Draw the target rotation (with offset) in magenta
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(footPos, targetRotation * Vector3.forward * 0.2f);
            
            // Draw ground normal in white
            Gizmos.color = Color.white;
            Gizmos.DrawRay(footPos, groundNormal * 0.3f);
        }
    }
}