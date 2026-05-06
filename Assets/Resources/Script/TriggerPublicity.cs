using UnityEngine;

public class TriggerPublicity : MonoBehaviour
{
    public bool intersecting;

    void Start()
    {
        MeshCollider ms = GetComponent<MeshCollider>();
        // Ensure the collider is set as a trigger
        ms.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if we hit the ground (you can use tags, layer, or name)
        if (other.CompareTag("Ground"))
        {
            intersecting = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Check if we left the ground
        if (other.CompareTag("Ground"))
        {
            intersecting = false;
        }
    }
}