using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class BeanController : MonoBehaviour
{
    [Header("PlayerConfig")]
    public PlayerConfigData pcd;
    public RagdollRigManager rrm;
    public GameObject disableObject;

    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float sprintMultiplier = 1.5f;
    public float maxSpeed = 10f;
    [SerializeField] float airControlMultiplier = 0.5f;
    [SerializeField] float stopFriction = 15f;
    public float jumpForce = 7f;
    public float gravityMultiplier = 2f;
    
    [Header("Slope Settings")]
    [SerializeField] float maxSlopeAngle = 45f;
    [SerializeField] float slopeForceMultiplier = 1.5f;
    [SerializeField] float downhillSpeedMultiplier = 1.2f;
    [SerializeField] float slopeSlipThreshold = 60f;
    [SerializeField] float antiSlipForce = 10f;
    [SerializeField] bool enableSlopeSliding = true;
    [SerializeField] float slideControlFactor = 0.3f;

    [Header("Improved Ground Check")]
    public float groundCheckRadius = 0.4f;
    public float groundCheckOffset = 0.1f;
    public LayerMask groundMask;
    public bool isGrounded;
    private bool contactGrounded = false;
    private Vector3 contactNormal = Vector3.up;
    private Vector3 currentGroundNormal = Vector3.up;
    private float currentSlopeAngle = 0f;
    private bool isOnSlope = false;
    private bool isSlidingDownSlope = false;
    bool wasGrounded;
    float previousVerticalVelocity;

    [Header("Camera Settings")]
    public Transform cameraHolder;
    public Transform bobHolder;
    public float mouseSensitivity = 100f;
    public float maxLookAngle = 80f;

    [Header("Ragdoll Settings")]
    public float ragdollTorque = 5f;
    public float maxAngularSpeed = 10f;
    public float collisionRagdollThreshold = 10f;
    [HideInInspector] public bool isRagdoll = false;

    [Header("Advanced Ragdoll Detection")]
    [SerializeField] private float ragdollSpeedChangeThreshold = 20f;
    [SerializeField] private float ragdollDirectionChangeThreshold = 15f;
    [SerializeField] private float ragdollCooldown = 1f;
    [SerializeField] private float ragdollAngularThreshold = 10f;
    [SerializeField] private float ragdollJerkThreshold = 50f;
    [SerializeField] private float ragdollForceThreshold = 100f;
    [SerializeField] private float ragdollVarianceThreshold = 25f;

    [Header("Bounce Settings")]
    public float bounceStrength = 0.5f;
    public float bounceRandomness = 0.2f;

    [Header("Camera Bobbing & Look")]
    public float walkBobFrequency = 15f;
    public float runBobFrequency = 20f;
    public float bobAmplitude = 0.15f;
    [Space]
    [Header("Angular View Bobbing")]
    public float angularBobAmplitude = 2f;
    public float angularBobFrequency = 1.2f;
    public bool enableAngularBobbing = true;

    [Header("Leg IK System")]
    [Space]
    [SerializeField] Transform leftFootIKTarget;
    [SerializeField] Transform rightFootIKTarget;
    [Space]
    [Header("IK Settings")]
    [SerializeField] float stepDistance = 1.5f;
    [SerializeField] float stepHeight = 0.8f;
    [SerializeField] float stepSpeed = 8f;
    [SerializeField] float stepSpeedMultiplier = 1.5f; // How much faster steps get at max speed
    [SerializeField] float minStepSpeedRatio = 0.6f; // Minimum step speed as ratio of base speed
    [SerializeField] float footGroundOffset = 0.1f;
    [SerializeField] float maxStepDistance = 2.5f;
    [SerializeField] float footRaycastDistance = 2f;
    [Space]
    [Header("Surface Adaptation")]
    [SerializeField] bool adaptToSurfaceNormal = true;
    [SerializeField] float surfaceAdaptationSpeed = 5f;
    [SerializeField] float maxSurfaceAngle = 60f;
    [Space]
    [Header("Step Timing")]
    [SerializeField] float minStepInterval = 0.3f;
    [SerializeField] float maxStepInterval = 0.8f;
    [SerializeField] bool alternateSteps = true;
    [Space]
    [Header("Advanced Stepping")]
    [SerializeField] float rotationStepThreshold = 45f; // Degrees of rotation to trigger step
    [SerializeField] float velocityPrediction = 0.4f; // How far ahead to place feet based on velocity
    [SerializeField] float minVelocityForPrediction = 2f; // Minimum speed for velocity prediction
    [SerializeField] float stopPlantingDelay = 0.3f; // Time after stopping before planting feet
    [SerializeField] float stopPlantingSpeed = 0.5f; // Speed threshold for "stopped"

    [Header("Footstep Audio")]
    public AudioSource footstepSource;
    public AudioClip[] footstepSounds;

    private Rigidbody rb;
    private float xRotation = 0f;
    private float verticalVelocity = 0f;
    private Vector3 bobStartLocalPos;
    private float bobTimer;
    private Vector3 lastVelocity;
    private float angularBobTimer;

    // Movement state tracking
    private Vector3 lastPosition;
    private bool wasMoving = false;
    private bool isSprinting = false;
    private Vector3 currentHorizontalVelocity;

    // Rotation tracking for stepping
    private float lastYRotation;
    private float rotationDelta;
    
    // Stop planting system
    private float stoppedTime = 0f;
    private bool hasPlantedAfterStop = false;
    private bool wasMovingLastFrame = false;

    // IK System Variables
    private bool leftFootStepping = false;
    private bool rightFootStepping = false;
    private Vector3 leftFootTargetPos;
    private Vector3 rightFootTargetPos;
    private Vector3 leftFootStartPos;
    private Vector3 rightFootStartPos;
    private Quaternion leftFootTargetRot;
    private Quaternion rightFootTargetRot;
    private Quaternion leftFootStartRot;
    private Quaternion rightFootStartRot;
    private float leftStepTimer = 0f;
    private float rightStepTimer = 0f;
    private float lastStepTime = 0f;
    private bool isLeftFootTurn = true;
    private Vector3 leftFootDefaultPos;
    private Vector3 rightFootDefaultPos;

    // Advanced ragdoll detection variables
    private float ragdollTimer = 0f;
    private float sustainedChangeTime = 0f;
    private Vector3 lastAngularVelocity;
    private Vector3 lastAcceleration;
    private float lastCollisionForce;
    private Vector3[] velocityHistory = new Vector3[5];
    private int velocityHistoryIndex = 0;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        bobStartLocalPos = bobHolder.localPosition;
        lastVelocity = rb.linearVelocity;
        lastPosition = transform.position;
        lastYRotation = transform.eulerAngles.y;

        InitializeLegIK();
    }

    void InitializeLegIK()
    {
        // Initialize default foot positions relative to body
        if (leftFootIKTarget == null || rightFootIKTarget == null)
        {
            Debug.LogWarning("Foot IK targets not assigned! Create empty GameObjects and assign them.");
            return;
        }

        // Set up default positions (adjust these based on your character proportions)
        leftFootDefaultPos = new Vector3(-0.3f, -0.9f, 0.1f);
        rightFootDefaultPos = new Vector3(0.3f, -0.9f, 0.1f);

        // Initialize foot positions
        leftFootIKTarget.localPosition = leftFootDefaultPos;
        rightFootIKTarget.localPosition = rightFootDefaultPos;
        
        leftFootTargetPos = leftFootIKTarget.position;
        rightFootTargetPos = rightFootIKTarget.position;
        
        leftFootTargetRot = leftFootIKTarget.rotation;
        rightFootTargetRot = rightFootIKTarget.rotation;
    }

    void Update()
    {
        GroundCheck();

        if (!isRagdoll)
        {
            HandleCameraLook();
            ApplyBobbing();
            HandleJumpInput();
            UpdateMovementTracking();
            UpdateLegIK();
        }
    }

    private void FixedUpdate()
    {
        if (!isRagdoll)
        {
            CheckAdvancedRagdollCondition();

            if (!isRagdoll)
            {
                HandleMovement();

                if (isGrounded && Mathf.Abs(verticalVelocity) >= collisionRagdollThreshold)
                {
                    Vector3 force = new Vector3(0f, verticalVelocity, 0f);
                    EnterRagdoll(force, transform.position);
                }
            }
        }

        if (!isRagdoll)
        {
            lastVelocity = rb.linearVelocity;
        }
    }

    #region Ground Detection and Slope Handling

    void GroundCheck()
    {
        wasGrounded = isGrounded;
        previousVerticalVelocity = verticalVelocity;
        
        // Get detailed ground information
        isGrounded = IsGroundedHybrid(out currentGroundNormal, out RaycastHit groundInfo, out bool isCloseToGround);
        
        CalculateSlopeInfo();
        
        // Only reset vertical velocity if we're actually touching ground AND not falling fast
        if (isGrounded && !wasGrounded)
        {
            // Check if this is a hard landing that should trigger ragdoll
            float impactVelocity = Mathf.Abs(previousVerticalVelocity);
            
            // Only reset vertical velocity if the impact isn't severe enough for ragdoll
            if (impactVelocity >= collisionRagdollThreshold)
            {
                // Let the ragdoll system handle this - don't reset velocity yet
                Vector3 impactForce = new Vector3(0f, previousVerticalVelocity, 0f);
                EnterRagdoll(impactForce, transform.position);
            }
            else
            {
                // Safe landing - reset vertical velocity
                verticalVelocity = 0f;
            }
        }
        else if (isGrounded && wasGrounded && verticalVelocity < 0f)
        {
            // Already grounded and still falling - safe to reset
            verticalVelocity = 0f;
        }
    }

    void CalculateSlopeInfo()
    {
        if (isGrounded)
        {
            currentSlopeAngle = Vector3.Angle(currentGroundNormal, Vector3.up);
            isOnSlope = currentSlopeAngle > 5f;
            
            if (enableSlopeSliding && currentSlopeAngle > slopeSlipThreshold)
            {
                Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                Vector3 slopeDownDirection = Vector3.ProjectOnPlane(Vector3.down, currentGroundNormal).normalized;
                
                float movementAgainstSlope = Vector3.Dot(horizontalVel.normalized, -slopeDownDirection);
                
                isSlidingDownSlope = movementAgainstSlope < 0.5f && horizontalVel.magnitude < moveSpeed * 0.8f;
            }
            else
            {
                isSlidingDownSlope = false;
            }
        }
        else
        {
            currentSlopeAngle = 0f;
            isOnSlope = false;
            isSlidingDownSlope = false;
        }
    }

    bool IsGroundedHybrid(out Vector3 groundNormal, out RaycastHit groundInfo, out bool isCloseToGround)
    {
        groundNormal = Vector3.up;
        groundInfo = new RaycastHit();
        isCloseToGround = false;
        
        // First check contact-based grounding (most reliable for actual contact)
        if (IsGroundedContact(out Vector3 contactNorm))
        {
            groundNormal = contactNorm;
            
            Vector3 rayStart = transform.position + Vector3.up * groundCheckOffset;
            if (Physics.Raycast(rayStart, Vector3.down, out groundInfo, groundCheckRadius + groundCheckOffset, groundMask))
            {
                return true;
            }
            
            return true;
        }
        
        // Then check sphere overlap (this can detect when close but not touching)
        Vector3 sphereCenter = transform.position + Vector3.up * groundCheckOffset;
        if (Physics.CheckSphere(sphereCenter, groundCheckRadius, groundMask))
        {
            isCloseToGround = true;
            
            // Use a more restrictive raycast to determine actual ground contact
            Vector3 rayStart = transform.position + Vector3.up * (groundCheckOffset * 0.5f);
            if (Physics.Raycast(rayStart, Vector3.down, out groundInfo, groundCheckOffset + 0.05f, groundMask))
            {
                groundNormal = groundInfo.normal;
                return true; // Actually touching
            }
            
            // Close to ground but not actually touching - don't consider grounded
            Vector3 fallbackRayStart = transform.position + Vector3.up * groundCheckOffset;
            if (Physics.Raycast(fallbackRayStart, Vector3.down, out groundInfo, groundCheckRadius + groundCheckOffset, groundMask))
            {
                groundNormal = groundInfo.normal;
            }
            
            return false; // Close but not grounded
        }
        
        return false;
    }

    bool IsGroundedContact(out Vector3 groundNormal)
    {
        groundNormal = contactNormal;
        return contactGrounded;
    }

    #endregion

    #region Movement and Input

    private void UpdateMovementTracking()
    {
        currentHorizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        bool currentlyMoving = currentHorizontalVelocity.magnitude > stopPlantingSpeed;
        
        // Track stopping for foot planting
        if (wasMovingLastFrame && !currentlyMoving)
        {
            // Just stopped
            stoppedTime = 0f;
            hasPlantedAfterStop = false;
        }
        else if (!currentlyMoving)
        {
            // Still stopped
            stoppedTime += Time.deltaTime;
        }
        else if (currentlyMoving)
        {
            // Moving - reset stop tracking
            stoppedTime = 0f;
            hasPlantedAfterStop = false;
        }
        
        wasMoving = currentlyMoving;
        wasMovingLastFrame = currentlyMoving;
        isSprinting = Input.GetKey(KeyCode.LeftShift) && currentlyMoving;
        
        // Track rotation for stepping
        float currentYRotation = transform.eulerAngles.y;
        rotationDelta = Mathf.DeltaAngle(lastYRotation, currentYRotation);
        lastYRotation = currentYRotation;
        
        lastPosition = transform.position;
    }

    void HandleJumpInput()
    {
        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            verticalVelocity = jumpForce;
        }
    }

    void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 moveDir = (transform.right * h + transform.forward * v).normalized;
        bool sprinting = Input.GetKey(KeyCode.LeftShift);
        float targetSpeed = moveSpeed * (sprinting ? sprintMultiplier : 1f);

        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        if (moveDir.magnitude >= 0.1f)
        {
            if (isGrounded)
            {
                HandleGroundMovement(moveDir, targetSpeed, horizontalVelocity, sprinting);
            }
            else
            {
                rb.AddForce(moveDir * targetSpeed * airControlMultiplier, ForceMode.Acceleration);
            }
        }
        else if (isGrounded)
        {
            HandleGroundFriction(horizontalVelocity);
        }

        if (!isGrounded)
        {
            verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.fixedDeltaTime;
        }

        if (isSlidingDownSlope && isGrounded)
        {
            ApplySlopeSliding();
        }

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, verticalVelocity, rb.linearVelocity.z);

        ClampHorizontalSpeed(sprinting);
    }

    void HandleGroundMovement(Vector3 moveDir, float targetSpeed, Vector3 horizontalVelocity, bool sprinting)
    {
        Vector3 slopeMove = Vector3.ProjectOnPlane(moveDir, currentGroundNormal).normalized;
        
        float forceMultiplier = 1f;
        
        if (isOnSlope)
        {
            if (currentSlopeAngle > maxSlopeAngle)
            {
                Vector3 slopeUp = -Vector3.ProjectOnPlane(Vector3.down, currentGroundNormal).normalized;
                float climbingComponent = Vector3.Dot(slopeMove, slopeUp);
                
                if (climbingComponent > 0)
                {
                    float steepnessFactor = Mathf.Clamp01(1f - ((currentSlopeAngle - maxSlopeAngle) / 30f));
                    slopeMove = Vector3.Lerp(Vector3.ProjectOnPlane(slopeMove, slopeUp), slopeMove, steepnessFactor);
                    forceMultiplier *= steepnessFactor * 0.5f;
                }
            }
            else
            {
                Vector3 slopeDirection = Vector3.ProjectOnPlane(Vector3.down, currentGroundNormal).normalized;
                float movementAlignment = Vector3.Dot(slopeMove, slopeDirection);
                
                if (movementAlignment > 0)
                {
                    forceMultiplier *= downhillSpeedMultiplier;
                }
                else if (movementAlignment < -0.3f)
                {
                    forceMultiplier *= slopeForceMultiplier;
                    
                    if (currentSlopeAngle > 25f)
                    {
                        Vector3 uphillAssist = currentGroundNormal * (currentSlopeAngle / maxSlopeAngle) * 2f;
                        rb.AddForce(uphillAssist, ForceMode.Acceleration);
                    }
                }
            }
        }
        
        if (horizontalVelocity.magnitude < targetSpeed * forceMultiplier)
        {
            Vector3 force = slopeMove * targetSpeed * forceMultiplier;
            rb.AddForce(force, ForceMode.Acceleration);
        }
        
        if (isOnSlope && currentSlopeAngle > 15f && !isSlidingDownSlope)
        {
            Vector3 slopeDown = Vector3.ProjectOnPlane(Vector3.down, currentGroundNormal).normalized;
            Vector3 antiSlip = -slopeDown * antiSlipForce * (currentSlopeAngle / 90f);
            rb.AddForce(antiSlip, ForceMode.Acceleration);
        }
    }

    void HandleGroundFriction(Vector3 horizontalVelocity)
    {
        if (isSlidingDownSlope)
        {
            Vector3 frictionReduction = Vector3.Lerp(horizontalVelocity, Vector3.zero, stopFriction * 0.1f * Time.deltaTime);
            rb.linearVelocity = new Vector3(frictionReduction.x, rb.linearVelocity.y, frictionReduction.z);
        }
        else
        {
            float frictionMultiplier = isOnSlope ? Mathf.Clamp01(1f - (currentSlopeAngle / 90f) * 0.5f) : 1f;
            Vector3 friction = Vector3.Lerp(horizontalVelocity, Vector3.zero, stopFriction * frictionMultiplier * Time.deltaTime);
            rb.linearVelocity = new Vector3(friction.x, rb.linearVelocity.y, friction.z);
        }
    }

    void ApplySlopeSliding()
    {
        Vector3 slideDirection = Vector3.ProjectOnPlane(Vector3.down, currentGroundNormal).normalized;
        float slideForce = Mathf.Clamp01((currentSlopeAngle - slopeSlipThreshold) / 30f) * 15f;
        
        rb.AddForce(slideDirection * slideForce, ForceMode.Acceleration);
        
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        
        if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
        {
            Vector3 controlDir = (transform.right * h + transform.forward * v).normalized;
            Vector3 slideControl = Vector3.ProjectOnPlane(controlDir, currentGroundNormal) * slideControlFactor * moveSpeed;
            rb.AddForce(slideControl, ForceMode.Acceleration);
        }
    }

    void ClampHorizontalSpeed(bool sprinting)
    {
        Vector3 clampedH = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float maxSpeedNow = maxSpeed * (sprinting ? sprintMultiplier : 1f);
        
        if (isSlidingDownSlope)
        {
            maxSpeedNow *= 1.5f;
        }
        else if (isOnSlope)
        {
            Vector3 slopeDirection = Vector3.ProjectOnPlane(Vector3.down, currentGroundNormal).normalized;
            float downhillFactor = Vector3.Dot(clampedH.normalized, slopeDirection);
            
            if (downhillFactor > 0.5f)
            {
                maxSpeedNow *= downhillSpeedMultiplier;
            }
        }
        
        if (clampedH.magnitude > maxSpeedNow)
        {
            clampedH = clampedH.normalized * maxSpeedNow;
            rb.linearVelocity = new Vector3(clampedH.x, rb.linearVelocity.y, clampedH.z);
        }
    }

    #endregion

    #region Camera and Bobbing

    [Header("Z-Axis Movement")]
    public float maxForwardOffset = 0.3f; // Maximum forward movement when looking straight
    public float zTransitionSpeed = 5f; // Speed of Z position transition

    void HandleCameraLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        transform.Rotate(Vector3.up * mouseX);

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        // Calculate Z offset based on look angle
        // When xRotation is 0 (looking straight), we want maximum forward offset
        // When xRotation is at extremes (maxLookAngle), we want zero offset
        float normalizedAngle = Mathf.Abs(xRotation) / maxLookAngle; // 0 to 1
        float zOffset = maxForwardOffset * (1f - normalizedAngle); // Inverted: 1 at center, 0 at extremes

        // Apply angular bobbing to camera rotation
        Quaternion baseLook = Quaternion.Euler(xRotation, 0f, 0f);
        
        if (enableAngularBobbing)
        {
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            bool moving = horizontalVel.magnitude > 0.1f && isGrounded;
            
            if (moving)
            {
                angularBobTimer += Time.deltaTime * angularBobFrequency * (horizontalVel.magnitude / maxSpeed);
                float angularBob = Mathf.Sin(angularBobTimer) * angularBobAmplitude * (horizontalVel.magnitude / maxSpeed);
                baseLook *= Quaternion.Euler(0f, 0f, angularBob);
            }
            else
            {
                angularBobTimer = 0f;
            }
        }

        cameraHolder.localRotation = baseLook;

        // Apply Z position transition
        Vector3 currentPos = cameraHolder.localPosition;
        Vector3 targetPos = new Vector3(currentPos.x, currentPos.y, zOffset);
        cameraHolder.localPosition = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * zTransitionSpeed);
    }

    void ApplyBobbing()
    {
        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        bool moving = horizontalVel.magnitude > 0.1f && isGrounded;

        if (moving)
        {
            bool sprinting = Input.GetKey(KeyCode.LeftShift);
            float bobFreq = sprinting ? runBobFrequency : walkBobFrequency;
            
            bobTimer += Time.deltaTime * bobFreq * horizontalVel.magnitude / maxSpeed;
            float bobOffset = Mathf.Sin(bobTimer) * bobAmplitude;

            Vector3 targetPos = bobStartLocalPos + new Vector3(0f, bobOffset, 0f);
            bobHolder.localPosition = Vector3.Lerp(bobHolder.localPosition, targetPos, Time.deltaTime * 10f);
        }
        else
        {
            bobHolder.localPosition = Vector3.Lerp(bobHolder.localPosition, bobStartLocalPos, Time.deltaTime * 10f);
            bobTimer = 0f;
        }
    }

    #endregion

    #region Leg IK System

    void UpdateLegIK()
    {
        if (leftFootIKTarget == null || rightFootIKTarget == null) return;

        if (isRagdoll)
        {
            // Reset to default positions during ragdoll
            leftFootIKTarget.localPosition = Vector3.Lerp(leftFootIKTarget.localPosition, leftFootDefaultPos, Time.deltaTime * 5f);
            rightFootIKTarget.localPosition = Vector3.Lerp(rightFootIKTarget.localPosition, rightFootDefaultPos, Time.deltaTime * 5f);
            return;
        }

        // Update stepping for both feet
        UpdateFootStepping();

        // Handle step triggering
        if (isGrounded)
        {
            // Check for stop planting
            bool shouldPlantFromStop = ShouldPlantFromStop();
            
            if (wasMoving || ShouldStepFromRotation() || shouldPlantFromStop)
            {
                TryTriggerStep();
            }
        }
        else if (!isGrounded)
        {
            // In air - keep feet in default positions
            if (!leftFootStepping && !rightFootStepping)
            {
                ResetFeetToDefaults();
            }
        }

        // Apply current foot positions
        ApplyFootPositions();
    }

    void UpdateFootStepping()
    {
        // Calculate dynamic step speed based on player velocity
        float currentSpeed = currentHorizontalVelocity.magnitude;
        float maxPossibleSpeed = maxSpeed * sprintMultiplier; // Account for sprinting
        float velocityRatio = Mathf.Clamp01(currentSpeed / maxPossibleSpeed);
        
        // Scale step speed: slower when stationary, faster when at max speed
        float dynamicStepSpeed = stepSpeed * Mathf.Lerp(minStepSpeedRatio, stepSpeedMultiplier, velocityRatio);
        
        // Update left foot stepping
        if (leftFootStepping)
        {
            leftStepTimer += Time.deltaTime * dynamicStepSpeed;
            if (leftStepTimer >= 1f)
            {
                leftStepTimer = 1f;
                leftFootStepping = false;
                PlayFootstepSound(5f); // Trigger footstep sound
            }

            float t = EaseInOutQuad(leftStepTimer);
            
            // Interpolate position with arc
            Vector3 currentPos = Vector3.Lerp(leftFootStartPos, leftFootTargetPos, t);
            float height = Mathf.Sin(t * Mathf.PI) * stepHeight;
            currentPos.y += height;
            
            leftFootIKTarget.position = currentPos;
            
            // Interpolate rotation
            leftFootIKTarget.rotation = Quaternion.Lerp(leftFootStartRot, leftFootTargetRot, t);
        }

        // Update right foot stepping
        if (rightFootStepping)
        {
            rightStepTimer += Time.deltaTime * dynamicStepSpeed;
            if (rightStepTimer >= 1f)
            {
                rightStepTimer = 1f;
                rightFootStepping = false;
                PlayFootstepSound(5f);
            }

            float t = EaseInOutQuad(rightStepTimer);
            
            Vector3 currentPos = Vector3.Lerp(rightFootStartPos, rightFootTargetPos, t);
            float height = Mathf.Sin(t * Mathf.PI) * stepHeight;
            currentPos.y += height;
            
            rightFootIKTarget.position = currentPos;
            rightFootIKTarget.rotation = Quaternion.Lerp(rightFootStartRot, rightFootTargetRot, t);
        }
    }

    void TryTriggerStep()
    {
        float currentTime = Time.time;
        if (currentTime - lastStepTime < minStepInterval) return;

        Vector3 bodyVelocity = currentHorizontalVelocity;
        Vector3 bodyForward = transform.forward;
        Vector3 bodyRight = transform.right;

        // Add velocity prediction - place feet ahead based on movement
        Vector3 velocityOffset = Vector3.zero;
        if (bodyVelocity.magnitude > minVelocityForPrediction)
        {
            velocityOffset = bodyVelocity.normalized * (bodyVelocity.magnitude * velocityPrediction);
            // Clamp the prediction to reasonable limits
            velocityOffset = Vector3.ClampMagnitude(velocityOffset, stepDistance * 0.8f);
        }

        // Calculate where feet should be (with velocity prediction)
        Vector3 predictedBodyPos = transform.position + velocityOffset;
        Vector3 leftIdealPos = predictedBodyPos + (-bodyRight * 0.3f) + (bodyForward * 0.1f);
        Vector3 rightIdealPos = predictedBodyPos + (bodyRight * 0.3f) + (bodyForward * 0.1f);

        float leftDist = Vector3.Distance(leftFootIKTarget.position, leftIdealPos);
        float rightDist = Vector3.Distance(rightFootIKTarget.position, rightIdealPos);

        // Check for rotation-based stepping
        bool rotationStep = ShouldStepFromRotation();
        bool stopPlantStep = ShouldPlantFromStop();

        // Determine which foot should step
        bool shouldStepLeft = false;
        bool shouldStepRight = false;

        // Special handling for stop planting - always use centered positions
        if (stopPlantStep && !hasPlantedAfterStop)
        {
            // Plant feet centered under body
            Vector3 centeredLeft = transform.position + (-bodyRight * 0.25f);
            Vector3 centeredRight = transform.position + (bodyRight * 0.25f);
            
            // Choose which foot is further from center
            float leftCenterDist = Vector3.Distance(leftFootIKTarget.position, centeredLeft);
            float rightCenterDist = Vector3.Distance(rightFootIKTarget.position, centeredRight);
            
            if (alternateSteps)
            {
                // Plant both feet to center position
                if (!leftFootStepping && !rightFootStepping)
                {
                    // Plant the foot that's further from center first
                    if (leftCenterDist > rightCenterDist)
                    {
                        shouldStepLeft = true;
                        leftIdealPos = centeredLeft;
                    }
                    else
                    {
                        shouldStepRight = true;
                        rightIdealPos = centeredRight;
                    }
                }
            }
            else
            {
                // Plant both feet if they're far enough from center
                if (leftCenterDist > 0.3f && !leftFootStepping)
                {
                    shouldStepLeft = true;
                    leftIdealPos = centeredLeft;
                }
                if (rightCenterDist > 0.3f && !rightFootStepping)
                {
                    shouldStepRight = true;
                    rightIdealPos = centeredRight;
                }
            }
            
            hasPlantedAfterStop = true;
        }
        else if (alternateSteps)
        {
            // Normal alternating stepping logic
            if (isLeftFootTurn && (leftDist > stepDistance || rotationStep) && !leftFootStepping && !rightFootStepping)
            {
                shouldStepLeft = true;
                isLeftFootTurn = false;
            }
            else if (!isLeftFootTurn && (rightDist > stepDistance || rotationStep) && !leftFootStepping && !rightFootStepping)
            {
                shouldStepRight = true;
                isLeftFootTurn = true;
            }
            // Emergency case: if one foot is way behind
            else if (leftDist > maxStepDistance && !leftFootStepping)
            {
                shouldStepLeft = true;
                isLeftFootTurn = false;
            }
            else if (rightDist > maxStepDistance && !rightFootStepping)
            {
                shouldStepRight = true;
                isLeftFootTurn = true;
            }
        }
        else
        {
            // Both feet can step independently
            shouldStepLeft = (leftDist > stepDistance || rotationStep) && !leftFootStepping;
            shouldStepRight = (rightDist > stepDistance || rotationStep) && !rightFootStepping;
        }

        // Execute steps
        if (shouldStepLeft)
        {
            StartLeftFootStep(leftIdealPos);
            lastStepTime = currentTime;
        }

        if (shouldStepRight)
        {
            StartRightFootStep(rightIdealPos);
            lastStepTime = currentTime;
        }
    }

    bool ShouldStepFromRotation()
    {
        return Mathf.Abs(rotationDelta) > rotationStepThreshold * Time.deltaTime;
    }

    bool ShouldPlantFromStop()
    {
        return !wasMoving && stoppedTime > stopPlantingDelay && !hasPlantedAfterStop;
    }

    void StartLeftFootStep(Vector3 targetPosition)
    {
        leftFootStepping = true;
        leftStepTimer = 0f;
        leftFootStartPos = leftFootIKTarget.position;
        leftFootStartRot = leftFootIKTarget.rotation;
        
        // Raycast to find ground
        Vector3 rayStart = targetPosition + Vector3.up * footRaycastDistance;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, footRaycastDistance * 2f, groundMask))
        {
            leftFootTargetPos = hit.point + Vector3.up * footGroundOffset;
            
            if (adaptToSurfaceNormal)
            {
                leftFootTargetRot = CalculateFootRotationFromNormal(hit.normal);
            }
            else
            {
                leftFootTargetRot = transform.rotation;
            }
        }
        else
        {
            leftFootTargetPos = targetPosition;
            leftFootTargetRot = transform.rotation;
        }
    }

    void StartRightFootStep(Vector3 targetPosition)
    {
        rightFootStepping = true;
        rightStepTimer = 0f;
        rightFootStartPos = rightFootIKTarget.position;
        rightFootStartRot = rightFootIKTarget.rotation;
        
        Vector3 rayStart = targetPosition + Vector3.up * footRaycastDistance;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, footRaycastDistance * 2f, groundMask))
        {
            rightFootTargetPos = hit.point + Vector3.up * footGroundOffset;
            
            if (adaptToSurfaceNormal)
            {
                rightFootTargetRot = CalculateFootRotationFromNormal(hit.normal);
            }
            else
            {
                rightFootTargetRot = transform.rotation;
            }
        }
        else
        {
            rightFootTargetPos = targetPosition;
            rightFootTargetRot = transform.rotation;
        }
    }

    Quaternion CalculateFootRotationFromNormal(Vector3 surfaceNormal)
    {
        // Ensure the surface isn't too steep for foot adaptation
        float angle = Vector3.Angle(surfaceNormal, Vector3.up);
        if (angle > maxSurfaceAngle)
        {
            return transform.rotation; // Use body rotation instead
        }

        // Calculate foot rotation to match surface
        Vector3 footForward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal).normalized;
        return Quaternion.LookRotation(footForward, surfaceNormal);
    }

    void ResetFeetToDefaults()
    {
        Vector3 leftTarget = transform.TransformPoint(leftFootDefaultPos);
        Vector3 rightTarget = transform.TransformPoint(rightFootDefaultPos);
        
        leftFootIKTarget.position = Vector3.Lerp(leftFootIKTarget.position, leftTarget, Time.deltaTime * 5f);
        rightFootIKTarget.position = Vector3.Lerp(rightFootIKTarget.position, rightTarget, Time.deltaTime * 5f);
        
        leftFootIKTarget.rotation = Quaternion.Lerp(leftFootIKTarget.rotation, transform.rotation, Time.deltaTime * 5f);
        rightFootIKTarget.rotation = Quaternion.Lerp(rightFootIKTarget.rotation, transform.rotation, Time.deltaTime * 5f);
    }

    void ApplyFootPositions()
    {
        // Smooth surface normal adaptation when not stepping
        if (!leftFootStepping && adaptToSurfaceNormal && isGrounded)
        {
            Vector3 rayStart = leftFootIKTarget.position + Vector3.up * 0.5f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit leftHit, 1f, groundMask))
            {
                Quaternion targetRot = CalculateFootRotationFromNormal(leftHit.normal);
                leftFootIKTarget.rotation = Quaternion.Lerp(leftFootIKTarget.rotation, targetRot, Time.deltaTime * surfaceAdaptationSpeed);
            }
        }

        if (!rightFootStepping && adaptToSurfaceNormal && isGrounded)
        {
            Vector3 rayStart = rightFootIKTarget.position + Vector3.up * 0.5f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit rightHit, 1f, groundMask))
            {
                Quaternion targetRot = CalculateFootRotationFromNormal(rightHit.normal);
                rightFootIKTarget.rotation = Quaternion.Lerp(rightFootIKTarget.rotation, targetRot, Time.deltaTime * surfaceAdaptationSpeed);
            }
        }
    }

    float EaseInOutQuad(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }

    #endregion

    #region Ragdoll System

    #region Enhanced Ragdoll Detection

    private void CheckAdvancedRagdollCondition()
    {
        if (isRagdoll)
        {
            ragdollTimer += Time.fixedDeltaTime;
            return;
        }

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 currentAngularVelocity = rb.angularVelocity;
        float currentSpeedMag = currentVelocity.magnitude;
        float lastSpeedMag = lastVelocity.magnitude;

        // Enhanced impact detection for falling
        float verticalImpact = Mathf.Abs(currentVelocity.y);
        bool hardFallDetected = false;
        
        // Check for sudden vertical velocity changes (impacts)
        if (isGrounded && !wasGrounded)
        {
            float fallImpact = Mathf.Abs(lastVelocity.y);
            if (fallImpact >= collisionRagdollThreshold)
            {
                hardFallDetected = true;
                Debug.Log($"Hard fall detected! Impact velocity: {fallImpact}");
            }
        }

        float deltaSpeed = Mathf.Abs(currentSpeedMag - lastSpeedMag) / Time.fixedDeltaTime;

        float directionChange = 0f;
        if (lastSpeedMag > 1f && currentSpeedMag > 1f)
        {
            Vector3 lastDir = lastVelocity.normalized;
            Vector3 currentDir = currentVelocity.normalized;
            float dot = Vector3.Dot(lastDir, currentDir);
            directionChange = (1f - dot) * currentSpeedMag;
        }

        float angularMagnitude = currentAngularVelocity.magnitude;
        bool spinCondition = angularMagnitude > ragdollAngularThreshold;

        Vector3 currentAcceleration = (currentVelocity - lastVelocity) / Time.fixedDeltaTime;
        float jerkMagnitude = (currentAcceleration - lastAcceleration).magnitude / Time.fixedDeltaTime;
        bool jerkCondition = jerkMagnitude > ragdollJerkThreshold;

        bool forceCondition = lastCollisionForce > ragdollForceThreshold;

        velocityHistory[velocityHistoryIndex] = currentVelocity;
        velocityHistoryIndex = (velocityHistoryIndex + 1) % velocityHistory.Length;

        float velocityVariance = CalculateVelocityVariance();
        bool erraticMovement = velocityVariance > ragdollVarianceThreshold;

        // Immediate ragdoll triggers (no sustained time required)
        if (hardFallDetected || forceCondition)
        {
            Vector3 ragdollForce = hardFallDetected ? 
                new Vector3(currentVelocity.x, lastVelocity.y, currentVelocity.z) : 
                currentVelocity - lastVelocity;
                
            if (ragdollForce.magnitude < 5f)
                ragdollForce = currentVelocity.normalized * 5f;

            EnterRagdoll(ragdollForce, transform.position);
            
            sustainedChangeTime = 0f;
            System.Array.Clear(velocityHistory, 0, velocityHistory.Length);
            velocityHistoryIndex = 0;
            return;
        }

        // Sustained condition checks (require time)
        if ((currentSpeedMag > 5f || angularMagnitude > ragdollAngularThreshold) && ragdollTimer >= ragdollCooldown)
        {
            bool speedCondition = deltaSpeed >= ragdollSpeedChangeThreshold;
            bool directionCondition = directionChange >= ragdollDirectionChangeThreshold;

            if (speedCondition || directionCondition || spinCondition || jerkCondition || erraticMovement)
            {
                float requiredSustainedTime = 0.15f;

                if (directionCondition || spinCondition) requiredSustainedTime = 0.05f;
                if (jerkCondition) requiredSustainedTime = 0.02f;
                if (erraticMovement) requiredSustainedTime = 0.1f;

                sustainedChangeTime += Time.fixedDeltaTime;

                if (sustainedChangeTime >= requiredSustainedTime)
                {
                    Vector3 ragdollForce = currentVelocity - lastVelocity;
                    if (ragdollForce.magnitude < 5f)
                        ragdollForce = currentVelocity.normalized * 5f;

                    EnterRagdoll(ragdollForce, transform.position);

                    sustainedChangeTime = 0f;
                    System.Array.Clear(velocityHistory, 0, velocityHistory.Length);
                    velocityHistoryIndex = 0;
                }
            }
            else
            {
                sustainedChangeTime = 0f;
            }
        }
        else
        {
            sustainedChangeTime = 0f;
        }

        lastAngularVelocity = currentAngularVelocity;
        lastAcceleration = currentAcceleration;
        lastCollisionForce = 0f;
        ragdollTimer += Time.fixedDeltaTime;
    }

    #endregion

    private float CalculateVelocityVariance()
    {
        if (velocityHistory[velocityHistory.Length - 1] == Vector3.zero) return 0f;

        Vector3 average = Vector3.zero;
        int validSamples = 0;

        for (int i = 0; i < velocityHistory.Length; i++)
        {
            if (velocityHistory[i] != Vector3.zero)
            {
                average += velocityHistory[i];
                validSamples++;
            }
        }

        if (validSamples < 2) return 0f;

        average /= validSamples;

        float variance = 0f;
        for (int i = 0; i < velocityHistory.Length; i++)
        {
            if (velocityHistory[i] != Vector3.zero)
            {
                variance += (velocityHistory[i] - average).sqrMagnitude;
            }
        }

        return variance / validSamples;
    }

    void EnterRagdoll(Vector3 collisionForce, Vector3 collisionPoint)
    {
        if (isRagdoll) return;

        rrm.RequestRagdoll(pcd, this.gameObject.GetComponent<Rigidbody>(), this.GetComponentInChildren<Camera>(), disableObject);
    }

    void ExitRagdoll()
    {
        if (!isRagdoll) return;

        isRagdoll = false;
        rb.freezeRotation = true;
        ragdollTimer = 0f;
        
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, Time.deltaTime * 5f);
    }

    #endregion

    #region Collision and Audio

    private void OnCollisionEnter(Collision collision)
    {
        if (isRagdoll) return;

        lastCollisionForce = collision.impulse.magnitude / Time.fixedDeltaTime;

        Vector3 velocityChange = rb.linearVelocity - lastVelocity;
        float impact = Mathf.Max(collision.relativeVelocity.magnitude, velocityChange.magnitude);

        if (impact >= collisionRagdollThreshold)
        {
            Vector3 force = collision.relativeVelocity;
            Vector3 point = collision.contacts[0].point;
            EnterRagdoll(force, point);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        bool foundGroundContact = false;
        Vector3 normalSum = Vector3.zero;
        int validContacts = 0;
        
        foreach (ContactPoint contact in collision.contacts)
        {
            if (((1 << collision.gameObject.layer) & groundMask) != 0)
            {
                Vector3 toContact = contact.point - transform.position;
                if (toContact.y <= groundCheckRadius)
                {
                    if (Vector3.Dot(contact.normal, Vector3.up) > 0.3f)
                    {
                        foundGroundContact = true;
                        normalSum += contact.normal;
                        validContacts++;
                    }
                }
            }
        }
        
        if (foundGroundContact && validContacts > 0)
        {
            contactGrounded = true;
            contactNormal = (normalSum / validContacts).normalized;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & groundMask) != 0)
        {
            contactGrounded = false;
        }
    }

    private void PlayFootstepSound(float impactForce)
    {
        if (footstepSource == null || footstepSounds == null || footstepSounds.Length == 0)
            return;

        float normalizedImpact = Mathf.Clamp01(impactForce / 10f);
        footstepSource.volume = 0.5f + (normalizedImpact * 0.5f);
        footstepSource.pitch = 0.8f + (Random.Range(0f, 1f) * 0.4f);

        AudioClip clip = footstepSounds[Random.Range(0, footstepSounds.Length)];
        footstepSource.PlayOneShot(clip);
    }

    #endregion

    #region Public Interface

    public bool IsMoving() => wasMoving;
    public bool IsSprinting() => isSprinting;
    public bool IsCrouching() => Input.GetKey(KeyCode.LeftControl);
    public float GetCurrentSpeed() => currentHorizontalVelocity.magnitude;
    public Vector3 GetVelocity() => rb.linearVelocity;
    public Vector3 GetHorizontalVelocity() => currentHorizontalVelocity;
    public bool IsGrounded() => isGrounded;
    public bool IsRagdoll() => isRagdoll;
    public bool IsOnSlope() => isOnSlope;
    public float GetSlopeAngle() => currentSlopeAngle;
    public bool IsSlidingDownSlope() => isSlidingDownSlope;
    public Vector3 GetGroundNormal() => currentGroundNormal;

    public void ForceRagdoll(Vector3 force, Vector3 point)
    {
        EnterRagdoll(force, point);
    }

    public void ForceExitRagdoll()
    {
        ExitRagdoll();
    }

    public void TryRecoverFromRagdoll()
    {
        if (isRagdoll && isGrounded && rb.linearVelocity.magnitude < 2f)
        {
            ExitRagdoll();
        }
    }

    public string GetDebugInfo()
    {
        return $"Bean State - Moving: {wasMoving}, Sprinting: {isSprinting}, " +
               $"Speed: {currentHorizontalVelocity.magnitude:F2}, Grounded: {isGrounded}, " +
               $"Ragdoll: {isRagdoll}, Slope: {currentSlopeAngle:F1}°, Sliding: {isSlidingDownSlope}, " +
               $"Left Stepping: {leftFootStepping}, Right Stepping: {rightFootStepping}, " +
               $"Rotation Delta: {rotationDelta:F1}°, Stopped Time: {stoppedTime:F1}s";
    }

    public void SetMoveSpeed(float newSpeed) => moveSpeed = newSpeed;
    public void SetSprintMultiplier(float newMultiplier) => sprintMultiplier = newMultiplier;
    public void SetMaxSpeed(float newMaxSpeed) => maxSpeed = newMaxSpeed;
    public void SetMaxSlopeAngle(float newAngle) => maxSlopeAngle = newAngle;
    public void SetSlopeForceMultiplier(float newMultiplier) => slopeForceMultiplier = newMultiplier;

    // IK-specific public methods
    public void SetStepDistance(float newDistance) => stepDistance = newDistance;
    public void SetStepHeight(float newHeight) => stepHeight = newHeight;
    public void SetStepSpeed(float newSpeed) => stepSpeed = newSpeed;
    public void SetStepSpeedMultiplier(float multiplier) => stepSpeedMultiplier = multiplier;
    public void SetMinStepSpeedRatio(float ratio) => minStepSpeedRatio = ratio;
    public void EnableSurfaceAdaptation(bool enable) => adaptToSurfaceNormal = enable;
    public void EnableAngularBobbing(bool enable) => enableAngularBobbing = enable;
    public void SetAngularBobAmplitude(float amplitude) => angularBobAmplitude = amplitude;

    // Advanced stepping controls
    public void SetRotationStepThreshold(float threshold) => rotationStepThreshold = threshold;
    public void SetVelocityPrediction(float prediction) => velocityPrediction = prediction;
    public void SetMinVelocityForPrediction(float minVel) => minVelocityForPrediction = minVel;
    public void SetStopPlantingDelay(float delay) => stopPlantingDelay = delay;
    public void SetStopPlantingSpeed(float speed) => stopPlantingSpeed = speed;

    public bool IsLeftFootStepping() => leftFootStepping;
    public bool IsRightFootStepping() => rightFootStepping;
    public Vector3 GetLeftFootPosition() => leftFootIKTarget != null ? leftFootIKTarget.position : Vector3.zero;
    public Vector3 GetRightFootPosition() => rightFootIKTarget != null ? rightFootIKTarget.position : Vector3.zero;

    #endregion

    #region Gizmos and Debug

    void OnDrawGizmosSelected()
    {
        // Draw improved ground check
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector3 sphereCenter = transform.position + Vector3.up * groundCheckOffset;
        Gizmos.DrawWireSphere(sphereCenter, groundCheckRadius);
        
        // Draw ground normal
        if (Application.isPlaying && isGrounded)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, currentGroundNormal * 2f);
            
            // Draw slope information
            if (isOnSlope)
            {
                Gizmos.color = currentSlopeAngle > maxSlopeAngle ? Color.red : Color.yellow;
                Vector3 slopeDown = Vector3.ProjectOnPlane(Vector3.down, currentGroundNormal).normalized;
                Gizmos.DrawRay(transform.position, slopeDown * 2f);
                
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
                    $"Slope: {currentSlopeAngle:F1}°" + (isSlidingDownSlope ? " SLIDING" : ""));
                #endif
            }
        }

        // Draw movement direction when moving
        if (Application.isPlaying && wasMoving)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, currentHorizontalVelocity.normalized * 2f);
        }

        // Draw ragdoll detection sphere
        if (Application.isPlaying)
        {
            Gizmos.color = isRagdoll ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
        
        // Draw slope limits
        if (!Application.isPlaying)
        {
            Gizmos.color = Color.magenta;
            float slopeRad = maxSlopeAngle * Mathf.Deg2Rad;
            Vector3 maxSlopeDir = new Vector3(Mathf.Sin(slopeRad), Mathf.Cos(slopeRad), 0);
            Gizmos.DrawRay(transform.position, maxSlopeDir * 2f);
            Gizmos.DrawRay(transform.position, new Vector3(-maxSlopeDir.x, maxSlopeDir.y, 0) * 2f);
        }

        // Draw IK system gizmos
        if (Application.isPlaying && leftFootIKTarget != null && rightFootIKTarget != null)
        {
            // Draw foot target positions
            Gizmos.color = leftFootStepping ? Color.green : Color.blue;
            Gizmos.DrawWireSphere(leftFootIKTarget.position, 0.1f);
            
            Gizmos.color = rightFootStepping ? Color.green : Color.blue;
            Gizmos.DrawWireSphere(rightFootIKTarget.position, 0.1f);
            
            // Draw step distance indicators
            Gizmos.color = Color.white;
            Vector3 bodyForward = transform.forward;
            Vector3 bodyRight = transform.right;
            
            Vector3 leftIdealPos = transform.position + (-bodyRight * 0.3f) + (bodyForward * 0.1f);
            Vector3 rightIdealPos = transform.position + (bodyRight * 0.3f) + (bodyForward * 0.1f);
            
            Gizmos.DrawWireSphere(leftIdealPos, stepDistance);
            Gizmos.DrawWireSphere(rightIdealPos, stepDistance);
            
            // Draw lines from feet to ideal positions
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(leftFootIKTarget.position, leftIdealPos);
            Gizmos.DrawLine(rightFootIKTarget.position, rightIdealPos);
        }

        // Draw foot raycast distances
        if (leftFootIKTarget != null && rightFootIKTarget != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 leftRayStart = leftFootIKTarget.position + Vector3.up * footRaycastDistance;
            Vector3 rightRayStart = rightFootIKTarget.position + Vector3.up * footRaycastDistance;
            
            Gizmos.DrawLine(leftRayStart, leftRayStart + Vector3.down * footRaycastDistance * 2f);
            Gizmos.DrawLine(rightRayStart, rightRayStart + Vector3.down * footRaycastDistance * 2f);
        }
    }

    #endregion
}