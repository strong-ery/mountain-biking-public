using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using Unity.VisualScripting;
using UnityEngine.SceneManagement;

namespace rayzngames
{
    public class DetachedBicycleVehicle : MonoBehaviour
    {
		public bool grabbed;
        // Debug info
		float horizontalInput;
        float verticalInput;
        bool braking;
        Rigidbody rb;

        [Header("Power")]
        [SerializeField] float motorForce;
        [SerializeField] float reverseForceMultiplier = 0.5f; // Reverse is typically slower
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
        [SerializeField] float maxLeanAngle = 180f;
        [Range(0.001f, 1f)] [SerializeField] float leanSmoothing;
        float targetLeanAngle;

        [Header("Object References")]
        public Transform handle;
        [SerializeField] WheelCollider frontWheel;
        [SerializeField] WheelCollider backWheel;
        [SerializeField] Transform frontWheelTransform;
        [SerializeField] Transform backWheelTransform;

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
        public bool isReversing; // New field to track reverse state
		public BikeStabilizer bs;

		// Add these new fields to your class:
		[Header("Surface Normal Caching")]
		private Vector3 frontWheelLastNormal = Vector3.up;
		private Vector3 backWheelLastNormal = Vector3.up;

		// Tracking variables
		private Vector3 lastAngularVelocity;
		private Vector3 lastAcceleration;
		private float lastCollisionForce;
		private Vector3[] velocityHistory = new Vector3[5]; // 5-frame rolling window
		private int velocityHistoryIndex = 0;

		void Start()
		{
			frontWheel.ConfigureVehicleSubsteps(5, 12, 15);
			backWheel.ConfigureVehicleSubsteps(5, 12, 15);
			rb = GetComponent<Rigidbody>();
		}

		void Update()
		{
			if (!grabbed)
			{
				return;
			}

			GetInput();
		}

		private void FixedUpdate()
		{
			GroundCheck();
			HandleEngine();
			HandleBrakes();

			if (IsGrounded)
			{
				HandleSteering();
				LeanOnTurn();
				UpdateHandles();
			}

			UpdateWheels();
			Speed_O_Meter();
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

		private void GetInput()
		{
			horizontalInput = Input.GetAxis("Horizontal");
			verticalInput = Input.GetAxis("Vertical"); // Now uses full range for forward/reverse
			braking = Input.GetKey(KeyCode.Space); // Space bar for braking (changed from S)
		}

		private void HandleEngine()
		{
			// Check if we're trying to reverse or go forward
			isReversing = verticalInput < 0f;
			
			float torque;
			if (braking)
			{
				torque = 0f;
			}
			else if (isReversing)
			{
				// Apply reverse torque with reduced force
				torque = verticalInput * motorForce * reverseForceMultiplier;
			}
			else
			{
				// Forward torque
				torque = verticalInput * motorForce;
			}
			
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
            
            // Optional: Reverse steering when going backwards
            float steerDirection = isReversing ? -horizontalInput : horizontalInput;
            currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, current_maxSteeringAngle * steerDirection, turnSmoothing * 0.1f);
            
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
    }
}