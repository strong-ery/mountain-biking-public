using System.Collections;
using rayzngames;
using UnityEngine;
using UnityEngine.UI;

public class BasicSwap : MonoBehaviour
{
    [Header("Cohesive System")]
    public GameObject nullStateObjectScriptGroup;
    public GameObject mountedObjectScriptGroup;
    public GameObject dismountedObjectScriptGroup;
    public GameObject ragdolledObjectScriptGroup;
    public GameObject deadObjectScriptGroup;
    public BicycleVehicle bv;
    public BikeStabilizer bs;
    public Animator ARAnimator;

    [Header("Distance Check")]
    public GameObject AROrigin; // Active ragdoll origin
    public float mountingRange = 2f; // Distance within which mounting is allowed

    [Header("Ragdoll Recovery")]
    public float belowThisRecover = 1f; // Speed threshold below which ragdoll recovery is allowed
    public Rigidbody rootPlayerRigidbody; // Root player rigidbody to check speed

    [Header("States")]
    public bool ragdolled; // enable ROSG
    public bool dead; // enable DOSG

    [Header("UI")]
    public UnityEngine.UI.Image cursorUIElement;
    public UnityEngine.UI.Image radialBarElement;
    public Sprite bikeSprite;
    public Sprite characterSprite;
    public Sprite defaultCursor;

    [Header("Timing")]
    public float dismountTime = 1.5f;
    public float mountTime = 2.0f;

    private bool swap;
    private bool previousSwapState;
    private bool previousRagdolledState;
    private bool previousDeadState;
    private bool isTransitioning = false;
    private Coroutine ragdollRecoveryCoroutine;
    private Coroutine mountingCoroutine;
    private bool isInRange = false;
    private bool isHoldingE = false;

    // if swap is false, we are on the bike, if true, we are off the bike

    void Start()
    {
        swap = !mountedObjectScriptGroup.activeSelf;
        previousSwapState = swap;
        previousRagdolledState = ragdolled;
        previousDeadState = dead;
        
        // Initialize UI
        ResetUI();
        
        // Ensure initial state is correct
        ApplySwapState();
    }

    void Update()
    {
        // Update range check for UI
        bool wasInRange = isInRange;
        isInRange = CanMount();
        
        // Handle UI cursor changes based on state and range
        UpdateCursor();
        
        // Handle E key input - PREVENT mounting if ragdolled or dead
        if (Input.GetKeyDown(KeyCode.E) && !isTransitioning && !ragdolled && !dead)
        {
            if (swap && isInRange) // Trying to mount
            {
                isHoldingE = true;
                mountingCoroutine = StartCoroutine(HandleMountingProgress());
            }
            else if (!swap && bv.currentSpeed <= 0.5f) // Trying to dismount (only when stopped)
            {
                isHoldingE = true;
                mountingCoroutine = StartCoroutine(HandleDismountingProgress());
            }
        }
        
        if (Input.GetKeyUp(KeyCode.E))
        {
            isHoldingE = false;
            CancelMountingProgress();
        }

        // Check for state changes
        bool ragdollChanged = ragdolled != previousRagdolledState;
        bool deadChanged = dead != previousDeadState;
        bool swapChanged = swap != previousSwapState;

        // Handle death state change (highest priority)
        if (deadChanged && dead && !isTransitioning)
        {
            // Stop ragdoll recovery if we die
            if (ragdollRecoveryCoroutine != null)
            {
                StopCoroutine(ragdollRecoveryCoroutine);
                ragdollRecoveryCoroutine = null;
            }

            if (!swap) // Currently on bike
            {
                StartCoroutine(HandleDeathFromBike());
            }
            else // Currently off bike
            {
                StartCoroutine(HandleDeathOffBike());
            }
        }
        // Handle ragdoll state change (if not dead and not already transitioning)
        else if (ragdollChanged && !dead && !isTransitioning)
        {
            if (ragdolled)
            {
                // Starting ragdoll
                if (!swap) // Currently on bike
                {
                    StartCoroutine(HandleRagdollFromBike());
                }
                else
                {
                    // If already off bike, just enable ragdoll
                    ragdolledObjectScriptGroup.SetActive(true);
                }
                
                // Start the recovery timer
                ragdollRecoveryCoroutine = StartCoroutine(RagdollRecoveryTimer());
            }
            else
            {
                // Manually stopping ragdoll - cancel recovery timer
                if (ragdollRecoveryCoroutine != null)
                {
                    StopCoroutine(ragdollRecoveryCoroutine);
                    ragdollRecoveryCoroutine = null;
                }
            }
        }
        // Handle normal swap if no special states are active and not transitioning
        else if (swapChanged && !ragdolled && !dead && !isTransitioning)
        {
            ApplySwapState();
        }

        // Update previous states
        previousSwapState = swap;
        previousRagdolledState = ragdolled;
        previousDeadState = dead;
    }

