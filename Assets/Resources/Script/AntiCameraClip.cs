using UnityEngine;

public class AntiCameraClip : MonoBehaviour
{
    [Header("Anti-Clipping")]
    public LayerMask collisionLayers = -1;
    public float minDistanceFromWalls = 0.5f;
    public float collisionCheckRadius = 0.3f;
    public float positionReturnSpeed = 2f; // How fast camera returns to original position

    // Store the intended position (without anti-clipping adjustments)
    private Vector3 intendedPosition;
    private bool hasValidIntendedPosition = false;

    void Start()
    {
        intendedPosition = transform.position;
        hasValidIntendedPosition = true;
    }

    void Update()
    {
        // Update intended position - this should be set by your camera controller
        // For this standalone script, we assume the intended position doesn't change
        // unless you're using this with another camera script that moves the camera
        if (!hasValidIntendedPosition)
        {
            intendedPosition = transform.position;
            hasValidIntendedPosition = true;
        }

        PreventClipping();
    }

    // Public method to set the intended position from external scripts
    public void SetIntendedPosition(Vector3 position)
    {
        intendedPosition = position;
        hasValidIntendedPosition = true;
    }

    private void PreventClipping()
    {
        if (!hasValidIntendedPosition) return;

        Vector3 finalPosition = intendedPosition;
        bool hasCollision = false;

        // Check for collisions in multiple directions
        Vector3[] checkDirections = {
            transform.forward, -transform.forward,
            transform.right, -transform.right,
            transform.up, -transform.up
        };

        foreach (Vector3 direction in checkDirections)
        {
            if (Physics.SphereCast(intendedPosition, collisionCheckRadius, direction,
                out RaycastHit hit, minDistanceFromWalls, collisionLayers))
            {
                hasCollision = true;
                Vector3 pushDirection = -direction;
                float pushDistance = minDistanceFromWalls - hit.distance;
                finalPosition += pushDirection * pushDistance;
            }
        }

        // If there's no collision, smoothly return to intended position
        if (!hasCollision)
        {
            transform.position = Vector3.Lerp(transform.position, intendedPosition, positionReturnSpeed * Time.deltaTime);
        }
        else
        {
            // If there is collision, immediately move to safe position
            transform.position = finalPosition;
        }
    }
}