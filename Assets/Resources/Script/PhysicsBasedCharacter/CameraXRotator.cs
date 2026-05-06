using UnityEngine;

public class CameraXRotator : MonoBehaviour
{
    [Header("X Rotation Limits")]
    [SerializeField] private float minXRotation = -90f;
    [SerializeField] private float maxXRotation = 90f;
    
    [Header("Input Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private bool invertY = false;
    
    public float currentXRotation = 0f;
    
    void Start()
    {
        // Initialize with current X rotation
        currentXRotation = transform.localEulerAngles.x;
        
        // Handle angle wrapping (Unity returns 0-360, we want -180 to 180)
        if (currentXRotation > 180f)
            currentXRotation -= 360f;
    }
    
    void Update()
    {
        // Get mouse input
        float mouseY = Input.GetAxis("Mouse Y");
        
        // Apply sensitivity and invert if needed
        mouseY *= mouseSensitivity;
        if (invertY)
            mouseY = -mouseY;
        
        // Update X rotation
        currentXRotation -= mouseY;
        
        // Clamp to limits
        currentXRotation = Mathf.Clamp(currentXRotation, minXRotation, maxXRotation);
    }
    
    void LateUpdate()
    {
        // Apply only X rotation, preserving Y and Z
        Vector3 currentRotation = transform.localEulerAngles;
        transform.localEulerAngles = new Vector3(currentXRotation, currentRotation.y, currentRotation.z);
    }
}