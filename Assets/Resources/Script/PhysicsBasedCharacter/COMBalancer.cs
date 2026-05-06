using UnityEngine;

public class COMBalancer : MonoBehaviour
{
    [Header("COM Settings")]
    public Transform head;
    public float comHeight = 1.2f; // Height above head
    public float pullForce = 1000f;
    public Vector3 offset = Vector3.zero; // Additional offset for COM target

    [Header("Movement Settings")]
    public Animator animator;
    public float movingStrength = 0.5f; // Strength multiplier when moving (0.0 to 1.0)

    [Header("Foot Grounding")]
    public Transform leftFoot;
    public Transform rightFoot;
    public float footPullForce = 500f; // Force to pull feet down
    public float movingFootStrength = 0.5f; // Foot strength multiplier when moving (0.0 to 1.0)
    public float groundCheckDistance = 0.1f; // Distance to check for ground
    public LayerMask groundLayer = 1; // What counts as ground

    private Transform comTarget;
    private bool isMoving = false;

    void Start()
    {
        // Create invisible COM target above character
        comTarget = new GameObject("COM_Target").transform;
    }

    void FixedUpdate()
    {
        // Get moving state from animator
        if (animator != null)
        {
            isMoving = animator.GetBool("Moving");
        }

        // Calculate force multiplier based on movement state
        float forceMultiplier = isMoving ? movingStrength : 1.0f;

        // Position COM target above head with rotation-relative offset
        Vector3 rotatedOffset = head.rotation * offset;
        comTarget.position = head.position + Vector3.up * comHeight + rotatedOffset;

        // Pull head upward toward COM target (like a puppet string)
        Vector3 pullDirection = (comTarget.position - head.position).normalized;
        head.GetComponent<Rigidbody>().AddForce(pullDirection * pullForce * forceMultiplier);

        // Apply downward force to feet
        ApplyFootGrounding();
    }

    void ApplyFootGrounding()
    {
        // Calculate foot force multiplier based on movement state
        float footForceMultiplier = isMoving ? movingFootStrength : 1.0f;

        // Pull left foot down
        if (leftFoot != null)
        {
            Rigidbody leftFootRb = leftFoot.GetComponent<Rigidbody>();
            if (leftFootRb != null)
            {
                // Check if foot is close to ground
                bool nearGround = Physics.Raycast(leftFoot.position, Vector3.down, groundCheckDistance, groundLayer);
                
                // Apply stronger force if not near ground, lighter force if near ground
                float groundMultiplier = nearGround ? 0.75f : 1.0f;
                leftFootRb.AddForce(Vector3.down * footPullForce * footForceMultiplier * groundMultiplier);
            }
        }

        // Pull right foot down
        if (rightFoot != null)
        {
            Rigidbody rightFootRb = rightFoot.GetComponent<Rigidbody>();
            if (rightFootRb != null)
            {
                // Check if foot is close to ground
                bool nearGround = Physics.Raycast(rightFoot.position, Vector3.down, groundCheckDistance, groundLayer);
                
                // Apply stronger force if not near ground, lighter force if near ground
                float groundMultiplier = nearGround ? 0.75f : 1.0f;
                rightFootRb.AddForce(Vector3.down * footPullForce * footForceMultiplier * groundMultiplier);
            }
        }
    }

    void OnDrawGizmos()
    {
        // Visualize COM target in editor
        if (comTarget != null)
        {
            // Change color based on movement state
            Gizmos.color = isMoving ? Color.yellow : Color.red;
            Gizmos.DrawWireSphere(comTarget.position, 0.1f);
        }

        // Visualize ground check rays
        if (leftFoot != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(leftFoot.position, Vector3.down * groundCheckDistance);
        }
        
        if (rightFoot != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(rightFoot.position, Vector3.down * groundCheckDistance);
        }
    }
}