    private void UpdateCursor()
    {
        if (isTransitioning || ragdolled || dead)
        {
            // Keep default cursor during transitions or special states
            cursorUIElement.sprite = defaultCursor;
            return;
        }

        if (swap) // Dismounted
        {
            if (isInRange)
            {
                cursorUIElement.sprite = bikeSprite;
            }
            else
            {
                cursorUIElement.sprite = defaultCursor;
            }
        }
        else // Mounted
        {
            // Only show character sprite if bike speed is low enough
            if (bv.currentSpeed <= 0.5f) // Same threshold as dismounting
            {
                cursorUIElement.sprite = characterSprite;
            }
            else
            {
                cursorUIElement.sprite = defaultCursor;
            }
        }
    }

    private void ResetUI()
    {
        cursorUIElement.sprite = defaultCursor;
        radialBarElement.fillAmount = 0f;
    }

    private void CancelMountingProgress()
    {
        if (mountingCoroutine != null)
        {
            StopCoroutine(mountingCoroutine);
            mountingCoroutine = null;
        }
        ResetUI();
    }

    private IEnumerator HandleMountingProgress()
    {
        float elapsed = 0f;
        
        // Add additional checks for ragdolled and dead states during mounting
        while (elapsed < mountTime && isHoldingE && isInRange && !isTransitioning && !ragdolled && !dead)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / mountTime;
            radialBarElement.fillAmount = progress;
            yield return null;
        }

        // Complete mounting only if all conditions are still met
        if (elapsed >= mountTime && isHoldingE && isInRange && !isTransitioning && !ragdolled && !dead)
        {
            // Complete mounting
            swap = false;
            ApplySwapState();
        }

