using UnityEngine;

public class LookAndInteract : MonoBehaviour
{
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] public KeyCode interactKey = KeyCode.E;
    [SerializeField] private LayerMask interactLayer;
    [SerializeField] private Camera playerCamera;

    private void Update()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.green);

       
    }
}
