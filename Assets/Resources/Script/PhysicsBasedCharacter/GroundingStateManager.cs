using UnityEngine;

public class GroundingStateManager : MonoBehaviour
{
    [Header("References")]
    public bool depricated = false;
    public DualGroundFootHandler footHandler;
    public MonoBehaviour pelvicRotationalLocker; // PelvicRotationalLocker component
    public Rigidbody targetRigidbody;
    
    [Header("Timing Settings")]
    [Tooltip("Time in seconds before disabling locker when not grounded")]
    public float timeToDisable = 5f;
    [Tooltip("Time in seconds before enabling locker when grounded")]
    public float timeToEnable = 3f;
    
    [Header("Debug Info - Read Only")]
    public bool isCurrentlyGrounded;
    public bool lockerEnabled = true;
    public float currentTimer;
    public GroundingState currentState;
    
    // Private variables
    private float groundedTimer;
    private float notGroundedTimer;
    private RigidbodyConstraints originalConstraints;
    private bool hasStoredOriginalConstraints = false;
    
    public enum GroundingState
    {
        Grounded,
        WaitingToDisable,
        NotGrounded,
        WaitingToEnable
    }
    
    void Start()
    {
        // Validate references
        if (footHandler == null)
        {
            footHandler = GetComponent<DualGroundFootHandler>();
            if (footHandler == null)
            {
                Debug.LogError($"GroundingStateManager on {gameObject.name}: No DualGroundFootHandler found!");
                enabled = false;
                return;
            }
        }
        
        if (pelvicRotationalLocker == null)
        {
            Debug.LogError($"GroundingStateManager on {gameObject.name}: PelvicRotationalLocker not assigned!");
            enabled = false;
            return;
        }
        
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
            if (targetRigidbody == null)
            {
                Debug.LogError($"GroundingStateManager on {gameObject.name}: No Rigidbody found!");
                enabled = false;
                return;
            }
        }
        
        // Store original rigidbody constraints
        if (!hasStoredOriginalConstraints)
        {
            originalConstraints = targetRigidbody.constraints;
            hasStoredOriginalConstraints = true;
        }
        
        // Initialize state
        currentState = GroundingState.Grounded;
        lockerEnabled = pelvicRotationalLocker.enabled;
        
