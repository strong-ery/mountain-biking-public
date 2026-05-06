using UnityEngine;
using System.Collections;

public class CalculateActiveCamSpeed : MonoBehaviour
{
    [Header("Speed to Angle Mapping")]
    public float zeroSpeedAngle = 0f;
    public float maxSpeedAngle = -180f;
    public float zeroSpeed = 0f;
    public float maxSpeed = 300f;
    
    [Header("Calculation Settings")]
    public float speedBufferInterval = 0.5f; // seconds (accuracy goes up with time)
    
    [Header("Target RectTransform")]
    public RectTransform targetRectTransform; // Assign the RectTransform you want to rotate
    
    private GameObject currentSpeedTarget;
    private Rigidbody currentRigidbody;
    private Vector3 lastPosition;
    private float currentSpeed = 0f;
    private bool isCalculating = false;

    void Start()
    {
        // Start the speed calculation coroutine
        StartCoroutine(CalculateSpeedRoutine());
    }

    void Update()
    {
        // Find the currently active GameObject with the "CalculateSpeed" tag
        FindActiveSpeedTarget();
        
        // If we have a Rigidbody, use its velocity for smooth real-time speed calculation
        if (currentRigidbody != null)
        {
            currentSpeed = currentRigidbody.linearVelocity.magnitude;
        }
        
        // Update the rotation based on current speed
        UpdateRotation();
    }

    void FindActiveSpeedTarget()
    {
        GameObject[] speedObjects = GameObject.FindGameObjectsWithTag("CalculateSpeed");
        
        // Find the active one
        GameObject activeTarget = null;
        foreach (GameObject obj in speedObjects)
        {
            if (obj.activeInHierarchy)
            {
                activeTarget = obj;
                break; // Only one should be active at a time
            }
        }
        
        // If we found a new target, update our reference and reset position tracking
        if (activeTarget != currentSpeedTarget)
        {
            currentSpeedTarget = activeTarget;
            currentRigidbody = null;
            
            if (currentSpeedTarget != null)
            {
                lastPosition = currentSpeedTarget.transform.position;
                // Check if the target has a Rigidbody component
                currentRigidbody = currentSpeedTarget.GetComponent<Rigidbody>();
            }
        }
    }

    IEnumerator CalculateSpeedRoutine()
    {
        while (true)
        {
            // Only use position-based calculation if there's no Rigidbody
            if (currentSpeedTarget != null && currentSpeedTarget.activeInHierarchy && currentRigidbody == null)
            {
                if (!isCalculating)
                {
                    isCalculating = true;
                    Vector3 startPosition = currentSpeedTarget.transform.position;
                    
                    // Wait for the specified interval
                    yield return new WaitForSeconds(speedBufferInterval);
                    
                    // Calculate speed if the target is still valid and still has no Rigidbody
                    if (currentSpeedTarget != null && currentSpeedTarget.activeInHierarchy && currentRigidbody == null)
                    {
                        Vector3 endPosition = currentSpeedTarget.transform.position;
                        float distance = Vector3.Distance(startPosition, endPosition);
                        currentSpeed = distance / speedBufferInterval;
                    }
                    else if (currentRigidbody != null)
                    {
                        // Rigidbody was added, speed will be handled in Update()
                        currentSpeed = currentRigidbody.linearVelocity.magnitude;
                    }
                    else
                    {
                        currentSpeed = 0f;
                    }
                    
                    isCalculating = false;
                }
                else
                {
                    yield return null; // Wait one frame if already calculating
                }
            }
            else
            {
                // If using Rigidbody or no target, don't calculate position-based speed
                if (currentSpeedTarget == null || !currentSpeedTarget.activeInHierarchy)
                {
                    currentSpeed = 0f;
                }
                yield return null; // Wait one frame
            }
        }
    }

    void UpdateRotation()
    {
        if (targetRectTransform == null) return;
        
        // Map the current speed to an angle between zeroSpeedAngle and maxSpeedAngle
        float normalizedSpeed = Mathf.Clamp01(currentSpeed / maxSpeed);
        float targetAngle = Mathf.Lerp(zeroSpeedAngle, maxSpeedAngle, normalizedSpeed);
        
        // Apply the rotation to the Z axis of the RectTransform
        Vector3 currentRotation = targetRectTransform.eulerAngles;
        targetRectTransform.rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, targetAngle);
    }
}