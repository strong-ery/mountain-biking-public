using UnityEngine;
using System.Collections;

public class ApplyBrakingForXSecondsOnEnable : MonoBehaviour
{
    public float seconds;
    public WheelCollider frontWheelCollider;
    public WheelCollider rearWheelCollider;
    public float brakeForce;
    public bool falloff;

    void OnEnable()
    {
        StartCoroutine(BrakeForSeconds());
    }

    private IEnumerator BrakeForSeconds()
    {
        if (falloff)
        {
            // Gradually reduce braking force over time
            float elapsedTime = 0f;
            
            while (elapsedTime < seconds)
            {
                // Calculate the falloff factor (1 at start, 0 at end)
                float falloffFactor = 1f - (elapsedTime / seconds);
                float currentBrakeForce = brakeForce * falloffFactor;
                
                // Apply the current brake force
                frontWheelCollider.brakeTorque = currentBrakeForce;
                rearWheelCollider.brakeTorque = currentBrakeForce;
                
                elapsedTime += Time.deltaTime;
                yield return null; // Wait for next frame
            }
        }
        else
        {
            // Apply constant braking force for the full duration
            frontWheelCollider.brakeTorque = brakeForce;
            rearWheelCollider.brakeTorque = brakeForce;
            
            // Wait for the specified number of seconds
            yield return new WaitForSeconds(seconds);
        }
        
        // Ensure brakes are fully released at the end
        frontWheelCollider.brakeTorque = 0f;
        rearWheelCollider.brakeTorque = 0f;
    }
}