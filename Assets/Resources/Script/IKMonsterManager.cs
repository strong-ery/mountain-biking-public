using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BipedalIKManager : MonoBehaviour
{
    [Header("Body Settings")]
    public Transform rootTransform;
    public float stepDistance = 0.8f;
    public float minStepDistance = 0.3f;
    public float rotationStepThreshold = 45f;
    
    [Header("Gait Settings")]
    public float strideLength = 1.2f;
    public float strideWidth = 0.3f;
    public float maxStepHeight = 0.4f;
    public bool alternateSteps = true;
    public float stepOverlapPrevention = 0.1f;
    
    [Header("Body Adjustment")]
    public bool adjustRootHeight = true;
    public float rootAdjustmentSpeed = 3f;
    public float maxRootAdjustment = 0.3f;
    public float groundClearance = 0.05f;

    [Header("Foot Placement")]
    public bool alignToGroundNormal = true;
    public float maxGroundAngle = 45f;
    public float footPlantDuration = 0.1f;

    [Header("Smart Integration")]
    public BeanController beanController;
    public float velocityPrediction = 0.5f;        // How far ahead to predict movement
    public float restStabilityRadius = 0.2f;       // How close feet should be when stationary
    public float dynamicStepThreshold = 0.1f;      // Minimum velocity to trigger dynamic stepping
    public float footReturnSpeed = 2f;             // Speed at which feet return to rest position
    public AnimationCurve strideLengthCurve = AnimationCurve.Linear(0, 0.5f, 1, 1.5f); // Speed to stride multiplier
    public AnimationCurve stepFrequencyCurve = AnimationCurve.Linear(0, 0.5f, 1, 2f);   // Speed to step frequency

    [System.Serializable]
    public class FootData
    {
        [Header("Foot Components")]
        public Transform target;
        public Transform pole;
        public Transform footTransform;
        public AudioSource audioSource;

        [Header("Foot Properties")]
        public Vector3 offsetFromRoot = Vector3.zero;
        public Vector3 poleOffset = Vector3.zero;
        public bool isLeftFoot = true;

        [Header("Step Customization")]
        public float stepHeight = 0.3f;
        public float stepSpeed = 4f;
        public float footRotationSpeed = 8f;

        [Header("Ground Detection")]
        public LayerMask groundLayer = 1;
        public float raycastDistance = 2f;
        public float raycastHeightOffset = 1f;
        public float footLength = 0.2f;

        [HideInInspector] public bool isStepping = false;
        [HideInInspector] public Vector3 currentTargetPos;
        [HideInInspector] public Quaternion currentTargetRot;
        [HideInInspector] public Vector3 restPosition;
        [HideInInspector] public Vector3 plantedPosition;
        [HideInInspector] public float lastStepTime;
        [HideInInspector] public Vector3 lastGroundNormal = Vector3.up;
        [HideInInspector] public float stepCycleTime = 0f;
        [HideInInspector] public Vector3 idealRestPosition;
        [HideInInspector] public float distanceFromIdeal;
    }

    [Header("Feet")]
    public FootData leftFoot = new FootData { isLeftFoot = true };
    public FootData rightFoot = new FootData { isLeftFoot = false };

    [Header("Footstep Sounds")]
    public AudioClip[] footstepSounds;
    public AudioClip[] leftFootSounds;
    public AudioClip[] rightFootSounds;
    
    [Header("Debug Visualization")]
    public bool showGizmos = true;
    public bool showStepRanges = true;
    public bool showGroundChecks = true;
    public bool showFootRaycasts = true;
    public bool showPredictedPath = true;
    public Color leftFootColor = Color.blue;
    public Color rightFootColor = Color.red;
    public Color gizmoColor = Color.green;
    
    // Private variables
    private Vector3 lastRootPosition;
    private Quaternion lastRootRotation;
    private Vector3 velocity;
    private Vector3 smoothedVelocity;
    private bool isMoving = false;
    private bool wasMoving = false;
    private int lastSteppingFoot = -1;
    private float targetRootHeight;
    private Vector3 movementDirection;
    private float movementSpeed;
    private float originalRootHeight;
    private float stepCycle = 0f;
    private MovementState currentMovementState;
    private MovementState previousMovementState;
    private float stateChangeTime;

    public enum MovementState
    {
        Idle,
        Walking,
        Running,
        Crouching,
        Jumping,
        Stopping
    }

    void Start()
    {
        InitializeFeet();
        lastRootPosition = rootTransform.position;
        lastRootRotation = rootTransform.rotation;
        originalRootHeight = rootTransform.position.y;
        targetRootHeight = rootTransform.position.y;
        
        // Try to find BeanController if not assigned
        if (beanController == null)
            beanController = GetComponent<BeanController>();
    }

    void Update()
    {
        UpdateMovementTracking();
        UpdateMovementState();
        UpdateFootTargets();
        HandleMovementBasedStepping();

        if (adjustRootHeight)
            AdjustRootHeight();
    }

    void UpdateMovementTracking()
    {
        velocity = (rootTransform.position - lastRootPosition) / Time.deltaTime;
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, Time.deltaTime * 5f);
        movementSpeed = smoothedVelocity.magnitude;
        
        wasMoving = isMoving;
        isMoving = movementSpeed > dynamicStepThreshold;

        if (isMoving)
            movementDirection = smoothedVelocity.normalized;
    }

    void UpdateMovementState()
    {
        previousMovementState = currentMovementState;
        
        if (beanController != null)
        {
            // Get movement state from bean controller
            Rigidbody rb = beanController.GetComponent<Rigidbody>();
            
            if (beanController.isRagdoll)
            {
                currentMovementState = MovementState.Jumping; // Treat ragdoll as airborne
            }
            else if (!beanController.isGrounded)
            {
                currentMovementState = MovementState.Jumping;
            }
            else if (Input.GetKey(KeyCode.LeftControl)) // Assuming crouch is still left control
            {
                currentMovementState = MovementState.Crouching;
            }
            else if (movementSpeed > beanController.moveSpeed * beanController.sprintMultiplier * 0.8f)
            {
                currentMovementState = MovementState.Running;
            }
            else if (movementSpeed > dynamicStepThreshold)
            {
                currentMovementState = MovementState.Walking;
            }
            else if (wasMoving && !isMoving)
            {
                currentMovementState = MovementState.Stopping;
            }
            else
            {
                currentMovementState = MovementState.Idle;
            }
        }
        else
        {
            // Fallback state detection
            if (movementSpeed > 10f) // Approximate sprint speed
                currentMovementState = MovementState.Running;
            else if (movementSpeed > dynamicStepThreshold)
                currentMovementState = MovementState.Walking;
            else if (wasMoving && !isMoving)
                currentMovementState = MovementState.Stopping;
            else
                currentMovementState = MovementState.Idle;
        }

        if (currentMovementState != previousMovementState)
        {
            stateChangeTime = Time.time;
            OnMovementStateChanged();
        }
    }

    void OnMovementStateChanged()
    {
        switch (currentMovementState)
        {
            case MovementState.Stopping:
                // Force feet to return to stable positions
                StartCoroutine(ReturnFeetToRestPosition());
                break;
                
            case MovementState.Idle:
                // Ensure feet are in comfortable positions
                break;
                
            case MovementState.Walking:
            case MovementState.Running:
                // Adjust step parameters based on speed
                AdjustStepParametersForSpeed();
                break;
        }
    }

    void AdjustStepParametersForSpeed()
    {
        float maxSpeed = beanController != null ? 
            (beanController.maxSpeed * beanController.sprintMultiplier) : 15f;
        float normalizedSpeed = Mathf.Clamp01(movementSpeed / maxSpeed);
        
        // Only adjust stride length based on speed, not step speed
        float dynamicStrideLength = strideLength * strideLengthCurve.Evaluate(normalizedSpeed);
        
        // Don't modify step speed - let user control it directly
    }

    IEnumerator ReturnFeetToRestPosition()
    {
        float returnTime = 1f / footReturnSpeed;
        float elapsed = 0f;

        Vector3 leftStartPos = leftFoot.currentTargetPos;
        Vector3 rightStartPos = rightFoot.currentTargetPos;
        
        while (elapsed < returnTime && currentMovementState == MovementState.Stopping)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnTime;

            // Calculate ideal rest positions
            Vector3 leftIdealRest = CalculateIdealRestPosition(leftFoot);
            Vector3 rightIdealRest = CalculateIdealRestPosition(rightFoot);

            // Smoothly move feet to rest positions
            if (!leftFoot.isStepping)
            {
                leftFoot.currentTargetPos = Vector3.Lerp(leftStartPos, leftIdealRest, t);
                leftFoot.target.position = leftFoot.currentTargetPos;
            }

            if (!rightFoot.isStepping)
            {
                rightFoot.currentTargetPos = Vector3.Lerp(rightStartPos, rightIdealRest, t);
                rightFoot.target.position = rightFoot.currentTargetPos;
            }

            yield return null;
        }
    }

    Vector3 CalculateIdealRestPosition(FootData foot)
    {
        // When idle, feet should be directly below the body in a stable stance
        Vector3 basePos = rootTransform.position;
        
        // Simple offset directly below the body
        Vector3 lateralOffset = rootTransform.right * (foot.isLeftFoot ? -strideWidth : strideWidth) * 0.5f;
        basePos += lateralOffset;
        
        // Add the original foot offset but only use the Y component to maintain height
        Vector3 originalOffset = rootTransform.TransformDirection(foot.offsetFromRoot);
        basePos.y += originalOffset.y;
        
        return basePos;
    }

    void InitializeFeet()
    {
        InitializeFoot(leftFoot);
        InitializeFoot(rightFoot);
    }

    void InitializeFoot(FootData foot)
    {
        if (foot.target == null || foot.pole == null) return;

        foot.restPosition = rootTransform.position + rootTransform.TransformDirection(foot.offsetFromRoot);
        foot.idealRestPosition = foot.restPosition;
        foot.currentTargetPos = foot.restPosition;
        foot.currentTargetRot = rootTransform.rotation;
        foot.plantedPosition = foot.restPosition;
        foot.lastStepTime = Time.time;

        GroundFoot(foot);

        foot.target.position = foot.currentTargetPos;
        foot.target.rotation = foot.currentTargetRot;
        foot.pole.position = foot.target.position + rootTransform.TransformDirection(foot.poleOffset);
    }

    void UpdateFootTargets()
    {
        UpdateFootTarget(leftFoot);
        UpdateFootTarget(rightFoot);
    }

    void UpdateFootTarget(FootData foot)
    {
        if (foot.target == null || foot.pole == null) return;

        // Update ideal rest position
        foot.idealRestPosition = CalculateIdealRestPosition(foot);
        foot.distanceFromIdeal = Vector3.Distance(foot.plantedPosition, foot.idealRestPosition);

        if (!foot.isStepping)
        {
            foot.pole.position = foot.target.position + rootTransform.TransformDirection(foot.poleOffset);

            // Gradually move towards ideal position when not stepping
            if (currentMovementState == MovementState.Idle || currentMovementState == MovementState.Stopping)
            {
                foot.currentTargetPos = Vector3.MoveTowards(foot.currentTargetPos, foot.idealRestPosition, Time.deltaTime * footReturnSpeed);
                foot.target.position = foot.currentTargetPos;
            }
        }
    }

    void HandleMovementBasedStepping()
    {
        switch (currentMovementState)
        {
            case MovementState.Idle:
            case MovementState.Stopping:
                CheckForStabilitySteps();
                break;
                
            case MovementState.Walking:
            case MovementState.Running:
                CheckForDynamicSteps();
                break;
                
            case MovementState.Crouching:
                CheckForCrouchSteps();
                break;
                
            case MovementState.Jumping:
                // Don't step while jumping
                break;
        }
    }

    void CheckForStabilitySteps()
    {
        // When idle, check if feet are too far from their ideal positions directly below the body
        Vector3 leftIdealPos = CalculateIdealRestPosition(leftFoot);
        Vector3 rightIdealPos = CalculateIdealRestPosition(rightFoot);
        
        float leftDistanceFromIdeal = Vector3.Distance(leftFoot.plantedPosition, leftIdealPos);
        float rightDistanceFromIdeal = Vector3.Distance(rightFoot.plantedPosition, rightIdealPos);
        
        // Step threshold for returning to center when idle
        float idleStepThreshold = restStabilityRadius * 1.2f;
        
        if (leftDistanceFromIdeal > idleStepThreshold && CanFootStep(leftFoot))
        {
            StartCoroutine(StepToPosition(leftFoot, leftIdealPos));
        }
        else if (rightDistanceFromIdeal > idleStepThreshold && CanFootStep(rightFoot))
        {
            StartCoroutine(StepToPosition(rightFoot, rightIdealPos));
        }
    }

    void CheckForDynamicSteps()
    {
        float bodyMovement = Vector3.Distance(rootTransform.position, lastRootPosition);
        float bodyRotation = Quaternion.Angle(rootTransform.rotation, lastRootRotation);

        if (bodyMovement >= minStepDistance || bodyRotation >= rotationStepThreshold)
        {
            FootData footToStep = DetermineNextFoot();

            if (footToStep != null && CanFootStep(footToStep))
            {
                Vector3 predictedPosition = PredictFootPosition(footToStep);
                float distanceFromPredicted = Vector3.Distance(footToStep.plantedPosition, predictedPosition);

                if (distanceFromPredicted > stepDistance || bodyRotation >= rotationStepThreshold)
                {
                    StartCoroutine(StepToPosition(footToStep, predictedPosition));
                    lastSteppingFoot = footToStep.isLeftFoot ? 0 : 1;
                }
            }

            if (bodyMovement >= stepDistance)
                lastRootPosition = rootTransform.position;
            if (bodyRotation >= rotationStepThreshold)
                lastRootRotation = rootTransform.rotation;
        }
    }

    void CheckForCrouchSteps()
    {
        // Smaller, more careful steps when crouching
        float crouchStepDistance = stepDistance * 0.7f;
        float bodyMovement = Vector3.Distance(rootTransform.position, lastRootPosition);

        if (bodyMovement >= crouchStepDistance)
        {
            FootData footToStep = DetermineNextFoot();
            if (footToStep != null && CanFootStep(footToStep))
            {
                Vector3 targetPos = PredictFootPosition(footToStep, 0.7f); // Shorter prediction for crouching
                StartCoroutine(StepToPosition(footToStep, targetPos));
            }
            lastRootPosition = rootTransform.position;
        }
    }

    Vector3 PredictFootPosition(FootData foot, float predictionMultiplier = 1f)
    {
        Vector3 basePos = rootTransform.position + rootTransform.TransformDirection(foot.offsetFromRoot);

        if (isMoving)
        {
            // Predict where the foot should be based on movement
            Vector3 predictedRootPos = rootTransform.position + (smoothedVelocity * velocityPrediction * predictionMultiplier);
            
            float maxSpeed = beanController != null ? 
                (beanController.maxSpeed * beanController.sprintMultiplier) : 15f;
            float normalizedSpeed = Mathf.Clamp01(movementSpeed / maxSpeed);
            float dynamicStrideLength = strideLength * strideLengthCurve.Evaluate(normalizedSpeed);
            
            Vector3 forwardOffset = movementDirection * dynamicStrideLength * 0.5f;
            Vector3 lateralOffset = rootTransform.right * (foot.isLeftFoot ? -strideWidth : strideWidth) * 0.5f;
            
            basePos = predictedRootPos + rootTransform.TransformDirection(foot.offsetFromRoot) + forwardOffset + lateralOffset;
        }

        return basePos;
    }

    FootData DetermineNextFoot()
    {
        if (!alternateSteps)
        {
            Vector3 leftPredicted = PredictFootPosition(leftFoot);
            Vector3 rightPredicted = PredictFootPosition(rightFoot);

            float leftDistance = Vector3.Distance(leftFoot.plantedPosition, leftPredicted);
            float rightDistance = Vector3.Distance(rightFoot.plantedPosition, rightPredicted);

            return leftDistance > rightDistance ? leftFoot : rightFoot;
        }

        if (lastSteppingFoot == -1)
        {
            return isMoving ? (Vector3.Dot(movementDirection, rootTransform.right) > 0 ? rightFoot : leftFoot) : leftFoot;
        }

        return lastSteppingFoot == 0 ? rightFoot : leftFoot;
    }

    bool CanFootStep(FootData foot)
    {
        return !foot.isStepping && 
               (Time.time - foot.lastStepTime) >= stepOverlapPrevention &&
               !IsBothFeetStepping();
    }

    bool IsBothFeetStepping()
    {
        return leftFoot.isStepping && rightFoot.isStepping;
    }

    IEnumerator StepToPosition(FootData foot, Vector3 targetWorldPos)
    {
        foot.isStepping = true;
        foot.lastStepTime = Time.time;

        Vector3 startPos = foot.currentTargetPos;
        Quaternion startRot = foot.currentTargetRot;

        Vector3 targetPos = targetWorldPos;
        Quaternion targetRot = rootTransform.rotation;

        // Ground the target position
        Vector3 groundNormal = Vector3.up;
        if (Physics.Raycast(targetPos + Vector3.up * foot.raycastHeightOffset, Vector3.down, out RaycastHit hit, foot.raycastDistance, foot.groundLayer))
        {
            targetPos = hit.point;
            groundNormal = hit.normal;

            float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);
            if (slopeAngle > maxGroundAngle)
            {
                Vector3 slopeDirection = Vector3.ProjectOnPlane(groundNormal, Vector3.up).normalized;
                targetPos -= slopeDirection * 0.2f;
            }
        }

        if (alignToGroundNormal)
        {
            Vector3 footForward = Vector3.ProjectOnPlane(rootTransform.forward, groundNormal).normalized;
            targetRot = Quaternion.LookRotation(footForward, groundNormal);
        }

        foot.lastGroundNormal = groundNormal;

        // Adjust step speed based on movement state
        float adjustedStepSpeed = foot.stepSpeed;
        // Don't modify step speed - use the user-defined values directly

        float stepTime = 1f / adjustedStepSpeed;
        float elapsed = 0f;
        float actualStepHeight = CalculateStepHeight(startPos, targetPos, foot);

        while (elapsed < stepTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / stepTime;

        // Use different curves for different movement states
        float curve;
        switch (currentMovementState)
        {
            case MovementState.Running:
                curve = Mathf.Sin(t * Mathf.PI); // Faster, more aggressive curve
                break;
            case MovementState.Crouching:
                curve = Mathf.SmoothStep(0f, 1f, Mathf.Sin(t * Mathf.PI * 0.8f)); // Slower, more careful
                break;
            default:
                curve = Mathf.SmoothStep(0f, 1f, Mathf.Sin(t * Mathf.PI));
                break;
        }

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
            currentPos.y += curve * actualStepHeight;

            Quaternion currentRot = Quaternion.Slerp(startRot, targetRot, t * foot.footRotationSpeed);

            foot.currentTargetPos = currentPos;
            foot.currentTargetRot = currentRot;
            foot.target.position = currentPos;
            foot.target.rotation = currentRot;

            Vector3 midPoint = (startPos + targetPos) * 0.5f;
            Vector3 polePos = midPoint + rootTransform.TransformDirection(foot.poleOffset);
            polePos.y += curve * actualStepHeight * 0.5f;
            foot.pole.position = polePos;

            yield return null;
        }

        foot.currentTargetPos = targetPos;
        foot.currentTargetRot = targetRot;
        foot.target.position = targetPos;
        foot.target.rotation = targetRot;
        foot.pole.position = targetPos + rootTransform.TransformDirection(foot.poleOffset);
        foot.plantedPosition = targetPos;

        PlayFootstepSound(foot);

        foot.isStepping = false;
    }

    float CalculateStepHeight(Vector3 startPos, Vector3 endPos, FootData foot)
    {
        float baseHeight = foot.stepHeight;

        // Adjust step height based on movement state
        switch (currentMovementState)
        {
            case MovementState.Running:
                baseHeight *= 1.2f;
                break;
            case MovementState.Crouching:
                baseHeight *= 0.6f;
                break;
            case MovementState.Stopping:
            case MovementState.Idle:
                baseHeight *= 0.8f;
                break;
        }

        Vector3 direction = (endPos - startPos).normalized;
        float distance = Vector3.Distance(startPos, endPos);

        if (Physics.Raycast(startPos, direction, out RaycastHit hit, distance, foot.groundLayer))
        {
            float obstacleHeight = hit.point.y - Mathf.Min(startPos.y, endPos.y);
            baseHeight = Mathf.Max(baseHeight, obstacleHeight + 0.1f);
        }

        return Mathf.Min(baseHeight, maxStepHeight);
    }

    void PlayFootstepSound(FootData foot)
    {
        if (foot.audioSource == null) return;

        AudioClip[] soundsToUse = null;

        if (foot.isLeftFoot && leftFootSounds != null && leftFootSounds.Length > 0)
            soundsToUse = leftFootSounds;
        else if (!foot.isLeftFoot && rightFootSounds != null && rightFootSounds.Length > 0)
            soundsToUse = rightFootSounds;
        else if (footstepSounds != null && footstepSounds.Length > 0)
            soundsToUse = footstepSounds;

        if (soundsToUse != null)
        {
            AudioClip clip = soundsToUse[Random.Range(0, soundsToUse.Length)];
            
            // Adjust audio based on movement state
            float pitchVariation = 0.2f;
            float volumeMultiplier = 1f;
            
            switch (currentMovementState)
            {
                case MovementState.Running:
                    pitchVariation = 0.3f;
                    volumeMultiplier = 1.2f;
                    break;
                case MovementState.Crouching:
                    pitchVariation = 0.1f;
                    volumeMultiplier = 0.7f;
                    break;
            }
            
            foot.audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            foot.audioSource.volume *= volumeMultiplier;
            foot.audioSource.PlayOneShot(clip);
        }
    }

    void GroundFoot(FootData foot)
    {
        Vector3 rayStart = foot.restPosition + Vector3.up * foot.raycastHeightOffset;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, foot.raycastDistance, foot.groundLayer))
        {
            foot.currentTargetPos = hit.point;
            foot.plantedPosition = hit.point;
            foot.target.position = hit.point;

            if (alignToGroundNormal)
            {
                Vector3 footForward = Vector3.ProjectOnPlane(rootTransform.forward, hit.normal).normalized;
                foot.currentTargetRot = Quaternion.LookRotation(footForward, hit.normal);
                foot.target.rotation = foot.currentTargetRot;
            }

            foot.lastGroundNormal = hit.normal;
        }
    }

    void AdjustRootHeight()
    {
        float leftFootHeight = leftFoot.currentTargetPos.y;
        float rightFootHeight = rightFoot.currentTargetPos.y;
        float averageFootHeight = (leftFootHeight + rightFootHeight) * 0.5f;

        float desiredHeight = averageFootHeight + (originalRootHeight - rootTransform.position.y) + groundClearance;

        float heightDifference = desiredHeight - targetRootHeight;
        heightDifference = Mathf.Clamp(heightDifference, -maxRootAdjustment, maxRootAdjustment);

        targetRootHeight = rootTransform.position.y + heightDifference;

        Vector3 currentRootPos = rootTransform.position;
        currentRootPos.y = Mathf.Lerp(currentRootPos.y, targetRootHeight, Time.deltaTime * rootAdjustmentSpeed);
        rootTransform.position = currentRootPos;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        if (rootTransform != null)
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(rootTransform.position, 0.1f);

            if (showStepRanges)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(rootTransform.position, stepDistance);
                
                // Show stability radius when idle/stopping
                if (currentMovementState == MovementState.Idle || currentMovementState == MovementState.Stopping)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(rootTransform.position, restStabilityRadius);
                }
            }

            if (Application.isPlaying && isMoving)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(rootTransform.position, movementDirection * 0.5f);
                
                if (showPredictedPath)
                {
                    Gizmos.color = Color.magenta;
                    Vector3 predictedPos = rootTransform.position + (smoothedVelocity * velocityPrediction);
                    Gizmos.DrawWireSphere(predictedPos, 0.05f);
                    Gizmos.DrawLine(rootTransform.position, predictedPos);
                }
            }
        }

        DrawFootGizmos(leftFoot, leftFootColor);
        DrawFootGizmos(rightFoot, rightFootColor);
    }

    void DrawFootGizmos(FootData foot, Color footColor)
    {
        if (foot == null || foot.target == null) return;

        Gizmos.color = footColor;
        Gizmos.DrawWireSphere(foot.currentTargetPos, foot.isStepping ? 0.08f : 0.06f);

        if (rootTransform != null)
            Gizmos.DrawLine(rootTransform.position, foot.currentTargetPos);

        if (Application.isPlaying)
        {
            // Show planted position
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(foot.plantedPosition, 0.04f);
            
            // Show ideal rest position
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(foot.idealRestPosition, 0.03f);
            
            // Show predicted position when moving
            if (isMoving)
            {
                Vector3 predictedPos = PredictFootPosition(foot);
                Gizmos.color = footColor * 0.5f;
                Gizmos.DrawWireSphere(predictedPos, 0.05f);
                Gizmos.DrawLine(foot.currentTargetPos, predictedPos);
            }
        }

        if (foot.pole != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(foot.pole.position, 0.05f);
            Gizmos.DrawLine(foot.currentTargetPos, foot.pole.position);
        }

        if (Application.isPlaying && alignToGroundNormal)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawRay(foot.currentTargetPos, foot.lastGroundNormal * 0.3f);
        }

        if (showFootRaycasts)
        {
            Gizmos.color = Color.red;
            Vector3 rayStart = foot.restPosition + Vector3.up * foot.raycastHeightOffset;
            Gizmos.DrawRay(rayStart, Vector3.down * foot.raycastDistance);
        }

        if (showGroundChecks)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(foot.restPosition, 0.03f);
        }
    }

    // ---------------- PUBLIC CONTROL ----------------
    public void ForceStep(bool leftFootStep)
    {
        FootData foot = leftFootStep ? leftFoot : rightFoot;
        if (!foot.isStepping)
        {
            Vector3 targetPos = PredictFootPosition(foot);
            StartCoroutine(StepToPosition(foot, targetPos));
            lastSteppingFoot = leftFootStep ? 0 : 1;
        }
    }

    public void ForceReturnToRest()
    {
        StartCoroutine(ReturnFeetToRestPosition());
    }

    public void SetMovementState(MovementState newState)
    {
        if (newState != currentMovementState)
        {
            previousMovementState = currentMovementState;
            currentMovementState = newState;
            stateChangeTime = Time.time;
            OnMovementStateChanged();
        }
    }

    // Getters for integration with other systems
    public MovementState GetCurrentMovementState() => currentMovementState;
    public bool IsFootStepping(bool leftFoot) => leftFoot ? this.leftFoot.isStepping : rightFoot.isStepping;
    public Vector3 GetFootPosition(bool leftFoot) => leftFoot ? this.leftFoot.currentTargetPos : rightFoot.currentTargetPos;
    public float GetMovementSpeed() => movementSpeed;
    public Vector3 GetMovementDirection() => movementDirection;
    
    // Legacy compatibility methods
    public void SetStepDistance(float newDistance) => stepDistance = newDistance;
    public void SetStrideLength(float newStride) => strideLength = newStride;
    public void SetStepHeight(float newHeight) { leftFoot.stepHeight = newHeight; rightFoot.stepHeight = newHeight; }
    public void SetStepSpeed(float newSpeed) { leftFoot.stepSpeed = newSpeed; rightFoot.stepSpeed = newSpeed; }
    public bool IsWalking() => isMoving && (leftFoot.isStepping || rightFoot.isStepping);
    public bool IsGrounded() => !leftFoot.isStepping || !rightFoot.isStepping;
    
    // Advanced control methods
    public void SetVelocityPrediction(float prediction) => velocityPrediction = prediction;
    public void SetRestStabilityRadius(float radius) => restStabilityRadius = radius;
    public void EnableSmartStepping(bool enable) => enabled = enable;
    
    // Debug information
    public string GetDebugInfo()
    {
        return $"State: {currentMovementState}, Speed: {movementSpeed:F2}, " +
               $"Left Stepping: {leftFoot.isStepping}, Right Stepping: {rightFoot.isStepping}, " +
               $"Left Distance from Ideal: {leftFoot.distanceFromIdeal:F2}, " +
               $"Right Distance from Ideal: {rightFoot.distanceFromIdeal:F2}";
    }
}