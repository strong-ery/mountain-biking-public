using UnityEngine;

public class AccelerativeAssistanceProbeRay : MonoBehaviour
{
    [Header("References")]
    public AccelerativeAssistance parentAssistance;
    
    [Header("Probe Settings")]
    public Transform frontProbe;
    public Transform backProbe;
    public float probeDistance = 5f;
    public LayerMask groundLayerMask = -1;
    
    [Header("Assistance Settings")]
    public ProbeAxis probeAxis = ProbeAxis.ForwardBack;
    [Tooltip("Base value for the first direction (Forward or Left)")]
    public float originalFirstValue = 1f;
    [Tooltip("Base value for the second direction (Backward or Right)")]
    public float originalSecondValue = 1f;
    [Tooltip("Uphill value for first direction (Forward or Left)")]
    public float firstUphillValue = 25f;
    [Tooltip("Downhill value for first direction (Forward or Left)")]
    public float firstDownhillValue = 0.5f;
    [Tooltip("Uphill value for second direction (Backward or Right)")]
    public float secondUphillValue = 25f;
    [Tooltip("Downhill value for second direction (Backward or Right)")]
    public float secondDownhillValue = 0.5f;
    
    [Header("Slope Detection")]
    [Tooltip("Minimum height difference to consider as a slope")]
    public float slopeThreshold = 0.1f;
    [Tooltip("Maximum height difference where assistance is still applied - prevents climbing impossible slopes")]
    public float maxHeightDifference = 3f;
    
    [Header("Debug Info")]
    public Vector3 frontHitPoint;
    public Vector3 backHitPoint;
    public Vector3 frontNormal;
    public Vector3 backNormal;
    public float heightDifference;
    public bool isUphill;
    public bool isTooSteep;
    public bool frontHit;
    public bool backHit;
    
    public enum ProbeAxis
    {
        ForwardBack,
        LeftRight
    }
    
    private float originalForwardValue;
    private float originalBackwardValue;
    private float originalLeftValue;
    private float originalRightValue;
    
    void Start()
    {
        if (parentAssistance != null)
        {
            // Store the current values as originals FIRST
            originalForwardValue = parentAssistance.forwardMultiplier;
            originalBackwardValue = parentAssistance.backwardMultiplier;
            originalLeftValue = parentAssistance.leftMultiplier;
            originalRightValue = parentAssistance.rightMultiplier;
            
            // ONLY set values for the selected axis
            switch (probeAxis)
            {
                case ProbeAxis.ForwardBack:
                    parentAssistance.forwardMultiplier = originalFirstValue;
                    parentAssistance.backwardMultiplier = originalSecondValue;
                    // Cache the values we just set
                    originalForwardValue = originalFirstValue;
                    originalBackwardValue = originalSecondValue;
                    break;
                    
                case ProbeAxis.LeftRight:
                    parentAssistance.leftMultiplier = originalFirstValue;
                    parentAssistance.rightMultiplier = originalSecondValue;
                    // Cache the values we just set
                    originalLeftValue = originalFirstValue;
                    originalRightValue = originalSecondValue;
                    break;
            }
        }
    }
    
    void Update()
    {
        if (parentAssistance == null || frontProbe == null || backProbe == null)
            return;
            
        ProbeGround();
        CalculateSlope();
        AdjustValues();
    }
    
    void ProbeGround()
    {
        // Cast rays in the local downward direction of each probe
        RaycastHit frontHitInfo;
        RaycastHit backHitInfo;
        
        Vector3 frontDownDirection = -frontProbe.transform.up;
        Vector3 backDownDirection = -backProbe.transform.up;
        
        frontHit = Physics.Raycast(frontProbe.position, frontDownDirection, out frontHitInfo, probeDistance, groundLayerMask);
        backHit = Physics.Raycast(backProbe.position, backDownDirection, out backHitInfo, probeDistance, groundLayerMask);
        
        if (frontHit)
        {
            frontHitPoint = frontHitInfo.point;
            frontNormal = frontHitInfo.normal;
        }
        else
        {
            frontHitPoint = frontProbe.position + frontDownDirection * probeDistance;
            frontNormal = frontProbe.transform.up;
        }
        
        if (backHit)
        {
            backHitPoint = backHitInfo.point;
            backNormal = backHitInfo.normal;
        }
        else
        {
            backHitPoint = backProbe.position + backDownDirection * probeDistance;
            backNormal = backProbe.transform.up;
        }
    }
    
