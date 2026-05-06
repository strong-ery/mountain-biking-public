using UnityEngine;

public class StableThirdPersonCamera : MonoBehaviour
{
    [Header("Target References")]
    [SerializeField] private Transform target;
    [SerializeField] private HeadAnimatedRigTwistCamera headController; // Reference to your existing script
    [SerializeField] private CameraXRotator xRotator; // Reference to your X rotator script
    
    [Header("Camera Mode")]
    [SerializeField] private bool firstPersonMode = false;
    
    [Header("First Person Settings")]
    [SerializeField] private Vector3 firstPersonOffset = new Vector3(0f, 1.6f, 0.2f); // Head position offset
    [SerializeField] private float firstPersonSensitivity = 2f;
    [SerializeField] private float firstPersonSmoothTime = 0.02f; // Very responsive for FP
    [SerializeField] private Vector2 firstPersonClampAngles = new Vector2(-80f, 80f); // X rotation limits
    [SerializeField] private bool firstPersonInvertY = false;
    [SerializeField] private float firstPersonFieldOfView = 90f; // FOV for first person
    [SerializeField] private bool lockCursorInFirstPerson = true;
    
    [Header("Camera Settings")]
    [SerializeField] private Vector3 offset = new Vector3(1.5f, 2f, -3f); // Over-the-shoulder offset
    [SerializeField] private Vector3 rotationOffset = new Vector3(0f, 0f, 0f); // Additional rotation offset (X, Y, Z)
    [SerializeField] private bool invertY = false; // Invert Y-axis input
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float maxFollowDistance = 10f;
    [SerializeField] private float thirdPersonFieldOfView = 75f; // FOV for third person
    
    [Header("Noise Reduction")]
    [SerializeField] private float positionSmoothTime = 0.1f;
    [SerializeField] private float rotationSmoothTime = 0.05f;
    [SerializeField] private float velocityThreshold = 0.5f; // Ignore small movements
    
    [Header("Rotation Stability")]
    [SerializeField] private float rotationTolerance = 0.1f; // Degrees - snap when extremely close
    [SerializeField] private float smoothTransitionRange = 3f; // Degrees - slow down smoothing in this range
    [SerializeField] private bool useLookAtRotation = false; // Toggle between input-based and look-at rotation
    [SerializeField] private float lookAtBlendSpeed = 2f; // How fast to blend to look-at when enabled
    
    [Header("Dynamic Adjustments")]
    [SerializeField] private float speedBasedDistance = 1f; // Pull back when moving fast
    [SerializeField] private float collisionAvoidanceRadius = 0.5f;
    [SerializeField] private LayerMask collisionLayers = -1;
    
    // Private variables
    private Vector3 currentVelocity;
    private Vector3 smoothPosition;
    private Vector3 targetPosition;
    private float currentYRotation;
    private float currentXRotation;
    private Vector3 lastTargetPosition;
    private float targetSpeed;
    
    // Smoothing variables
    private Vector3 positionVelocity;
    private float yRotationVelocity;
    private float xRotationVelocity;
    
    // First person variables
    private float firstPersonYaw = 0f;
    private float firstPersonPitch = 0f;
    private Camera cameraComponent;
    private bool wasFirstPersonLastFrame = false;
    private float originalFOV;
    
    void Start()
    {
        if (target == null)
        {
            Debug.LogError("StableThirdPersonCamera: Target not assigned!");
            return;
        }
        
        // Get camera component
        cameraComponent = GetComponent<Camera>();
        if (cameraComponent != null)
        {
            originalFOV = cameraComponent.fieldOfView;
        }
        
        // Initialize positions
        lastTargetPosition = target.position;
        smoothPosition = target.position + offset;
        transform.position = smoothPosition;
        
        // Initialize rotations from external controllers if available
        if (headController != null)
        {
            currentYRotation = headController.targetYRotation; // This needs to be public
            firstPersonYaw = headController.targetYRotation;
        }
        
        if (xRotator != null)
        {
            currentXRotation = xRotator.currentXRotation; // This needs to be public
            firstPersonPitch = xRotator.currentXRotation;
        }
        
        // Handle cursor state for first person
        HandleCursorState();
    }
    
    void LateUpdate()
    {
        if (target == null) return;
        
        // Check if we switched modes
        if (firstPersonMode != wasFirstPersonLastFrame)
        {
            OnCameraModeChanged();
        }
        
        if (firstPersonMode)
        {
            UpdateFirstPersonCamera();
        }
        else
        {
            UpdateThirdPersonCamera();
        }
        
        wasFirstPersonLastFrame = firstPersonMode;
    }
    
