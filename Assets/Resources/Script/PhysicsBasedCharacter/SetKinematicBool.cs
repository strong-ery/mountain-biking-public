using UnityEngine;

// Alternative: Even more optimized - only runs once per enable
public class SetKinematicOnEnable : MonoBehaviour
{
    public Rigidbody rb;
    public bool target;

    void OnEnable()
    {
        if (rb != null)
            rb.isKinematic = target;
    }
}