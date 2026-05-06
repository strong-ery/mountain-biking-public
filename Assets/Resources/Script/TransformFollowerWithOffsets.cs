using UnityEngine;
using System.Collections.Generic;

public class TransformFollowerWithOffsets : MonoBehaviour
{
    [Header("Transforms to Follow")]
    public Transform[] donors;  // Can be a single or multiple donors
    public Transform receiver;  // The transform that will follow

    [Header("Offsets (Local to Donors)")]
    public Vector3 positionOffset = Vector3.zero; // Local offset from each donor
    public Vector3 rotationOffset = Vector3.zero; // Local rotation offset (degrees)

    [Header("Rotation Modes")]
    public bool cloneRotation = true;
    public bool rotateTo = false; // NEW: Rotate to face average donor position
    public bool cloneRotationX = true;
    public bool cloneRotationY = true;
    public bool cloneRotationZ = true;

    [Header("Position Options")]
    public bool clonePosition = true;
    public bool interpolate = false; // Use Rigidbody interpolation if available
    
    [Header("Smoothing Options")]
    public SmoothingType smoothingType = SmoothingType.None;
    
    [Header("Exponential Smoothing")]
    [Range(0f, 1f)]
    public float positionSmoothingFactor = 0.1f; // Lower = smoother
    [Range(0f, 1f)]
    public float rotationSmoothingFactor = 0.1f;
    
    [Header("Velocity-Based Smoothing")]
    public float maxPositionSpeed = 5f; // Max units per second
    public float maxRotationSpeed = 180f; // Max degrees per second
    
    [Header("Dead Zone Filtering")]
    public bool useDeadZone = false;
    public float positionDeadZone = 0.001f; // Ignore movements smaller than this
    public float rotationDeadZone = 0.1f; // Ignore rotations smaller than this (degrees)
    
    [Header("Kalman Filter")]
    public float processNoise = 0.01f; // How much we trust the model
    public float measurementNoise = 0.1f; // How much we trust the measurements

    public enum SmoothingType
    {
        None,
        ExponentialSmoothing,
        VelocityBased,
        DeadZoneFiltering,
        SimpleKalman,
        Hybrid // Combines multiple techniques
    }

    private Rigidbody rb;
    
    // Smoothing state variables
    private Vector3 smoothedPosition;
    private Quaternion smoothedRotation;
    private bool smoothingInitialized = false;
    
    // Velocity-based smoothing
    private Vector3 currentVelocity;
    private float currentAngularVelocity;
    
    // Simple Kalman filter variables
    private Vector3 kalmanPosition;
    private Vector3 kalmanVelocity;
    private float kalmanPositionVariance = 1f;
    private float kalmanVelocityVariance = 1f;

    void OnValidate()
    {
        // Ensure only one rotation mode is active at a time
        if (cloneRotation && rotateTo) rotateTo = false;
    }

