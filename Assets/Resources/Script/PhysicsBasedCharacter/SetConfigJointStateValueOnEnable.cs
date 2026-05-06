using UnityEngine;

public class SetConfigJointStateValueOnEnable : MonoBehaviour
{
    [Header("References")]
    public InitialStateInfoStorage initialStateInfoStorage;
    
    [Header("Settings")]
    public float target;
    public bool restoreToDefault;
    
    [Header("Interpolation Settings")]
    [Tooltip("When restoring to default, should the values interpolate smoothly?")]
    public bool interpolate = false;
    
    [Tooltip("Duration of interpolation in seconds (only used when interpolate is true)")]
    public float interpolationDuration = 1f;

    void OnEnable()
    {
        if (initialStateInfoStorage != null)
        {
            if (restoreToDefault)
            {
                initialStateInfoStorage.RestoreOriginalConfigJointValuesToAll(interpolate, interpolationDuration);
            }
            else
            {
                initialStateInfoStorage.SetAllConfigJointSlerpPosSpringToValue(target);
            }
        }
    }
}