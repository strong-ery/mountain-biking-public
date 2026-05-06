using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InitialStateInfoStorage : MonoBehaviour
{
    [SerializeField] private List<Rigidbody> rigidbodies = new List<Rigidbody>();
    
    private Dictionary<ConfigurableJoint, float> originalSlerpPositionSprings = new Dictionary<ConfigurableJoint, float>();
    private bool isInitialized = false;
    private Coroutine interpolationCoroutine = null;

    void Start()
    {
        InitializeOriginalValues();
    }

    public List<Rigidbody> GetRigidbodies()
    {
        return rigidbodies;
    }

    public List<ConfigurableJoint> GetConfigurableJoints()
    {
        List<ConfigurableJoint> joints = new List<ConfigurableJoint>();
        
        foreach (Rigidbody rb in rigidbodies)
        {
            if (rb != null)
            {
                ConfigurableJoint[] rbJoints = rb.GetComponents<ConfigurableJoint>();
                joints.AddRange(rbJoints);
            }
        }
        
        return joints;
    }

    public float GetSlerpPositionSpring(ConfigurableJoint joint)
    {
        if (joint != null)
        {
            return joint.slerpDrive.positionSpring;
        }
        return 0f;
    }

    public List<float> GetAllSlerpPositionSprings()
    {
        List<float> springs = new List<float>();
        List<ConfigurableJoint> joints = GetConfigurableJoints();
        
        foreach (ConfigurableJoint joint in joints)
        {
            springs.Add(GetSlerpPositionSpring(joint));
        }
        
        return springs;
    }

    public void RestoreOriginalConfigJointValuesToAll()
    {
        RestoreOriginalConfigJointValuesToAll(false, 1f);
    }

    public void RestoreOriginalConfigJointValuesToAll(bool interpolate, float interpolationDuration = 1f)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Original values not initialized yet. Call InitializeOriginalValues() first.");
            return;
        }

        // Stop any existing interpolation
        if (interpolationCoroutine != null)
        {
            StopCoroutine(interpolationCoroutine);
            interpolationCoroutine = null;
        }

        if (interpolate)
        {
            interpolationCoroutine = StartCoroutine(InterpolateToOriginalValues(interpolationDuration));
        }
        else
        {
            // Immediate restoration
            foreach (var kvp in originalSlerpPositionSprings)
            {
                ConfigurableJoint joint = kvp.Key;
                float originalSpring = kvp.Value;
                
                if (joint != null)
                {
                    JointDrive slerpDrive = joint.slerpDrive;
                    slerpDrive.positionSpring = originalSpring;
                    joint.slerpDrive = slerpDrive;
                }
            }
        }
    }

    private IEnumerator InterpolateToOriginalValues(float duration)
    {
        // Store current values at start of interpolation
        Dictionary<ConfigurableJoint, float> currentValues = new Dictionary<ConfigurableJoint, float>();
        
        foreach (var kvp in originalSlerpPositionSprings)
        {
            ConfigurableJoint joint = kvp.Key;
            if (joint != null)
            {
                currentValues[joint] = joint.slerpDrive.positionSpring;
            }
        }

        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // Use smooth step for better interpolation curve
            t = t * t * (3f - 2f * t);
            
            foreach (var kvp in originalSlerpPositionSprings)
            {
                ConfigurableJoint joint = kvp.Key;
                float originalValue = kvp.Value;
                
                if (joint != null && currentValues.ContainsKey(joint))
                {
                    float currentValue = currentValues[joint];
                    float interpolatedValue = Mathf.Lerp(currentValue, originalValue, t);
                    
                    JointDrive slerpDrive = joint.slerpDrive;
                    slerpDrive.positionSpring = interpolatedValue;
                    joint.slerpDrive = slerpDrive;
                }
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Ensure final values are exactly the original values
        foreach (var kvp in originalSlerpPositionSprings)
        {
            ConfigurableJoint joint = kvp.Key;
            float originalSpring = kvp.Value;
            
            if (joint != null)
            {
                JointDrive slerpDrive = joint.slerpDrive;
                slerpDrive.positionSpring = originalSpring;
                joint.slerpDrive = slerpDrive;
            }
        }
        
        interpolationCoroutine = null;
    }

    public void SetAllConfigJointSlerpPosSpringToValue(float springValue)
    {
        // Stop any existing interpolation when setting new values
        if (interpolationCoroutine != null)
        {
            StopCoroutine(interpolationCoroutine);
            interpolationCoroutine = null;
        }

        List<ConfigurableJoint> joints = GetConfigurableJoints();
        
        foreach (ConfigurableJoint joint in joints)
        {
            if (joint != null)
            {
                JointDrive slerpDrive = joint.slerpDrive;
                slerpDrive.positionSpring = springValue;
                joint.slerpDrive = slerpDrive;
            }
        }
    }

    public void InitializeOriginalValues()
    {
        originalSlerpPositionSprings.Clear();
        List<ConfigurableJoint> joints = GetConfigurableJoints();
        
        foreach (ConfigurableJoint joint in joints)
        {
            if (joint != null)
            {
                originalSlerpPositionSprings[joint] = joint.slerpDrive.positionSpring;
            }
        }
        
        isInitialized = true;
    }

    // Optional: Call this if you modify the rigidbodies list at runtime
    public void RefreshOriginalValues()
    {
        InitializeOriginalValues();
    }

    // Stop interpolation if needed
    public void StopInterpolation()
    {
        if (interpolationCoroutine != null)
        {
            StopCoroutine(interpolationCoroutine);
            interpolationCoroutine = null;
        }
    }

    void OnDestroy()
    {
        StopInterpolation();
    }
}