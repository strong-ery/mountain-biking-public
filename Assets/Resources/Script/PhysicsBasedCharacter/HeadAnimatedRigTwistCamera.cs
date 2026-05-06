using UnityEngine;

public class HeadAnimatedRigTwistCamera : MonoBehaviour
{
    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 2f;
    public bool invertY = false;
    
    [Header("Y Rotation Inheritance")]
    public Transform yInheritObject = null;
    
    [Header("PID Controller Settings")]
    public float proportionalGain = 50f;    // Much lower values
    public float integralGain = 0f;         // Start with 0
    public float derivativeGain = 5f;       // Lower value
    public float maxTorque = 100f;          // Much lower max torque
    public float dampingForce = 10f;        // Add damping to prevent oscillation
    
    private Rigidbody yInheritRigidbody;
    
    private float xRotation = 0f;
    private float yRotation = 0f;
    private Vector2 targetRotation;
    private Vector2 currentRotation;
    
    // PID Controller variables
    private float previousError = 0f;
    private float integralError = 0f;
    public float targetYRotation = 0f;
    
    void Start()
    {
        Vector3 currentEuler = transform.eulerAngles;
        xRotation = currentEuler.x;
        yRotation = 0f;
        
        if (xRotation > 180f)
            xRotation -= 360f;
            
        targetRotation = new Vector2(xRotation, yRotation);
        currentRotation = targetRotation;
        targetYRotation = yRotation;
        
        if (yInheritObject != null)
        {
            yInheritRigidbody = yInheritObject.GetComponent<Rigidbody>();
            
            if (yInheritRigidbody != null)
            {
                // Add some angular drag to prevent spinning
                // yInheritRigidbody.angularDamping = 2f;
                
                float currentY = yInheritRigidbody.rotation.eulerAngles.y;
                if (currentY > 180f) currentY -= 360f;
                targetYRotation = currentY;
            }
        }
    }
    
    void Update()
    {
        HandleMouseInput();
    }
    
    void FixedUpdate() // Use FixedUpdate for physics
    {
        UpdateYInheritObject();
    }
    
    void HandleMouseInput()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        if (invertY)
            mouseY = -mouseY;
        
        targetYRotation += mouseX;
        
        // Proper angle normalization
        targetYRotation = NormalizeAngle(targetYRotation);
        
        xRotation -= mouseY;
        targetRotation = new Vector2(xRotation, targetYRotation);
    }
    
    void UpdateYInheritObject()
    {
        if (yInheritRigidbody != null)
        {
            float currentY = NormalizeAngle(yInheritRigidbody.rotation.eulerAngles.y);
            
            // Calculate shortest angular distance
            float error = Mathf.DeltaAngle(currentY, targetYRotation);
            
            // Only apply PID if error is significant
            if (Mathf.Abs(error) < 0.1f)
            {
                // Apply damping to stop small oscillations
                Vector3 currentAngularVelocity = yInheritRigidbody.angularVelocity;
                yInheritRigidbody.AddTorque(-currentAngularVelocity.y * dampingForce * Vector3.up, ForceMode.Force);
                return;
            }
            
            // PID calculations
            float proportional = error * proportionalGain;
            
            // Integral with windup protection
            integralError += error * Time.fixedDeltaTime;
            integralError = Mathf.Clamp(integralError, -10f, 10f); // Prevent windup
            float integral = integralError * integralGain;
            
            float derivative = (error - previousError) / Time.fixedDeltaTime;
            derivative *= derivativeGain;
            
            float totalTorque = proportional + integral + derivative;
            totalTorque = Mathf.Clamp(totalTorque, -maxTorque, maxTorque);
            
            // Apply torque
            yInheritRigidbody.AddTorque(Vector3.up * totalTorque, ForceMode.Force);
            
            // Add velocity damping to prevent overshooting
            float angularVelocityY = yInheritRigidbody.angularVelocity.y;
            yInheritRigidbody.AddTorque(Vector3.up * (-angularVelocityY * dampingForce), ForceMode.Force);
            
            previousError = error;
        }
    }
    
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
}