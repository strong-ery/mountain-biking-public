using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace rayzngames
{
    public class BicycleVehicle : MonoBehaviour
    {
        // Debug info
        float horizontalInput;
        float verticalInput;
        bool braking;
        Rigidbody rb;

        [Header("PlayerConfig")]
        public PlayerConfigData pcd;
        public BasicSwap basicSwap;
        public Camera cam;
        public GameObject disableObject;
        public PlayerHealthManager phm;

        [Header("Power")]
        [SerializeField] float motorForce;
        public Vector3 COG;

        [Header("Brake Settings")]
        public float brakeForce = 2000f;
        public float brakeSmoothSpeed = 8f; // higher = snappier, lower = softer

        private float currentRearBrake;
        private float currentFrontBrake;

        [Header("Steering")]
        [SerializeField] float maxSteeringAngle;
        [Range(0f, 1f)] [SerializeField] float steerReductorAmmount;
        [Range(0.001f, 1f)] [SerializeField] float turnSmoothing;

        [Header("Lean")]
        [SerializeField] float maxLeanAngle = 45f;
        [Range(0.001f, 1f)] [SerializeField] float leanSmoothing;
        float targetLeanAngle;

        [Header("Object References")]
        public Transform handle;
        [SerializeField] WheelCollider frontWheel;
        [SerializeField] WheelCollider backWheel;
        [SerializeField] Transform frontWheelTransform;
        [SerializeField] Transform backWheelTransform;
        [SerializeField] TrailRenderer frontTrail;
        [SerializeField] TrailRenderer rearTrail;

        [Header("Ground Check")]
        public bool IsGrounded;
        [SerializeField] LayerMask groundLayer;
        [SerializeField] float groundCheckDistance = 0.5f;
        [SerializeField] Transform frontRayOrigin;
        [SerializeField] Transform backRayOrigin;

        [Header("Info")]
        [SerializeField] float currentSteeringAngle;
        [SerializeField] float current_maxSteeringAngle;
        [Range(-45, 45)] public float currentLeanAngle;
        public float currentSpeed;
        public BikeStabilizer bs;

        [Header("Boost Settings")]
        [SerializeField] float maxStamina = 5f;
        [SerializeField] float staminaDrainRate = 1f;
        [SerializeField] float staminaRegenRate = 0.5f;
        [SerializeField] float boostMultiplier = 2f;
        [SerializeField] Image staminaUI;

        [SerializeField] float rearWheelGripMultiplier = 2f;
        [SerializeField] float boostRampSpeed = 2f;
        float currentBoostMultiplier = 1f;

        [Range(-90, 90)]
        [SerializeField] private float boostControlAngleEngageValue;
        [SerializeField] private float boostAngleSensitivity;

        [Range(0, 1)]
        [SerializeField] private float boostAngleExponentStartRatioOf45;

        private float backWheelForwardStiffness;
        private float backWheelSidewaysStiffness;

        [SerializeField] float boostMassMultiplier = 1.5f;
        private float originalMass;

        [Header("Air Control")]
        [SerializeField] private float airRotateSpeed = 90f;
        [SerializeField] private float airControlDelay = 1f;
        [SerializeField] private float angularDamping = 2f;

        private float airborneTimer = 0f;

        private float currentYawSpeed = 0f;
        private float currentRollSpeed = 0f;
        private float currentPitchSpeed = 0f;

        [SerializeField] private float airControlAccel = 5f;   // How fast it reaches target speed
        [SerializeField] private float airControlDecay = 1.5f; // How fast it slows down when no input

        // Air control state tracking
        [Header("Air Control State")]
        public bool isAirControl = false; // True when air control is active and receiving input

        float currentStamina;
        bool isBoosting;

        public bool IsAirborne => !IsGrounded;

        // ---------------- Restored Jerk Detection (Original System) ----------------
        [Header("Impact Detection")]
        [SerializeField] private float jerkThreshold = 50f; // Damage threshold
        [SerializeField] private float ragdollJerkThreshold = 100f; // Ragdoll threshold
        [SerializeField] private float impactTimeout = 0.5f; // Invincibility frames duration
        private float impactCooldownTimer = 0f; // Timer for i-frames

        // Tracking variables for jerk calculation
        private Vector3 lastVelocity;
        private Vector3 lastAcceleration;

        // ---------------- Ragdoll System ----------------
        [Header("Ragdoll Settings")]
        [SerializeField] private float ragdollCooldown = 1f; // seconds to wait before detecting again
        private bool isRagdoll = false;
        private float ragdollTimer = 0f;
        private float ragdollIgnoreTimer = 0f;
        [SerializeField] private TriggerPublicity tsf;
        [SerializeField] private TriggerPublicity tsb;

        // Surface normal caching
        [Header("Surface Normal Caching")]
        private Vector3 frontWheelLastNormal = Vector3.up;
        private Vector3 backWheelLastNormal = Vector3.up;
        [SerializeField] float ragdollForceMultiplier = 50f;

        // Visuals
        [Header("Visuals")]
        [SerializeField] private Transform pedals;
        [SerializeField] private float pedalSpinSpeed = 360f; // degrees per 1 m/s of bike speed
        private float currentPedalRotation = 0f;

        void Start()
        {
            frontWheel.ConfigureVehicleSubsteps(5, 12, 15);
            backWheel.ConfigureVehicleSubsteps(5, 12, 15);
            rb = GetComponent<Rigidbody>();
            currentStamina = maxStamina;

            backWheelForwardStiffness = backWheel.forwardFriction.stiffness;
            backWheelSidewaysStiffness = backWheel.sidewaysFriction.stiffness;
            originalMass = rb.mass;

            // Initialize for jerk detection
            lastVelocity = rb.linearVelocity;
            lastAcceleration = Vector3.zero;
        }

        void Update()
        {
            GetInput();
            HandleBoost();
        }

        private void FixedUpdate()
        {
            CheckForImpact(); // Restored original jerk detection
            if (isRagdoll) return;

            GroundCheck();
            HandleEngine();
            HandleBrakes();

            if (IsGrounded)
            {
                airborneTimer = 0f;
                isAirControl = false; // Reset air control when grounded
                HandleSteering();
                LeanOnTurn();
                UpdateHandles();
            }
            else
            {
                airborneTimer += Time.fixedDeltaTime;
                if (airborneTimer >= airControlDelay)
                {
                    HandleSteering();
                    UpdateHandles();
                    AirControl();
                }
                else
                {
                    isAirControl = false; // Not yet in air control phase
                }
            }

            UpdateWheels();
            Speed_O_Meter();

            if (IsGrounded)
            {
                SpinPedals();
            }
        }

        // Replace your CheckForImpact method with this simplified version
        private void CheckForImpact()
        {
            if (isRagdoll) return;

            // Immediate trigger collision check (highest priority)
            if (tsf.intersecting || tsb.intersecting)
            {
                TriggerRagdoll(25f);
                return;
            }

            // Countdown the impact cooldown timer
            if (impactCooldownTimer > 0f)
            {
                impactCooldownTimer -= Time.fixedDeltaTime;
                // Update tracking variables even during cooldown to prevent false positives
                lastVelocity = rb.linearVelocity;
                lastAcceleration = Vector3.zero; // Reset acceleration during cooldown
                return; // Skip all jerk detection during i-frames
            }

            // Countdown the ragdoll ignore timer
            if (ragdollIgnoreTimer > 0f)
            {
                ragdollIgnoreTimer -= Time.fixedDeltaTime;
                lastVelocity = rb.linearVelocity;
                lastAcceleration = Vector3.zero; // Reset acceleration during ragdoll ignore
                return;
            }

            // Calculate jerk
            Vector3 currentVelocity = rb.linearVelocity;
            Vector3 currentAcceleration = (currentVelocity - lastVelocity) / Time.fixedDeltaTime;
            float jerkMagnitude = (currentAcceleration - lastAcceleration).magnitude / Time.fixedDeltaTime;

            // Process impact if jerk threshold is reached
            if (jerkMagnitude >= jerkThreshold)
            {
                // START COOLDOWN IMMEDIATELY to prevent multiple detections of the same impact
                impactCooldownTimer = impactTimeout;

                // Apply damage
                if (phm != null)
                {
                    float baseDamage = (jerkMagnitude - jerkThreshold) * 0.002f;
                    float damageVariation = UnityEngine.Random.Range(0.8f, 1.2f);
                    float finalDamage = Mathf.Clamp(baseDamage * damageVariation, 1f, 100f);
                    phm.TakeDamage(finalDamage);
                    Debug.Log($"Applied {finalDamage:F1} damage from jerk impact of {jerkMagnitude:F2}");
                }

                // Trigger ragdoll if threshold met
                if (jerkMagnitude >= ragdollJerkThreshold)
                {
                    float scaledRagdollForce = ragdollForceMultiplier * (jerkMagnitude / ragdollJerkThreshold);
                    TriggerRagdoll(scaledRagdollForce);
                }
            }

            // Always update tracking variables at the end
            lastVelocity = currentVelocity;
            lastAcceleration = currentAcceleration;
        }

        private void OnImpactDetected(float jerkForce, bool shouldRagdoll)
        {
            Debug.Log($"IMPACT DETECTED! Jerk Force: {jerkForce:F2}, Should Ragdoll: {shouldRagdoll}");

            // Apply damage
            if (jerkForce >= jerkThreshold && phm != null)
            {
                // Calculate damage based on jerk force (using original formula)
                float baseDamage = (jerkForce - jerkThreshold) * 0.002f;

                // Optional: Add some randomization to make impacts feel more varied
                float damageVariation = UnityEngine.Random.Range(0.8f, 1.2f);
                float finalDamage = baseDamage * damageVariation;

                // Clamp damage to reasonable values
                finalDamage = Mathf.Clamp(finalDamage, 1f, 100f);

                // Apply damage to player
                phm.TakeDamage(finalDamage);

                Debug.Log($"Applied {finalDamage:F1} damage from jerk impact of {jerkForce:F2}");
            }

            // Trigger ragdoll if threshold met
            if (shouldRagdoll)
            {
                // Scale ragdoll force based on impact
                float scaledRagdollForce = ragdollForceMultiplier * (jerkForce / ragdollJerkThreshold);
                TriggerRagdoll(scaledRagdollForce);
            }

            // Add any other impact effects here (screen shake, sound, etc.)
        }

        // Add these methods to handle script enabling/disabling properly
        private void OnEnable()
        {
            // Reset tracking variables when script is re-enabled to prevent false impacts
            if (rb != null)
            {
                lastVelocity = rb.linearVelocity;
                lastAcceleration = Vector3.zero;
                impactCooldownTimer = 0f; // Clear any existing cooldown
                ragdollIgnoreTimer = ragdollCooldown; // Set ignore timer to prevent immediate re-detection
            }
        }

        private void OnDisable()
        {
            // Clear tracking variables when disabled
            lastVelocity = Vector3.zero;
            lastAcceleration = Vector3.zero;
        }

        // Modified TriggerRagdoll method to set ignore timer
        private void TriggerRagdoll(float rdfm)
        {
            basicSwap.ragdolled = true;
            ragdollIgnoreTimer = ragdollCooldown; // Prevent immediate re-detection when getting back on
        }

        private void HandleBrakes()
        {
            // Target brake values
            float targetRear = braking ? brakeForce : 0f;
            float targetFront = Input.GetKey(KeyCode.R) ? brakeForce : 0f;

            // Smoothly interpolate
            currentRearBrake = Mathf.Lerp(currentRearBrake, targetRear, brakeSmoothSpeed * Time.fixedDeltaTime);
            currentFrontBrake = Mathf.Lerp(currentFrontBrake, targetFront, brakeSmoothSpeed * Time.fixedDeltaTime);

            // Apply
            ApplyRearBraking(currentRearBrake);
            ApplyFrontBraking(currentFrontBrake);
        }

        private void SpinPedals()
        {
            if (pedals == null) return;

            // Spin proportional to speed
            float spinAmount = currentSpeed * pedalSpinSpeed * Time.fixedDeltaTime;
            currentPedalRotation += spinAmount;

            // Keep it from growing too large
            if (currentPedalRotation > 360f) currentPedalRotation -= 360f;

            // Apply rotation on local X-axis
            pedals.localRotation = Quaternion.Euler(currentPedalRotation, 0f, 0f);
        }

        private void GetInput()
        {
            horizontalInput = Input.GetAxis("Horizontal");
            verticalInput = Input.GetAxis("Vertical"); // Keep full range for air control
            braking = Input.GetKey(KeyCode.S); // S key for braking
        }

        private void HandleEngine()
        {
            // Only use positive vertical input for ground movement (no reverse)
            float groundVerticalInput = Mathf.Max(0f, verticalInput);
            float torque = braking ? 0f : groundVerticalInput * motorForce;
            torque *= currentBoostMultiplier;
            backWheel.motorTorque = torque;
        }

        public void ApplyFrontBraking(float brakeForce)
        {
            frontWheel.brakeTorque = brakeForce;
        }

        public void ApplyRearBraking(float brakeForce)
        {
            backWheel.brakeTorque = brakeForce;
        }

        void MaxSteeringReductor()
        {
            float t = (rb.linearVelocity.magnitude / 30f) * steerReductorAmmount;
            t = Mathf.Clamp01(t);
            current_maxSteeringAngle = Mathf.Lerp(maxSteeringAngle, 5f, t);
        }

        public void HandleSteering()
        {
            MaxSteeringReductor();
            currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, current_maxSteeringAngle * horizontalInput, turnSmoothing * 0.1f);
            frontWheel.steerAngle = currentSteeringAngle;
            targetLeanAngle = maxLeanAngle * -horizontalInput;
        }

        public void UpdateHandles()
        {
            handle.localEulerAngles = new Vector3(handle.localEulerAngles.x, currentSteeringAngle, handle.localEulerAngles.z);
        }

        private void LeanOnTurn()
        {
            Vector3 currentRot = transform.rotation.eulerAngles;
            float speed = rb.linearVelocity.magnitude;

            if (speed >= 1f)
            {
                if (Mathf.Abs(currentSteeringAngle) < 0.5f)
                    currentLeanAngle = Mathf.LerpAngle(currentLeanAngle, 0f, leanSmoothing * 0.1f);
                else
                    currentLeanAngle = Mathf.LerpAngle(currentLeanAngle, targetLeanAngle, leanSmoothing * 0.1f);
            }
            else
            {
                currentLeanAngle = Mathf.LerpAngle(currentLeanAngle, targetLeanAngle, leanSmoothing * 0.02f);
            }

            rb.centerOfMass = new Vector3(rb.centerOfMass.x, COG.y, rb.centerOfMass.z);
            transform.rotation = Quaternion.Euler(currentRot.x, currentRot.y, currentLeanAngle);
        }

        private void AirControl()
        {
            float yawInput = horizontalInput;
            float pitchInput = verticalInput;

            // Check if there's any meaningful input
            bool hasInput = Mathf.Abs(yawInput) > 0.01f || Mathf.Abs(pitchInput) > 0.01f;
            
            // Update isAirControl - true only when airborne, past delay, and has input
            isAirControl = hasInput;

            // Angular damping to calm rigidbody rotation while in air control
            Vector3 localAngVel = transform.InverseTransformDirection(rb.angularVelocity);
            localAngVel.x = Mathf.Lerp(localAngVel.x, 0f, angularDamping * Time.fixedDeltaTime);
            localAngVel.y = Mathf.Lerp(localAngVel.y, 0f, angularDamping * Time.fixedDeltaTime);
            rb.angularVelocity = transform.TransformDirection(localAngVel);

            // Smooth acceleration toward input target
            if (Mathf.Abs(yawInput) > 0.01f)
            {
                currentYawSpeed = Mathf.MoveTowards(currentYawSpeed, yawInput * airRotateSpeed, airControlAccel * Time.fixedDeltaTime);
                currentRollSpeed = Mathf.MoveTowards(currentRollSpeed, -yawInput * airRotateSpeed * 0.5f, airControlAccel * Time.fixedDeltaTime);
            }
            else
            {
                // Decay speed when no input
                currentYawSpeed = Mathf.MoveTowards(currentYawSpeed, 0f, airControlDecay * Time.fixedDeltaTime);
                currentRollSpeed = Mathf.MoveTowards(currentRollSpeed, 0f, airControlDecay * Time.fixedDeltaTime);
            }

            if (Mathf.Abs(pitchInput) > 0.01f)
            {
                currentPitchSpeed = Mathf.MoveTowards(currentPitchSpeed, pitchInput * airRotateSpeed, airControlAccel * Time.fixedDeltaTime);
            }
            else
            {
                currentPitchSpeed = Mathf.MoveTowards(currentPitchSpeed, 0f, airControlDecay * Time.fixedDeltaTime);
            }

            // Apply smoothed rotation
            transform.Rotate(currentPitchSpeed * Time.fixedDeltaTime, currentYawSpeed * Time.fixedDeltaTime, currentRollSpeed * Time.fixedDeltaTime, Space.Self);
        }

        public void UpdateWheels()
        {
            UpdateSingleWheel(frontWheel, frontWheelTransform);
            UpdateSingleWheel(backWheel, backWheelTransform);
        }

        private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
        {
            Vector3 position;
            Quaternion rotation;
            wheelCollider.GetWorldPose(out position, out rotation);
            wheelTransform.rotation = rotation;
            wheelTransform.position = position;
        }

        void Speed_O_Meter()
        {
            currentSpeed = rb.linearVelocity.magnitude;
        }

        private void GroundCheck()
        {
            bool frontGrounded = false;
            bool backGrounded = false;

            if (frontRayOrigin)
            {
                RaycastHit hit;
                frontGrounded = Physics.Raycast(frontRayOrigin.position, Vector3.down, out hit, groundCheckDistance, groundLayer);
                if (frontGrounded)
                {
                    frontWheelLastNormal = hit.normal;
                }
            }

            if (backRayOrigin)
            {
                RaycastHit hit;
                backGrounded = Physics.Raycast(backRayOrigin.position, Vector3.down, out hit, groundCheckDistance, groundLayer);
                if (backGrounded)
                {
                    backWheelLastNormal = hit.normal;
                }
            }

            IsGrounded = frontGrounded || backGrounded;
        }

        private void HandleBoost()
        {
            // Use the same ground input logic for boost detection
            float groundVerticalInput = Mathf.Max(0f, verticalInput);
            isBoosting = groundVerticalInput > 0f && IsGrounded && Input.GetKey(KeyCode.LeftShift) && currentStamina > 0f;

            if (isBoosting)
            {
                currentStamina -= staminaDrainRate * Time.deltaTime;
                currentStamina = Mathf.Max(currentStamina, 0f);
            }
            else
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                currentStamina = Mathf.Min(currentStamina, maxStamina);
            }

            if (staminaUI)
                staminaUI.fillAmount = currentStamina / maxStamina;

            float targetMultiplier = isBoosting ? boostMultiplier : 1f;
            currentBoostMultiplier = Mathf.Lerp(currentBoostMultiplier, targetMultiplier, boostRampSpeed * Time.deltaTime);

            if (backWheel != null)
            {
                WheelFrictionCurve fFriction = backWheel.forwardFriction;
                WheelFrictionCurve sFriction = backWheel.sidewaysFriction;

                if (isBoosting)
                {
                    fFriction.stiffness = backWheelForwardStiffness * rearWheelGripMultiplier;
                    sFriction.stiffness = backWheelSidewaysStiffness * rearWheelGripMultiplier;
                }
                else
                {
                    fFriction.stiffness = Mathf.Lerp(fFriction.stiffness, backWheelForwardStiffness, 5f * Time.deltaTime);
                    sFriction.stiffness = Mathf.Lerp(sFriction.stiffness, backWheelSidewaysStiffness, 5f * Time.deltaTime);
                }

                backWheel.forwardFriction = fFriction;
                backWheel.sidewaysFriction = sFriction;
            }

            if (IsGrounded && isBoosting)
            {
                rb.mass = originalMass * boostMassMultiplier;

                float pitchAngle = transform.eulerAngles.x;
                if (pitchAngle > 180f) pitchAngle -= 360f;

                // Apply the engagement offset FIRST
                float adjustedPitchAngle = pitchAngle - boostControlAngleEngageValue;

                float sensitivity = boostAngleSensitivity;
                float exponentStart = boostAngleExponentStartRatioOf45;

                // Use the adjusted pitch angle for the factor calculation
                float angleFactor = adjustedPitchAngle / 45f;

                // Rest of your code stays the same...
                float absFactor = Mathf.Abs(angleFactor);
                if (absFactor > exponentStart)
                {
                    float overThreshold = (absFactor - exponentStart) / (1f - exponentStart);
                    overThreshold = Mathf.Pow(overThreshold, sensitivity);
                    absFactor = exponentStart + overThreshold * (1f - exponentStart);
                }
                angleFactor = Mathf.Sign(angleFactor) * absFactor;
                angleFactor = Mathf.Clamp(angleFactor, -1f, 1f);

                // Shift magnitudes (positive values)
                float forwardShiftMag = 0.25f;   // forward shift magnitude
                float backwardShiftMag = 1f;  // backward shift magnitude
                float verticalShift = 0.1f;
                float forwardShift;

                forwardShift = -angleFactor * (angleFactor > 0 ? backwardShiftMag : forwardShiftMag);

                float verticalAdjust = verticalShift * angleFactor;

                Vector3 forwardDirection = transform.InverseTransformDirection(transform.forward);
                Vector3 adjustedCOM = COG + forwardDirection * forwardShift;
                adjustedCOM.y += verticalAdjust;

                rb.centerOfMass = adjustedCOM;

                // Debug logs
                Debug.Log($"[Bike Balance] Pitch Angle: {pitchAngle:F2}°, AngleFactor: {angleFactor:F2}, COM: {rb.centerOfMass}");
            }
            else
            {
                rb.mass = Mathf.Lerp(rb.mass, originalMass, 5f * Time.deltaTime);
                rb.centerOfMass = Vector3.Lerp(rb.centerOfMass, COG, 5f * Time.deltaTime);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (frontRayOrigin)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(frontRayOrigin.position, frontRayOrigin.position + Vector3.down * groundCheckDistance);
            }
            if (backRayOrigin)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(backRayOrigin.position, backRayOrigin.position + Vector3.down * groundCheckDistance);
            }

            // COM Adjustment Visualization
            if (Application.isPlaying && rb != null)
            {
                // Original COM position (world space)
                Vector3 originalCOMWorld = transform.TransformPoint(COG);

                // Current COM position (world space) 
                Vector3 currentCOMWorld = transform.TransformPoint(rb.centerOfMass);

                // Draw original COM as yellow sphere
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(originalCOMWorld, 0.1f);

                // Draw current COM as red sphere
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(currentCOMWorld, 0.15f);

                // Draw adjustment vector from original to current
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(originalCOMWorld, currentCOMWorld);

                // Draw arrow head for direction
                Vector3 direction = (currentCOMWorld - originalCOMWorld).normalized;
                Vector3 right = Vector3.Cross(direction, Vector3.up).normalized * 0.1f;
                Vector3 arrowTip = currentCOMWorld;
                Vector3 arrowBase = currentCOMWorld - direction * 0.2f;

                Gizmos.DrawLine(arrowTip, arrowBase + right);
                Gizmos.DrawLine(arrowTip, arrowBase - right);

                // Optional: Draw labels in Scene view (only visible when selected)
#if UNITY_EDITOR
                UnityEditor.Handles.Label(originalCOMWorld + Vector3.up * 0.2f, "Original COM");
                UnityEditor.Handles.Label(currentCOMWorld + Vector3.up * 0.2f, "Adjusted COM");
#endif
            }
        }
    }
}