    void CalculateSlope()
    {
        // Calculate height difference between the two probe points
        heightDifference = frontHitPoint.y - backHitPoint.y;
        
        // Determine if we're dealing with uphill movement
        // Positive height difference means front is higher than back
        isUphill = Mathf.Abs(heightDifference) > slopeThreshold;
        
        // Check if the slope is too steep for assistance
        isTooSteep = Mathf.Abs(heightDifference) > maxHeightDifference;
    }
    
    void AdjustValues()
    {
        if (!isUphill || isTooSteep)
        {
            // Reset to original values when on flat ground or slope is too steep
            ResetToOriginalValues();
            return;
        }
        
        switch (probeAxis)
        {
            case ProbeAxis.ForwardBack:
                AdjustForwardBackValues();
                break;
                
            case ProbeAxis.LeftRight:
                AdjustLeftRightValues();
                break;
        }
    }
    
    void AdjustForwardBackValues()
    {
        if (heightDifference > slopeThreshold)
        {
            // Front is higher - going forward is uphill, backward is downhill
            parentAssistance.forwardMultiplier = firstUphillValue;
            parentAssistance.backwardMultiplier = secondDownhillValue;
        }
        else if (heightDifference < -slopeThreshold)
        {
            // Back is higher - going backward is uphill, forward is downhill
            parentAssistance.forwardMultiplier = firstDownhillValue;
            parentAssistance.backwardMultiplier = secondUphillValue;
        }
        
        // NEVER TOUCH left/right values - they stay as they were originally
    }
    
    void AdjustLeftRightValues()
    {
        if (heightDifference > slopeThreshold)
        {
            // Front is higher (Front = Left probe) - going left is uphill, right is downhill
            parentAssistance.leftMultiplier = firstUphillValue;
            parentAssistance.rightMultiplier = secondDownhillValue;
        }
        else if (heightDifference < -slopeThreshold)
        {
            // Back is higher (Back = Right probe) - going right is uphill, left is downhill
            parentAssistance.leftMultiplier = firstDownhillValue;
            parentAssistance.rightMultiplier = secondUphillValue;
        }
        
        // NEVER TOUCH forward/back values - they stay as they were originally
    }
    
    void ResetToOriginalValues()
    {
        // Only reset values for the selected axis
        switch (probeAxis)
        {
            case ProbeAxis.ForwardBack:
                parentAssistance.forwardMultiplier = originalForwardValue;
                parentAssistance.backwardMultiplier = originalBackwardValue;
                break;
                
            case ProbeAxis.LeftRight:
                parentAssistance.leftMultiplier = originalLeftValue;
                parentAssistance.rightMultiplier = originalRightValue;
                break;
        }
    }
    
    void OnDrawGizmos()
    {
        if (frontProbe == null || backProbe == null)
            return;
            
        Vector3 frontDownDirection = -frontProbe.transform.up;
        Vector3 backDownDirection = -backProbe.transform.up;
            
        // Draw probe rays
        Gizmos.color = frontHit ? Color.green : Color.red;
        Gizmos.DrawRay(frontProbe.position, frontDownDirection * probeDistance);
        
        Gizmos.color = backHit ? Color.green : Color.red;
        Gizmos.DrawRay(backProbe.position, backDownDirection * probeDistance);
        
        // Draw hit points
        if (frontHit)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(frontHitPoint, 0.1f);
            
            // Draw surface normal
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(frontHitPoint, frontNormal);
        }
        
        if (backHit)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(backHitPoint, 0.1f);
            
            // Draw surface normal
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(backHitPoint, backNormal);
        }
        
        // Draw connection between probe points
        Gizmos.color = Color.white;
        Gizmos.DrawLine(frontProbe.position, backProbe.position);
        
        // Draw slope direction indicator
        if (isUphill)
        {
            Vector3 center = (frontProbe.position + backProbe.position) * 0.5f;
            
            if (isTooSteep)
            {
                // Draw in magenta if too steep
                Gizmos.color = Color.magenta;
            }
            else
            {
                // Normal slope colors
                Gizmos.color = heightDifference > 0 ? Color.red : Color.blue;
            }
            
            Vector3 slopeDirection = heightDifference > 0 ? 
                (frontProbe.position - backProbe.position).normalized : 
                (backProbe.position - frontProbe.position).normalized;
            Gizmos.DrawRay(center, slopeDirection * 2f);
        }
    }
}