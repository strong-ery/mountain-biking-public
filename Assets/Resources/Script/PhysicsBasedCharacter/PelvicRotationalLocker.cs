using UnityEngine;

public class PelvicRotationalLocker : MonoBehaviour
{
    public GameObject stabilizeObject;
    public bool lockX;
    public bool lockY;
    public bool lockZ;

    void FixedUpdate() // Physics timestep
    {
        Rigidbody rb = stabilizeObject.GetComponent<Rigidbody>();
        Vector3 angularVel = rb.angularVelocity;
        
        if (lockX) angularVel.x = 0;
        if (lockY) angularVel.y = 0;
        if (lockZ) angularVel.z = 0;
        
        rb.angularVelocity = angularVel;
    }
}