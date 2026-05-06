using UnityEngine;

public class ModifyTransformOnEnable : MonoBehaviour
{
    public Transform targetTransform;  // Renamed to avoid shadowing
    public Vector3 posOffset;

    void OnEnable()
    {
        if (targetTransform != null)
        {
            targetTransform.position += posOffset;
        }
    }
}