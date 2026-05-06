using UnityEngine;

public class RigidBodyAttractor : MonoBehaviour
{
    [Header("Target")]
    public Transform targetTransform;
    public Vector3 targetPosition;
    public Quaternion targetRotation = Quaternion.identity;
    
    [Header("Rigidbody")]
    public Rigidbody targetRigidbody;
    
    [Header("Attraction Forces")]
    public float attractionForce = 2000f;
    public float rotationTorque = 1000f;
    public float velocityMatchingForce = 5000f;
    public float angularVelocityMatchingTorque = 2000f;
    
    [Header("Limits")]
    public float maxVelocity = 20f;
    public float maxAngularVelocity = 720f; // degrees per second
    public bool limitVelocities = true;
    
    [Header("Predictive Tracking")]
    public bool usePredictiveForces = true;
    public float positionPredictionTime = 0.15f;
    public float rotationPredictionTime = 0.1f;
    public int velocitySmoothing = 3; // frames to average over
    
    [Header("Damping")]
    public float positionDamping = 0.85f;
    public float rotationDamping = 0.9f;
    public bool useProportionalDamping = true;
    
    [Header("Distance Scaling")]
    public bool useDistanceBasedForce = true;
    public AnimationCurve forceDistanceCurve = AnimationCurve.EaseInOut(0f, 2f, 5f, 0.5f);
    public float maxForceDistance = 10f;
    
    [Header("Spring System (Alternative)")]
    public bool useSpringSystem = false;
    public float springStrength = 100f;
    public float springDamper = 10f;
    
    [Header("Settings")]
    public bool useTargetTransform = true;
    public bool attractPosition = true;
    public bool attractRotation = true;
    public bool showDebugGizmos = true;
    public bool showVelocityGizmos = true;

    // Tracking variables
    private Vector3 previousTargetPosition;
    private Quaternion previousTargetRotation;
    private Vector3 targetVelocity;
    private Vector3 targetAngularVelocity;
    
    // Smoothing buffers
    private Vector3[] velocityBuffer;
    private Vector3[] angularVelocityBuffer;
    private int bufferIndex = 0;
    
    // Debug info
    private Vector3 lastAppliedForce;
    private Vector3 lastAppliedTorque;
    private float currentDistance;
    private float currentAngle;

    private void Start()
    {
        // Setup rigidbody
        if (targetRigidbody == null)
            targetRigidbody = GetComponent<Rigidbody>();
        
        if (targetRigidbody == null)
        {
            Debug.LogError("RigidBodyAttractor: No Rigidbody assigned or found!");
            return;
        }
        
        // Configure rigidbody for physics
        targetRigidbody.isKinematic = false;
        targetRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        
        // Initialize tracking
        InitializeTracking();
        
        // Setup smoothing buffers
        velocityBuffer = new Vector3[velocitySmoothing];
        angularVelocityBuffer = new Vector3[velocitySmoothing];
    }

    private void InitializeTracking()
    {
        if (useTargetTransform && targetTransform != null)
        {
            previousTargetPosition = targetTransform.position;
            previousTargetRotation = targetTransform.rotation;
            targetPosition = targetTransform.position;
            targetRotation = targetTransform.rotation;
        }
        else
        {
            previousTargetPosition = targetPosition;
            previousTargetRotation = targetRotation;
        }
    }

    private void Update()
    {
        if (targetRigidbody == null) return;

        // Update targets
        UpdateTargets();
        
        // Calculate target motion
        CalculateTargetMotion();
        
        // Update debug info
        UpdateDebugInfo();
    }

    private void UpdateTargets()
    {
        if (useTargetTransform && targetTransform != null)
        {
            targetPosition = targetTransform.position;
            targetRotation = targetTransform.rotation;
        }
    }

