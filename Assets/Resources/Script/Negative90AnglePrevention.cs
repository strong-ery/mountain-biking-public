using rayzngames;
using UnityEngine;

public class Negative90AnglePrevention : MonoBehaviour
{
    [Header("Bike Components")]
    public Rigidbody bikeRb;
    public BicycleVehicle bv;

    [Header("Angle Prevention Settings")]
    [SerializeField] private float dangerThreshold = 75f; // Start applying torque at 75°
    [SerializeField] private float maxTorqueForce = 500f;
    [SerializeField] private float torqueMultiplier = 2f; // How aggressively to scale torque
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private void Start()
    {
        // Auto-assign if not set
        if (bikeRb == null)
            bikeRb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (bikeRb == null) return;

        if (!bv.isAirControl) return;

        // Get current X rotation (pitch)
        float currentXAngle = GetNormalizedXAngle();
        
        // Check if we're in the danger zone
        float absAngle = Mathf.Abs(currentXAngle);
        
        if (absAngle > dangerThreshold)
        {
            ApplyCorrectiveTorque(currentXAngle, absAngle);
        }

        if (showDebugInfo)
        {
            Debug.Log($"X Angle: {currentXAngle:F1}°, Abs: {absAngle:F1}°");
        }
    }

    private float GetNormalizedXAngle()
    {
        // Get the X rotation and normalize it to -180 to 180 range
        Vector3 eulerAngles = bikeRb.transform.eulerAngles;
        float xAngle = eulerAngles.x;
        
        // Normalize to -180 to 180
        if (xAngle > 180f)
            xAngle -= 360f;
            
        return xAngle;
    }

    private void ApplyCorrectiveTorque(float currentXAngle, float absAngle)
    {
        // Calculate how close we are to the danger zone (0 = at threshold, 1 = at 90°)
        float dangerRatio = Mathf.Clamp01((absAngle - dangerThreshold) / (90f - dangerThreshold));
        
        // Progressive torque scaling - gets stronger as we get closer to 90°
        float torqueStrength = Mathf.Pow(dangerRatio, torqueMultiplier) * maxTorqueForce;
        
        // Determine direction - if positive angle, apply negative torque (and vice versa)
        float torqueDirection = -Mathf.Sign(currentXAngle);
        
        // Apply the corrective torque on X-axis
        Vector3 correctiveTorque = new Vector3(torqueDirection * torqueStrength, 0f, 0f);
        bikeRb.AddTorque(correctiveTorque, ForceMode.Force);
        
        if (showDebugInfo)
        {
            Debug.Log($"Applying corrective torque: {correctiveTorque.x:F1} (Danger ratio: {dangerRatio:F2})");
        }
    }

    // Helper method to visualize the danger zones in the scene view
    private void OnDrawGizmosSelected()
    {
        if (bikeRb == null) return;
        
        // Draw warning zone indicators
        Gizmos.color = Color.yellow;
        Vector3 pos = bikeRb.transform.position;
        
        // Visual representation of the danger threshold
        Gizmos.DrawWireSphere(pos + Vector3.up * 2f, 0.5f);
        
        float currentXAngle = GetNormalizedXAngle();
        float absAngle = Mathf.Abs(currentXAngle);
        
        if (absAngle > dangerThreshold)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pos + Vector3.up * 2f, 0.7f);
        }
    }
}