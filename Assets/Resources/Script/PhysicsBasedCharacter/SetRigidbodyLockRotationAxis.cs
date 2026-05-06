using UnityEngine;

public class SetRigidbodyLockRotationAxis : MonoBehaviour
{
    public Rigidbody rb;
    public bool lockX;
    public bool lockY;
    public bool lockZ;

    void OnEnable()
    {
        if (rb != null)
        {
            RigidbodyConstraints constraints = RigidbodyConstraints.None;

            // Add rotation constraints based on selected axes
            if (lockX)
                constraints |= RigidbodyConstraints.FreezeRotationX;
            if (lockY)
                constraints |= RigidbodyConstraints.FreezeRotationY;
            if (lockZ)
                constraints |= RigidbodyConstraints.FreezeRotationZ;

            rb.constraints = constraints;
        }
    }
}