        Debug.Log($"GroundingStateManager initialized. Original constraints: {originalConstraints}");
    }
    
    void Update()
    {
        if (depricated)
            return;
            
        // Check if either foot is grounded
        isCurrentlyGrounded = footHandler.leftGrounded || footHandler.rightGrounded;
        
        switch (currentState)
        {
            case GroundingState.Grounded:
                HandleGroundedState();
                break;
                
            case GroundingState.WaitingToDisable:
                HandleWaitingToDisableState();
                break;
                
            case GroundingState.NotGrounded:
                HandleNotGroundedState();
                break;
                
            case GroundingState.WaitingToEnable:
                HandleWaitingToEnableState();
                break;
        }
        
        // Update debug timer display
        switch (currentState)
        {
            case GroundingState.WaitingToDisable:
                currentTimer = timeToDisable - notGroundedTimer;
                break;
            case GroundingState.WaitingToEnable:
                currentTimer = timeToEnable - groundedTimer;
                break;
            default:
                currentTimer = 0f;
                break;
        }
    }
    
    private void HandleGroundedState()
    {
        if (isCurrentlyGrounded)
        {
            // Reset timers when grounded
            notGroundedTimer = 0f;
            groundedTimer = 0f;
        }
        else
        {
            // Start counting time not grounded
            currentState = GroundingState.WaitingToDisable;
            notGroundedTimer = 0f;
        }
    }
    
    private void HandleWaitingToDisableState()
    {
        if (isCurrentlyGrounded)
        {
            // Became grounded again, return to grounded state
            currentState = GroundingState.Grounded;
            notGroundedTimer = 0f;
        }
        else
        {
            notGroundedTimer += Time.deltaTime;
            
            if (notGroundedTimer >= timeToDisable)
            {
                // Time to disable the locker and remove constraints
                DisableLockerAndConstraints();
                currentState = GroundingState.NotGrounded;
            }
        }
    }
    
    private void HandleNotGroundedState()
    {
        if (isCurrentlyGrounded)
        {
            // Became grounded, start counting time to re-enable
            currentState = GroundingState.WaitingToEnable;
            groundedTimer = 0f;
        }
    }
    
    private void HandleWaitingToEnableState()
    {
        if (!isCurrentlyGrounded)
        {
            // Lost grounding again, return to not grounded state
            currentState = GroundingState.NotGrounded;
            groundedTimer = 0f;
        }
        else
        {
            groundedTimer += Time.deltaTime;
            
            if (groundedTimer >= timeToEnable)
            {
                // Time to re-enable the locker and restore constraints
                EnableLockerAndConstraints();
                currentState = GroundingState.Grounded;
            }
        }
    }
    
    private void DisableLockerAndConstraints()
    {
        // Disable the PelvicRotationalLocker
        if (pelvicRotationalLocker != null && pelvicRotationalLocker.enabled)
        {
            pelvicRotationalLocker.enabled = false;
            lockerEnabled = false;
            Debug.Log("PelvicRotationalLocker disabled - player airborne for " + timeToDisable + " seconds");
        }
        
        // Remove X and Z rotational constraints from rigidbody
        if (targetRigidbody != null)
        {
            RigidbodyConstraints newConstraints = targetRigidbody.constraints;
            
            // Remove FreezeRotationX and FreezeRotationZ constraints
            newConstraints &= ~RigidbodyConstraints.FreezeRotationX;
            newConstraints &= ~RigidbodyConstraints.FreezeRotationZ;
            
            targetRigidbody.constraints = newConstraints;
            Debug.Log($"Rigidbody constraints updated: {newConstraints} (removed X and Z rotation constraints)");
        }
    }
    
    private void EnableLockerAndConstraints()
    {
        targetRigidbody.transform.position += new Vector3(0, 2, 0);

        // Enable the PelvicRotationalLocker
        if (pelvicRotationalLocker != null && !pelvicRotationalLocker.enabled)
        {
            pelvicRotationalLocker.enabled = true;
            lockerEnabled = true;
            Debug.Log("PelvicRotationalLocker enabled - player grounded for " + timeToEnable + " seconds");
        }
        
        // Restore original rigidbody constraints
        if (targetRigidbody != null && hasStoredOriginalConstraints)
        {
            targetRigidbody.constraints = originalConstraints;
            Debug.Log($"Rigidbody constraints restored: {originalConstraints}");
        }
    }
    
    // Public methods for external access
    public bool IsGrounded()
    {
        return isCurrentlyGrounded;
    }
    
    public bool IsLockerEnabled()
    {
        return lockerEnabled;
    }
    
    public float GetTimeUntilStateChange()
    {
        switch (currentState)
        {
            case GroundingState.WaitingToDisable:
                return timeToDisable - notGroundedTimer;
            case GroundingState.WaitingToEnable:
                return timeToEnable - groundedTimer;
            default:
                return 0f;
        }
    }
    
    // Manual override methods (useful for testing or special situations)
    [ContextMenu("Force Disable Locker")]
    public void ForceDisableLocker()
    {
        DisableLockerAndConstraints();
        currentState = GroundingState.NotGrounded;
        Debug.Log("Manually disabled locker and constraints");
    }
    
    [ContextMenu("Force Enable Locker")]
    public void ForceEnableLocker()
    {
        EnableLockerAndConstraints();
        currentState = GroundingState.Grounded;
        Debug.Log("Manually enabled locker and constraints");
    }
    
    void OnValidate()
    {
        // Ensure timing values are positive
        if (timeToDisable < 0f) timeToDisable = 0f;
        if (timeToEnable < 0f) timeToEnable = 0f;
    }
}