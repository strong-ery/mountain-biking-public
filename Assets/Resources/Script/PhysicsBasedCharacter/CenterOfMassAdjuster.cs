using UnityEngine;

public class CenterOfMassAdjuster : MonoBehaviour
{
    public DualGroundFootHandler footHandler;
    public Rigidbody pelvis;
    
    [Header("Center of Mass Settings")]
    public float adjustmentStrength = 1f; // How much to adjust the center of mass
    public float smoothSpeed = 5f; // How fast to interpolate the adjustment
    public Vector3 baseCenterOfMass = Vector3.zero; // Default center of mass offset
    
    [Header("Debug Info")]
    public Quaternion averageRotationDirection; // For debugging
    public Vector3 currentAdjustment; // Current adjustment being applied
    
    private Vector3 targetAdjustment;
    
    void Update()
    {
        // Check if at least one foot is grounded
        bool anyFootGrounded = footHandler.leftGrounded || footHandler.rightGrounded;
        
        if (anyFootGrounded && pelvis != null)
        {
            CalculateAverageRotation();
            AdjustCenterOfMass();
        }
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
    
    void AdjustCenterOfMass()
    {
        // Get the ground normal from the average rotation
        Vector3 groundUp = averageRotationDirection * Vector3.up;
        
        // Calculate the tilt direction (inverse of ground normal for leaning uphill)
        Vector3 tiltDirection = Vector3.up - groundUp;
        
        // Convert to local space of the pelvis
        Vector3 localTiltDirection = pelvis.transform.InverseTransformDirection(tiltDirection);
        
        // Calculate target adjustment (inverse direction to lean uphill)
        targetAdjustment = localTiltDirection * adjustmentStrength;
        
        // Smoothly interpolate to the target adjustment
        currentAdjustment = Vector3.Lerp(
            currentAdjustment, 
            targetAdjustment, 
            smoothSpeed * Time.deltaTime
        );
        
        // Apply the adjustment to the center of mass
        pelvis.centerOfMass = baseCenterOfMass + currentAdjustment;
    }
    
    void OnDrawGizmos()
    {
        if (pelvis != null && Application.isPlaying)
        {
            // Draw the current center of mass
            Gizmos.color = Color.blue;
            Vector3 worldCenterOfMass = pelvis.transform.TransformPoint(pelvis.centerOfMass);
            Gizmos.DrawWireSphere(worldCenterOfMass, 0.1f);
            
            // Draw line from pelvis to center of mass
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pelvis.transform.position, worldCenterOfMass);
            
            // Draw the ground normal direction
            if (footHandler != null && (footHandler.leftGrounded || footHandler.rightGrounded))
            {
                Vector3 groundUp = averageRotationDirection * Vector3.up;
                Gizmos.color = Color.green;
                Gizmos.DrawRay(pelvis.transform.position, groundUp * 2f);
                
                // Draw the tilt direction
                Vector3 tiltDirection = Vector3.up - groundUp;
                Gizmos.color = Color.red;
                Gizmos.DrawRay(pelvis.transform.position, tiltDirection * 2f);
            }
        }
    }
}