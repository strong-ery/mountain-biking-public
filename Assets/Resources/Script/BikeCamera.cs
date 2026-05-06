using UnityEngine;

public class BikeCamera : MonoBehaviour
{
    [Header("Mouse Settings")]
    public float mouseSensitivity = 3f;
    public float maxYawAngle = 100f;
    public float minPitch = -90f;
    public float maxPitch = 90f;

    [Header("Bike Reference")]
    public Rigidbody bikeRigidbody;
    public float leanSmoothSpeed = 5f;
    public float maxLeanAngle = 45f;
    public Vector3 rotationOffset = new Vector3(0f, 180f, 0f);

    [Header("Camera Smoothing")]
    public float rotationSmoothTime = 0.1f;

    [Header("Reset Settings")]
    public float resetSpeed = 3f;

    [Header("Anti-Clipping")]
    public LayerMask collisionLayers = -1;
    public float minDistanceFromWalls = 0.5f;
    public float collisionCheckRadius = 0.3f;
    public float positionReturnSpeed = 2f;

    private float yaw;
    private float pitch;
    private float currentLeanZ = 0f;

    private float yawSmooth;
    private float pitchSmooth;
    private float yawVelocity;
    private float pitchVelocity;

    // Store the intended position (without anti-clipping adjustments)
    private Vector3 intendedPosition;
    private bool hasValidIntendedPosition = false;

    void Start()
    {
        yaw = yawSmooth = 0f;
        pitch = pitchSmooth = 0f;
        intendedPosition = transform.position;
        hasValidIntendedPosition = true;
    }

    void Update()
    {
        HandleMouseLook();
        // PreventClipping();
    }

    private void HandleMouseLook()
    {
        bool isResetting = Input.GetMouseButton(1);

        if (isResetting)
        {
            if (bikeRigidbody != null)
            {
                Quaternion bikeRotation = bikeRigidbody.rotation;
                Quaternion offsetRotation = Quaternion.Euler(rotationOffset);
                Quaternion pitchOffset = Quaternion.AngleAxis(15f, Vector3.right);
                Quaternion targetResetRotation = bikeRotation * offsetRotation * pitchOffset;

                transform.rotation = Quaternion.Slerp(transform.rotation, targetResetRotation, resetSpeed * Time.deltaTime);
            }
            else
            {
                Quaternion offsetRotation = Quaternion.Euler(rotationOffset);
                Quaternion pitchOffset = Quaternion.AngleAxis(25f, Vector3.right);
                Quaternion targetResetRotation = offsetRotation * pitchOffset;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetResetRotation, resetSpeed * Time.deltaTime);
            }

            yaw = Mathf.Lerp(yaw, 0f, resetSpeed * Time.deltaTime);
            pitch = Mathf.Lerp(pitch, 25f, resetSpeed * Time.deltaTime);
        }
        else
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            yaw += mouseX;
            pitch -= mouseY;

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            yaw = Mathf.Clamp(yaw, -maxYawAngle, maxYawAngle);

            yawSmooth = Mathf.SmoothDamp(yawSmooth, yaw, ref yawVelocity, rotationSmoothTime);
            pitchSmooth = Mathf.SmoothDamp(pitchSmooth, pitch, ref pitchVelocity, rotationSmoothTime);

            if (bikeRigidbody != null)
            {
                Vector3 bikeEulers = bikeRigidbody.rotation.eulerAngles;
                Quaternion bikeBaseRotation = Quaternion.Euler(0f, bikeEulers.y, 0f);
                Quaternion offsetRotation = Quaternion.Euler(rotationOffset);
                bikeBaseRotation *= offsetRotation;

                Quaternion targetRotation = bikeBaseRotation * Quaternion.Euler(pitchSmooth, yawSmooth, 0f);

                float bikeRoll = bikeEulers.z;
                if (bikeRoll > 180f) bikeRoll -= 360f;
                bikeRoll = -bikeRoll;
                bikeRoll = Mathf.Clamp(bikeRoll, -maxLeanAngle, maxLeanAngle);
                currentLeanZ = Mathf.Lerp(currentLeanZ, bikeRoll, leanSmoothSpeed * Time.deltaTime);

                Quaternion leanRotation = Quaternion.AngleAxis(currentLeanZ, targetRotation * Vector3.forward);

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation * leanRotation, 10f * Time.deltaTime);
            }
            else
            {
                Quaternion offsetRotation = Quaternion.Euler(rotationOffset);
                Quaternion targetRotation = offsetRotation * Quaternion.Euler(pitchSmooth, yawSmooth, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }
        }

        // Update intended position BEFORE anti-clipping adjustments
        intendedPosition = transform.position;
        hasValidIntendedPosition = true;
    }

    private void PreventClipping()
    {
        if (!hasValidIntendedPosition) return;

        Vector3 bestPosition = intendedPosition;
        float minPenetration = float.MaxValue;
        bool hasCollision = false;

        // Check for collisions in multiple directions
        Vector3[] checkDirections = {
            transform.forward, -transform.forward,
            transform.right, -transform.right,
            transform.up, -transform.up
        };

        // Find the collision that requires the smallest adjustment
        foreach (Vector3 direction in checkDirections)
        {
            if (Physics.SphereCast(intendedPosition, collisionCheckRadius, direction,
                out RaycastHit hit, minDistanceFromWalls, collisionLayers))
            {
                hasCollision = true;
                float penetration = minDistanceFromWalls - hit.distance;
                
                // Only update if this collision requires a smaller adjustment
                if (penetration < minPenetration)
                {
                    minPenetration = penetration;
                    bestPosition = intendedPosition + (-direction * penetration);
                }
            }
        }

        // Apply the result
        if (!hasCollision)
        {
            // Smoothly return to intended position when no collision
            transform.position = Vector3.Lerp(transform.position, intendedPosition, positionReturnSpeed * Time.deltaTime);
        }
        else
        {
            // Use the best position that requires minimal adjustment
            transform.position = bestPosition;
        }
    }
}