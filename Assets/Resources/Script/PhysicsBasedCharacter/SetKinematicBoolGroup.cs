using UnityEngine;

// Alternative: Even more optimized - only runs once per enable
public class SetKinematicOnEnableInGroup : MonoBehaviour
{
    public Rigidbody[] rb;
    public bool target;

    void OnEnable()
    {
        if (rb != null)
        {
            foreach (Rigidbody r in rb)
            {
                r.isKinematic = target;
            }
        }
    }
}