    void Start()
    {
        if (interpolate && receiver != null)
        {
            rb = receiver.GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogWarning("Interpolate is enabled but the receiver has no Rigidbody. Disabling interpolation.");
                interpolate = false;
            }
        }
    }

    void LateUpdate()
    {
        if (receiver == null || donors == null || donors.Length == 0)
            return;

        // ===== CALCULATE RAW POSITION & ROTATION =====
        Vector3 rawPosition = CalculateRawPosition();
        Quaternion rawRotation = CalculateRawRotation();

        // ===== APPLY SMOOTHING =====
        Vector3 finalPosition = ApplyPositionSmoothing(rawPosition);
        Quaternion finalRotation = ApplyRotationSmoothing(rawRotation);

        // ===== APPLY TO RECEIVER =====
        if (clonePosition)
        {
            if (interpolate && rb != null)
                rb.MovePosition(finalPosition);
            else
                receiver.position = finalPosition;
        }

        if (cloneRotation)
        {
            if (interpolate && rb != null)
                rb.MoveRotation(finalRotation);
            else
                receiver.rotation = finalRotation;
        }
        else if (rotateTo)
        {
            Vector3 direction = (finalPosition - receiver.position).normalized;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(direction, Vector3.up);
                lookRot *= Quaternion.Euler(rotationOffset);
                finalRotation = ApplyRotationSmoothing(lookRot);

                if (interpolate && rb != null)
                    rb.MoveRotation(finalRotation);
                else
                    receiver.rotation = finalRotation;
            }
        }
    }

    private Vector3 CalculateRawPosition()
    {
        Vector3 rawPosition = Vector3.zero;
        int posCount = 0;

        foreach (Transform donor in donors)
        {
            if (donor != null)
            {
                rawPosition += donor.position + donor.rotation * positionOffset;
                posCount++;
            }
        }

        if (posCount > 0)
            rawPosition /= posCount;

        return rawPosition;
    }

    private Quaternion CalculateRawRotation()
    {
        Quaternion avgRotation = Quaternion.identity;
        int rotCount = 0;

        foreach (Transform donor in donors)
        {
            if (donor != null)
            {
                Quaternion donorWithOffset = donor.rotation * Quaternion.Euler(rotationOffset);

                if (rotCount == 0)
                    avgRotation = donorWithOffset;
                else
                    avgRotation = Quaternion.Slerp(avgRotation, donorWithOffset, 1f / (rotCount + 1));

                rotCount++;
            }
        }

        // Apply axis filtering
        if (rotCount > 0 && cloneRotation)
        {
            Vector3 finalEuler = receiver.rotation.eulerAngles;
            Vector3 avgEuler = avgRotation.eulerAngles;
            if (cloneRotationX) finalEuler.x = avgEuler.x;
            if (cloneRotationY) finalEuler.y = avgEuler.y;
            if (cloneRotationZ) finalEuler.z = avgEuler.z;
            avgRotation = Quaternion.Euler(finalEuler);
        }

        return avgRotation;
    }

    private Vector3 ApplyPositionSmoothing(Vector3 targetPosition)
    {
        if (!smoothingInitialized)
        {
            smoothedPosition = targetPosition;
            kalmanPosition = targetPosition;
            smoothingInitialized = true;
            return targetPosition;
        }

        switch (smoothingType)
        {
            case SmoothingType.ExponentialSmoothing:
                return ExponentialSmoothPosition(targetPosition);
            
            case SmoothingType.VelocityBased:
                return VelocityBasedSmoothPosition(targetPosition);
            
            case SmoothingType.DeadZoneFiltering:
                return DeadZoneFilterPosition(targetPosition);
            
            case SmoothingType.SimpleKalman:
                return KalmanFilterPosition(targetPosition);
            
            case SmoothingType.Hybrid:
                // Combine dead zone with exponential smoothing
                Vector3 deadZoneFiltered = DeadZoneFilterPosition(targetPosition);
                return ExponentialSmoothPosition(deadZoneFiltered);
            
            default:
                return targetPosition;
        }
    }

    private Quaternion ApplyRotationSmoothing(Quaternion targetRotation)
    {
        if (!smoothingInitialized)
        {
            smoothedRotation = targetRotation;
            return targetRotation;
        }

        switch (smoothingType)
        {
            case SmoothingType.ExponentialSmoothing:
                smoothedRotation = Quaternion.Slerp(smoothedRotation, targetRotation, rotationSmoothingFactor);
                return smoothedRotation;
            
            case SmoothingType.VelocityBased:
                float maxRotationRadians = maxRotationSpeed * Mathf.Deg2Rad * Time.deltaTime;
                smoothedRotation = Quaternion.RotateTowards(smoothedRotation, targetRotation, maxRotationRadians * Mathf.Rad2Deg);
                return smoothedRotation;
            
            case SmoothingType.DeadZoneFiltering:
                float angleDiff = Quaternion.Angle(smoothedRotation, targetRotation);
                if (angleDiff > rotationDeadZone)
                {
                    smoothedRotation = targetRotation;
                }
                return smoothedRotation;
            
            case SmoothingType.Hybrid:
                // Dead zone + exponential
                float angle = Quaternion.Angle(smoothedRotation, targetRotation);
                if (angle > rotationDeadZone)
                {
                    smoothedRotation = Quaternion.Slerp(smoothedRotation, targetRotation, rotationSmoothingFactor);
                }
                return smoothedRotation;
            
            default:
                return targetRotation;
        }
    }

    private Vector3 ExponentialSmoothPosition(Vector3 targetPosition)
    {
        smoothedPosition = Vector3.Lerp(smoothedPosition, targetPosition, positionSmoothingFactor);
        return smoothedPosition;
    }

    private Vector3 VelocityBasedSmoothPosition(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - smoothedPosition);
        float distance = direction.magnitude;
        
        if (distance > 0.0001f)
        {
            float maxDistance = maxPositionSpeed * Time.deltaTime;
            if (distance > maxDistance)
            {
                smoothedPosition += direction.normalized * maxDistance;
            }
            else
            {
                smoothedPosition = targetPosition;
            }
        }
        
        return smoothedPosition;
    }

    private Vector3 DeadZoneFilterPosition(Vector3 targetPosition)
    {
        float distance = Vector3.Distance(smoothedPosition, targetPosition);
        if (distance > positionDeadZone)
        {
            smoothedPosition = targetPosition;
        }
        return smoothedPosition;
    }

    private Vector3 KalmanFilterPosition(Vector3 measurement)
    {
        float dt = Time.deltaTime;
        
        // Predict
        kalmanPosition += kalmanVelocity * dt;
        kalmanPositionVariance += processNoise;
        
        // Update
        float kalmanGain = kalmanPositionVariance / (kalmanPositionVariance + measurementNoise);
        Vector3 residual = measurement - kalmanPosition;
        kalmanPosition += residual * kalmanGain;
        kalmanVelocity = residual / dt;
        kalmanPositionVariance *= (1 - kalmanGain);
        
        smoothedPosition = kalmanPosition;
        return smoothedPosition;
    }
}