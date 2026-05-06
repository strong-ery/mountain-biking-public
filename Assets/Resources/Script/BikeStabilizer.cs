using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // For DepthOfField

public class BikeStabilizer : MonoBehaviour
{
    public rayzngames.BicycleVehicle bv;
    public Rigidbody bikeRb;
    public Camera bikeCam;
    public Volume ppv;

    [Header("Stabilizer Settings")]
    public float minAngularDamping = 0.5f;
    public float maxAngularDamping = 5f;
    public float speedForMaxDamping = 20f;

    [Header("Linear Damping Settings")]
    public float minLinearDamping = 0.1f;
    public float maxLinearDamping = 1f;

    [Header("Pitch Stabilizer")]
    public float maxPitchAngle = 90f;
    public float pitchCorrectionForce = 10f;
    public float maxAngularVelocityX = 5f;

    [Header("Camera FOV Settings")]
    public float minFOV = 60f;
    public float maxFOV = 90f;
    public float speedForMaxFOV = 30f;

    void FixedUpdate()
    {
        if (bikeRb == null || bv == null)
            return;

        // Camera FOV scaling
        if (bikeCam != null)
        {
            float fovRatio = Mathf.Clamp01(bv.currentSpeed / speedForMaxFOV);
            bikeCam.fieldOfView = Mathf.Lerp(minFOV, maxFOV, fovRatio);
        }

        if (!bv.IsGrounded)
        {
            bikeRb.angularDamping = 0f;
            bikeRb.linearDamping = 0f;
            return;
        }

        float speedRatio = Mathf.Clamp01(bv.currentSpeed / speedForMaxDamping);

        // Angular damping
        bikeRb.angularDamping = Mathf.Lerp(minAngularDamping, maxAngularDamping, speedRatio);

        // Linear damping
        float inverseSpeedRatio = 1f - Mathf.Clamp01(bv.currentSpeed / speedForMaxDamping);
        bikeRb.linearDamping = Mathf.Lerp(minLinearDamping, maxLinearDamping, inverseSpeedRatio);

        // Limit pitch velocity
        Vector3 localAngularVel = transform.InverseTransformDirection(bikeRb.angularVelocity);
        localAngularVel.x = Mathf.Clamp(localAngularVel.x, -maxAngularVelocityX, maxAngularVelocityX);
        bikeRb.angularVelocity = transform.TransformDirection(localAngularVel);

        // Pitch correction
        if (bv.currentSpeed > speedForMaxDamping * 0.5f)
        {
            Vector3 euler = bikeRb.rotation.eulerAngles;
            float pitch = euler.x;
            if (pitch > 180f) pitch -= 360f;

            if (Mathf.Abs(pitch) > maxPitchAngle)
            {
                float correction = -pitch * speedRatio;
                bikeRb.AddTorque(transform.right * correction * pitchCorrectionForce, ForceMode.Acceleration);
            }
        }
    }
}