    void OnCameraModeChanged()
    {
        HandleCursorState();
        
        if (cameraComponent != null)
        {
            if (firstPersonMode)
            {
                cameraComponent.fieldOfView = firstPersonFieldOfView;
                
                // Initialize first person rotation from current camera rotation
                Vector3 currentEuler = transform.eulerAngles;
                firstPersonYaw = currentEuler.y;
                firstPersonPitch = currentEuler.x;
                
                // Normalize pitch to -180 to 180 range
                if (firstPersonPitch > 180f)
                    firstPersonPitch -= 360f;
            }
            else
            {
                cameraComponent.fieldOfView = thirdPersonFieldOfView;
            }
        }
    }
    
    void HandleCursorState()
    {
        if (firstPersonMode && lockCursorInFirstPerson)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (!firstPersonMode)
        {
            // Only unlock if we're switching from first person
            if (lockCursorInFirstPerson && wasFirstPersonLastFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
    
    void UpdateFirstPersonCamera()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * firstPersonSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * firstPersonSensitivity;
        
        // Apply Y inversion
        if (firstPersonInvertY)
            mouseY = -mouseY;
        
        // Update rotation
        firstPersonYaw += mouseX;
        firstPersonPitch -= mouseY;
        
        // Clamp pitch
        firstPersonPitch = Mathf.Clamp(firstPersonPitch, firstPersonClampAngles.x, firstPersonClampAngles.y);
        
        // Smooth rotation for natural feel
        Quaternion targetRotation = Quaternion.Euler(firstPersonPitch, firstPersonYaw, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime / firstPersonSmoothTime);
        
        // Position camera at head level
        Vector3 headPosition = target.position + target.TransformDirection(firstPersonOffset);
        transform.position = Vector3.Lerp(transform.position, headPosition, Time.deltaTime / firstPersonSmoothTime);
        
        // Update external controllers if they exist
        UpdateExternalControllers();
    }
    
    void UpdateExternalControllers()
    {
        // Note: This method is no longer needed since we're reading from the controllers
        // rather than trying to control them from the first person camera
        // The external controllers remain the source of truth for input
    }
    
    void UpdateThirdPersonCamera()
    {
        UpdateTargetTracking();
        CalculateDesiredPosition();
        ApplyCollisionAvoidance();
        SmoothCameraMovement();
        UpdateCameraRotation();
    }
    
    void UpdateTargetTracking()
    {
        // Calculate target velocity for noise filtering
        Vector3 targetVelocity = (target.position - lastTargetPosition) / Time.deltaTime;
        targetSpeed = targetVelocity.magnitude;
        
        // Filter out small movements (noise reduction)
        if (targetSpeed < velocityThreshold)
        {
            targetVelocity = Vector3.zero;
        }
        
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.deltaTime * followSpeed);
        lastTargetPosition = target.position;
    }
    
    void CalculateDesiredPosition()
    {
        // Get rotation values from external controllers
        float yRotation = currentYRotation;
        float xRotation = currentXRotation;
        
        if (headController != null)
        {
            yRotation = headController.targetYRotation;
        }
        
        if (xRotator != null)
        {
            xRotation = xRotator.currentXRotation;
            
            // Apply Y inversion to X rotation if enabled
            if (invertY)
            {
                xRotation = -xRotation;
            }
        }
        
        // Apply rotation offset to the input rotations (this affects positioning)
        yRotation += rotationOffset.y;
        xRotation += rotationOffset.x;
        
        // Create rotation based on input + offset
        Quaternion rotation = Quaternion.Euler(xRotation, yRotation, 0f);
        
        // Calculate dynamic offset based on speed
        Vector3 dynamicOffset = offset;
        float speedMultiplier = 1f + (targetSpeed * speedBasedDistance * 0.1f);
        dynamicOffset.z *= speedMultiplier; // Pull back when moving fast
        
        // Apply rotation to offset
        Vector3 rotatedOffset = rotation * dynamicOffset;
        
        // Calculate target position
        targetPosition = target.position + rotatedOffset;
        
        // Ensure we don't get too far from target
        Vector3 directionToTarget = target.position - targetPosition;
        if (directionToTarget.magnitude > maxFollowDistance)
        {
            targetPosition = target.position - directionToTarget.normalized * maxFollowDistance;
        }
    }
    
    void ApplyCollisionAvoidance()
    {
        // Check for obstacles between camera and target
        Vector3 directionToTarget = target.position - targetPosition;
        float distanceToTarget = directionToTarget.magnitude;
        
        RaycastHit hit;
        if (Physics.SphereCast(targetPosition, collisionAvoidanceRadius, directionToTarget.normalized, 
            out hit, distanceToTarget, collisionLayers))
        {
            // Move camera closer to avoid collision
            float safeDistance = hit.distance - collisionAvoidanceRadius - 0.1f;
            targetPosition = target.position - directionToTarget.normalized * Mathf.Max(safeDistance, 1f);
        }
    }
    
    void SmoothCameraMovement()
    {
        // Smooth position interpolation
        smoothPosition = Vector3.SmoothDamp(smoothPosition, targetPosition, ref positionVelocity, positionSmoothTime);
        transform.position = smoothPosition;
    }
    
    void UpdateCameraRotation()
    {
        if (useLookAtRotation)
        {
            // Use look-at rotation system
            UpdateLookAtRotation();
        }
        else
        {
            // Use input-based rotation system (stable)
            UpdateInputBasedRotation();
        }
    }
    
    void UpdateInputBasedRotation()
    {
        // Get target rotations from controllers
        float targetY = currentYRotation;
        float targetX = currentXRotation;
        
        if (headController != null)
        {
            targetY = headController.targetYRotation;
        }
        
        if (xRotator != null)
        {
            targetX = xRotator.currentXRotation;
            
            // Apply Y inversion to X rotation if enabled
            if (invertY)
            {
                targetX = -targetX;
            }
        }
        
        // Apply rotation offset
        targetX += rotationOffset.x;
        targetY += rotationOffset.y;
        
        // Calculate differences
        float yDifference = Mathf.DeltaAngle(currentYRotation, targetY);
        float xDifference = Mathf.DeltaAngle(currentXRotation, targetX);
        
        // Calculate dynamic smooth time based on distance from target
        float ySmoothTime = CalculateDynamicSmoothTime(Mathf.Abs(yDifference));
        float xSmoothTime = CalculateDynamicSmoothTime(Mathf.Abs(xDifference));
        
        // Only snap if extremely close (prevents micro-jitters but allows smooth approach)
        if (Mathf.Abs(yDifference) <= rotationTolerance)
        {
            currentYRotation = targetY;
            yRotationVelocity = 0f;
        }
        else
        {
            currentYRotation = Mathf.SmoothDampAngle(currentYRotation, targetY, ref yRotationVelocity, ySmoothTime);
        }
        
        if (Mathf.Abs(xDifference) <= rotationTolerance)
        {
            currentXRotation = targetX;
            xRotationVelocity = 0f;
        }
        else
        {
            currentXRotation = Mathf.SmoothDampAngle(currentXRotation, targetX, ref xRotationVelocity, xSmoothTime);
        }
        
        // Apply final rotation with Z offset (roll)
        transform.rotation = Quaternion.Euler(currentXRotation, currentYRotation, rotationOffset.z);
    }
    
    float CalculateDynamicSmoothTime(float angleDifference)
    {
        // Base smooth time
        float baseSmoothTime = rotationSmoothTime;
        
        // If we're within the smooth transition range, gradually increase smooth time
        if (angleDifference <= smoothTransitionRange)
        {
            // Create a smooth curve that increases smooth time as we get closer
            float normalizedDistance = angleDifference / smoothTransitionRange; // 0 = very close, 1 = at edge
            float smoothMultiplier = 1f + (1f - normalizedDistance) * 2f; // Increase smooth time when closer
            baseSmoothTime *= smoothMultiplier;
        }
        
        return baseSmoothTime;
    }
    
    void UpdateLookAtRotation()
    {
        // Look at target with slight offset for better framing
        Vector3 lookDirection = (target.position + Vector3.up * 1f) - transform.position;
        if (lookDirection != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(lookDirection);
            
            // Check if we're close enough to the target rotation
            float angleDifference = Quaternion.Angle(transform.rotation, lookRotation);
            
            if (angleDifference > rotationTolerance)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * lookAtBlendSpeed);
            }
            else
            {
                transform.rotation = lookRotation; // Snap to prevent micro-jitters
            }
        }
    }
    
    // Public methods for external access
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            lastTargetPosition = target.position;
        }
    }
    
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
    
    public void SetRotationOffset(Vector3 newRotationOffset)
    {
        rotationOffset = newRotationOffset;
    }
    
    public void ToggleLookAtMode(bool enable)
    {
        useLookAtRotation = enable;
    }
    
    public void ToggleFirstPersonMode(bool enable)
    {
        firstPersonMode = enable;
    }
    
    public bool IsFirstPersonMode()
    {
        return firstPersonMode;
    }
    
    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (target == null) return;
        
        if (!firstPersonMode)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPosition, 0.3f);
            
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, target.position);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(target.position, collisionAvoidanceRadius);
        }
        else
        {
            // Show first person head position
            Vector3 headPos = target.position + target.TransformDirection(firstPersonOffset);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(headPos, 0.2f);
        }
    }
}