        // Reset UI regardless of outcome
        ResetUI();
        mountingCoroutine = null;
    }

    private IEnumerator HandleDismountingProgress()
    {
        float elapsed = 0f;
        radialBarElement.fillAmount = 1f; // Start full
        
        // Add additional checks for ragdolled and dead states during dismounting
        while (elapsed < dismountTime && isHoldingE && !isTransitioning && !ragdolled && !dead)
        {
            elapsed += Time.deltaTime;
            float progress = 1f - (elapsed / dismountTime); // Count down from 1 to 0
            radialBarElement.fillAmount = progress;
            yield return null;
        }

        // Complete dismounting only if all conditions are still met
        if (elapsed >= dismountTime && isHoldingE && !isTransitioning && !ragdolled && !dead)
        {
            // Complete dismounting
            swap = true;
            ApplySwapState();
        }

        // Reset UI regardless of outcome
        ResetUI();
        mountingCoroutine = null;
    }

    private bool CanMount()
    {
        // PREVENT mounting if ragdolled, dead, already mounted, or during transitions
        if (AROrigin == null || swap == false || ragdolled || dead || isTransitioning)
        {
            return false;
        }

        // Find all objects with SelfBike layer
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        
        foreach (GameObject obj in allObjects)
        {
            if (obj.layer == LayerMask.NameToLayer("SelfBike"))
            {
                float distance = Vector3.Distance(AROrigin.transform.position, obj.transform.position);
                if (distance <= mountingRange)
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    private bool CanRecoverFromRagdoll()
    {
        if (rootPlayerRigidbody == null)
        {
            // If no rigidbody is assigned, allow recovery (fallback behavior)
            return true;
        }
        
        // Check if the player's speed is below the recovery threshold
        return rootPlayerRigidbody.linearVelocity.magnitude <= belowThisRecover;
    }

    private IEnumerator RagdollRecoveryTimer()
    {
        yield return new WaitForSeconds(5f);
        
        // Check if we can recover based on speed
        if (ragdolled && !dead)
        {
            if (CanRecoverFromRagdoll())
            {
                // Speed is low enough, recover to dismounted
                ragdolled = false; // Uncheck ragdolled
                swap = true; // Ensure we're in dismounted state
                
                // Apply the dismounted state
                dismountedObjectScriptGroup.SetActive(true);
                ragdollRecoveryCoroutine = null;
            }
            else
            {
                // Speed is too high, wait another 5 seconds
                ragdollRecoveryCoroutine = StartCoroutine(RagdollRecoveryTimer());
            }
        }
        else
        {
            ragdollRecoveryCoroutine = null;
        }
    }

    private IEnumerator HandleDeathFromBike()
    {
        isTransitioning = true;
        CancelMountingProgress(); // Cancel any ongoing mounting/dismounting
        
        // Step 1: Switch to null state for 0.1 seconds
        nullStateObjectScriptGroup.SetActive(true);
        bv.enabled = false;
        bs.enabled = false;
        ARAnimator.SetTrigger("Dismount");
        swap = true; // Update internal state
        
        yield return new WaitForSeconds(0.1f);
        
        // Step 2: Switch to dismounted for 0.1 seconds (null state will auto-disable)
        dismountedObjectScriptGroup.SetActive(true);
        
        yield return new WaitForSeconds(0.1f);
        
        // Step 3: Switch to ragdolled for 0.1 seconds (dismounted will auto-disable)
        ragdolledObjectScriptGroup.SetActive(true);
        
        yield return new WaitForSeconds(0.1f);
        
        // Step 4: Switch to dead (ragdolled will auto-disable)
        deadObjectScriptGroup.SetActive(true);
        
        isTransitioning = false;
    }

    private IEnumerator HandleDeathOffBike()
    {
        isTransitioning = true;
        CancelMountingProgress(); // Cancel any ongoing mounting/dismounting
        
        // Step 1: Switch to null state for 0.1 seconds
        nullStateObjectScriptGroup.SetActive(true);
        
        yield return new WaitForSeconds(0.1f);
        
        // Step 2: Switch to dismounted for 0.1 seconds (null state will auto-disable)
        dismountedObjectScriptGroup.SetActive(true);
        
        yield return new WaitForSeconds(0.1f);
        
        // Step 3: Switch to ragdolled for 0.1 seconds (dismounted will auto-disable)
        ragdolledObjectScriptGroup.SetActive(true);
        
        yield return new WaitForSeconds(0.1f);
        
        // Step 4: Switch to dead (ragdolled will auto-disable)
        deadObjectScriptGroup.SetActive(true);
        
        isTransitioning = false;
    }

    private IEnumerator HandleRagdollFromBike()
    {
        isTransitioning = true;
        CancelMountingProgress(); // Cancel any ongoing mounting/dismounting
        
        // Step 1: Switch to null state for 0.1 seconds
        nullStateObjectScriptGroup.SetActive(true);
        bv.enabled = false;
        bs.enabled = false;
        ARAnimator.SetTrigger("Dismount");
        swap = true; // Update internal state
        
        yield return new WaitForSeconds(0.1f);
        
        // Step 2: Switch to dismounted for 0.1 seconds (null state will auto-disable)
        dismountedObjectScriptGroup.SetActive(true);
        
        yield return new WaitForSeconds(0.1f);
        
        // Step 3: Switch to ragdolled (dismounted will auto-disable)
        ragdolledObjectScriptGroup.SetActive(true);
        
        isTransitioning = false;
    }

    private void ApplySwapState()
    {
        Rigidbody bvrb = bv.gameObject.GetComponent<Rigidbody>();

        if (swap)
        {
            // Dismounted state
            mountedObjectScriptGroup.SetActive(false);
            dismountedObjectScriptGroup.SetActive(true);
            bv.enabled = false;
            bs.enabled = false;
            ARAnimator.SetTrigger("Dismount");

            bvrb.constraints = RigidbodyConstraints.None;
        }
        else
        {
            // Mounted state
            dismountedObjectScriptGroup.SetActive(false);
            mountedObjectScriptGroup.SetActive(true);
            bv.enabled = true;
            bs.enabled = true;

            bvrb.constraints = RigidbodyConstraints.FreezeRotationZ;
            bvrb.transform.eulerAngles = new Vector3(bvrb.transform.eulerAngles.x, bvrb.transform.eulerAngles.y, 0f);
        }
    }
}