    private void CalculateTargetMotion()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0) return;

        // Calculate raw velocities
        Vector3 rawVelocity = (targetPosition - previousTargetPosition) / deltaTime;
        Vector3 rawAngularVelocity = CalculateAngularVelocity(previousTargetRotation, targetRotation, deltaTime);
        
        // Add to smoothing buffers
        velocityBuffer[bufferIndex] = rawVelocity;
        angularVelocityBuffer[bufferIndex] = rawAngularVelocity;
        bufferIndex = (bufferIndex + 1) % velocitySmoothing;
        
        // Calculate smoothed velocities
        targetVelocity = Vector3.zero;
        targetAngularVelocity = Vector3.zero;
        
        for (int i = 0; i < velocitySmoothing; i++)
        {
            targetVelocity += velocityBuffer[i];
            targetAngularVelocity += angularVelocityBuffer[i];
        }
        
        targetVelocity /= velocitySmoothing;
        targetAngularVelocity /= velocitySmoothing;
        
        // Store for next frame
        previousTargetPosition = targetPosition;
        previousTargetRotation = targetRotation;
    }

    private Vector3 CalculateAngularVelocity(Quaternion from, Quaternion to, float deltaTime)
    {
        Quaternion deltaRotation = to * Quaternion.Inverse(from);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
        
        if (angle > 180f) angle -= 360f;
        
        return axis * (angle * Mathf.Deg2Rad) / deltaTime;
    }

    private void UpdateDebugInfo()
    {
        if (targetRigidbody != null)
        {
            currentDistance = Vector3.Distance(targetRigidbody.position, targetPosition);
            currentAngle = Quaternion.Angle(targetRigidbody.rotation, targetRotation);
        }
    }

    private void FixedUpdate()
    {
        if (targetRigidbody == null) return;

        // Reset debug forces
        lastAppliedForce = Vector3.zero;
        lastAppliedTorque = Vector3.zero;

        // Apply forces
        if (useSpringSystem)
        {
            ApplySpringForces();
        }
        else
        {
            ApplyAttractionForces();
        }
        
        // Limit velocities if enabled
        if (limitVelocities)
        {
            LimitVelocities();
        }
    }

    private void ApplyAttractionForces()
    {
        if (attractPosition)
        {
            ApplyPositionForces();
        }

        if (attractRotation)
        {
            ApplyRotationForces();
        }
    }

    private void ApplyPositionForces()
    {
        Vector3 currentPos = targetRigidbody.position;
        Vector3 targetPos = targetPosition;
        
        // Apply prediction if enabled
        if (usePredictiveForces && targetVelocity.magnitude > 0.1f)
        {
            targetPos += targetVelocity * positionPredictionTime;
        }
        
        Vector3 displacement = targetPos - currentPos;
        float distance = displacement.magnitude;
        
        if (distance < 0.001f) return;
        
        Vector3 direction = displacement.normalized;
        float force = attractionForce;
        
        // Distance-based scaling
        if (useDistanceBasedForce)
        {
            float normalizedDistance = Mathf.Clamp01(distance / maxForceDistance);
            force *= forceDistanceCurve.Evaluate(normalizedDistance);
        }
        
        // Primary attraction force
        Vector3 attractionForceVec = direction * force;
        
        // Velocity matching
        if (usePredictiveForces)
        {
            Vector3 velocityError = targetVelocity - targetRigidbody.linearVelocity;
            Vector3 velocityForce = velocityError * velocityMatchingForce;
            attractionForceVec += velocityForce;
        }
        
        // Damping
        Vector3 dampingForce = Vector3.zero;
        if (useProportionalDamping)
        {
            // Proportional to velocity difference
            Vector3 relativeVelocity = targetRigidbody.linearVelocity - targetVelocity;
            dampingForce = -relativeVelocity * positionDamping * targetRigidbody.mass;
        }
        else
        {
            // Simple velocity damping
            dampingForce = -targetRigidbody.linearVelocity * positionDamping;
        }
        
        Vector3 totalForce = attractionForceVec + dampingForce;
        targetRigidbody.AddForce(totalForce, ForceMode.Force);
        
        lastAppliedForce = totalForce;
    }

    private void ApplyRotationForces()
    {
        Quaternion currentRot = targetRigidbody.rotation;
        Quaternion targetRot = targetRotation;
        
        // Apply prediction if enabled
        if (usePredictiveForces && targetAngularVelocity.magnitude > 0.1f)
        {
            Quaternion predictedRotation = Quaternion.AngleAxis(
                targetAngularVelocity.magnitude * Mathf.Rad2Deg * rotationPredictionTime,
                targetAngularVelocity.normalized
            );
            targetRot = predictedRotation * targetRot;
        }
        
        // Calculate rotation difference
        Quaternion rotDiff = targetRot * Quaternion.Inverse(currentRot);
        rotDiff.ToAngleAxis(out float angle, out Vector3 axis);
        
        if (angle > 180f) angle -= 360f;
        if (Mathf.Abs(angle) < 0.1f || axis == Vector3.zero) return;
        
        // Convert to torque
        float torque = rotationTorque * (angle / 180f); // Normalize by 180 degrees
        Vector3 torqueVec = axis * torque;
        
        // Angular velocity matching
        if (usePredictiveForces)
        {
            Vector3 angularVelocityError = targetAngularVelocity - targetRigidbody.angularVelocity;
            Vector3 angularMatchingTorque = angularVelocityError * angularVelocityMatchingTorque;
            torqueVec += angularMatchingTorque;
        }
        
        // Angular damping
        Vector3 dampingTorque = Vector3.zero;
        if (useProportionalDamping)
        {
            Vector3 relativeAngularVel = targetRigidbody.angularVelocity - targetAngularVelocity;
            dampingTorque = -relativeAngularVel * rotationDamping;
        }
        else
        {
            dampingTorque = -targetRigidbody.angularVelocity * rotationDamping;
        }
        
        Vector3 totalTorque = torqueVec + dampingTorque;
        targetRigidbody.AddTorque(totalTorque, ForceMode.Force);
        
        lastAppliedTorque = totalTorque;
    }

    private void ApplySpringForces()
    {
        if (attractPosition)
        {
            Vector3 displacement = targetPosition - targetRigidbody.position;
            Vector3 springForce = displacement * springStrength;
            Vector3 dampingForce = -targetRigidbody.linearVelocity * springDamper;
            Vector3 totalForce = springForce + dampingForce;
            
            targetRigidbody.AddForce(totalForce, ForceMode.Force);
            lastAppliedForce = totalForce;
        }
        
        if (attractRotation)
        {
            Quaternion rotDiff = targetRotation * Quaternion.Inverse(targetRigidbody.rotation);
            rotDiff.ToAngleAxis(out float angle, out Vector3 axis);
            
            if (angle > 180f) angle -= 360f;
            
            if (Mathf.Abs(angle) > 0.1f && axis != Vector3.zero)
            {
                Vector3 springTorque = axis * (angle * Mathf.Deg2Rad * springStrength);
                Vector3 dampingTorque = -targetRigidbody.angularVelocity * springDamper;
                Vector3 totalTorque = springTorque + dampingTorque;
                
                targetRigidbody.AddTorque(totalTorque, ForceMode.Force);
                lastAppliedTorque = totalTorque;
            }
        }
    }

    private void LimitVelocities()
    {
        // Limit linear velocity
        if (targetRigidbody.linearVelocity.magnitude > maxVelocity)
        {
            targetRigidbody.linearVelocity = targetRigidbody.linearVelocity.normalized * maxVelocity;
        }
        
        // Limit angular velocity
        float angularSpeed = targetRigidbody.angularVelocity.magnitude * Mathf.Rad2Deg;
        if (angularSpeed > maxAngularVelocity)
        {
            float maxAngularSpeedRad = maxAngularVelocity * Mathf.Deg2Rad;
            targetRigidbody.angularVelocity = targetRigidbody.angularVelocity.normalized * maxAngularSpeedRad;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        if (targetRigidbody == null) return;
        
        Vector3 currentTargetPos = useTargetTransform && targetTransform != null ? 
            targetTransform.position : targetPosition;
        
        Quaternion currentTargetRot = useTargetTransform && targetTransform != null ? 
            targetTransform.rotation : targetRotation;
        
        // Target position and orientation
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(currentTargetPos, 0.15f);
        
        // Target orientation axes
        float axisLength = 0.5f;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(currentTargetPos, currentTargetRot * Vector3.right * axisLength);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(currentTargetPos, currentTargetRot * Vector3.up * axisLength);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(currentTargetPos, currentTargetRot * Vector3.forward * axisLength);
        
        // Connection line
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(targetRigidbody.position, currentTargetPos);
        
        // Current rigidbody position
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(targetRigidbody.position, 0.1f);
        
        if (showVelocityGizmos)
        {
            // Current velocity
            Gizmos.color = Color.cyan;
            Vector3 velEnd = targetRigidbody.position + targetRigidbody.linearVelocity;
            Gizmos.DrawLine(targetRigidbody.position, velEnd);
            Gizmos.DrawWireCube(velEnd, Vector3.one * 0.05f);
            
            // Target velocity
            Gizmos.color = Color.magenta;
            Vector3 targetVelEnd = currentTargetPos + targetVelocity;
            Gizmos.DrawLine(currentTargetPos, targetVelEnd);
            Gizmos.DrawWireCube(targetVelEnd, Vector3.one * 0.05f);
            
            // Applied force visualization
            if (lastAppliedForce.magnitude > 0.1f)
            {
                Gizmos.color = Color.red;
                Vector3 forceEnd = targetRigidbody.position + lastAppliedForce.normalized * 0.3f;
                Gizmos.DrawLine(targetRigidbody.position, forceEnd);
            }
        }
    }

    // Public API
    public void SetTarget(Vector3 position, Quaternion rotation)
    {
        targetPosition = position;
        targetRotation = rotation;
        useTargetTransform = false;
    }
    
    public void SetTarget(Transform transform)
    {
        targetTransform = transform;
        useTargetTransform = true;
    }
    
    public void SetTargetPosition(Vector3 position)
    {
        targetPosition = position;
        useTargetTransform = false;
    }
    
    public void SetTargetRotation(Quaternion rotation)
    {
        targetRotation = rotation;
        useTargetTransform = false;
    }

    public void SetForces(float attraction, float rotation, float velocityMatching = -1f, float angularMatching = -1f)
    {
        attractionForce = attraction;
        rotationTorque = rotation;
        if (velocityMatching >= 0) velocityMatchingForce = velocityMatching;
        if (angularMatching >= 0) angularVelocityMatchingTorque = angularMatching;
    }
    
    public void SetDamping(float position, float rotation)
    {
        positionDamping = position;
        rotationDamping = rotation;
    }
    
    public void SetPrediction(bool enabled, float posTime = 0.15f, float rotTime = 0.1f)
    {
        usePredictiveForces = enabled;
        positionPredictionTime = posTime;
        rotationPredictionTime = rotTime;
    }

    // Getters
    public float GetDistanceToTarget() => currentDistance;
    public float GetAngleToTarget() => currentAngle;
    public Vector3 GetTargetVelocity() => targetVelocity;
    public Vector3 GetTargetAngularVelocity() => targetAngularVelocity;
    public Vector3 GetLastAppliedForce() => lastAppliedForce;
    public Vector3 GetLastAppliedTorque() => lastAppliedTorque;
    public bool IsMoving() => targetRigidbody != null && targetRigidbody.linearVelocity.magnitude > 0.1f;
}