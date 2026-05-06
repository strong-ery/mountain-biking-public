using UnityEngine;

public class SetColliderStateOnEnable : MonoBehaviour
{
    [Header("Collider Settings")]
    public Collider targetCollider;
    public bool enableState = true;

    void OnEnable()
    {
        if (targetCollider != null)
        {
            targetCollider.enabled = enableState;
        }
    }
}