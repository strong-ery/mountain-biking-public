using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SelectiveAngularDamping : MonoBehaviour
{
    [Header("Angular Damping Settings")]
    [SerializeField, Range(0f, 10000f)] private float xAxisDampingForce = 10f;
    [SerializeField, Range(0f, 10000f)] private float yAxisDampingForce = 10f;
    [SerializeField, Range(0f, 10000f)] private float zAxisDampingForce = 10f;
    
    [Header("Options")]
    [SerializeField] private bool useLocalSpace = true;
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField, Range(0f, 10000f)] private float maxDampingTorque = 50f;
    [SerializeField] private ForceMode forceMode = ForceMode.Force;
    
    private Rigidbody rb;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    
    void FixedUpdate()
    {
        ApplySelectiveAngularDamping();
    }
    
    private void ApplySelectiveAngularDamping()
    {
        Vector3 angularVelocity = rb.angularVelocity;
        
        if (angularVelocity.magnitude < 0.01f) return; // Skip if barely rotating
        
        Vector3 dampingTorque = Vector3.zero;
        
        if (useLocalSpace)
        {
            // Convert angular velocity to local space
            Vector3 localAngularVelocity = transform.InverseTransformDirection(angularVelocity);
            
            // Calculate damping torque in local space
            dampingTorque.x = -localAngularVelocity.x * xAxisDampingForce;
            dampingTorque.y = -localAngularVelocity.y * yAxisDampingForce;
            dampingTorque.z = -localAngularVelocity.z * zAxisDampingForce;
            
            // Convert back to world space
            dampingTorque = transform.TransformDirection(dampingTorque);
        }
        else
        {
            // Apply damping in world space
            dampingTorque.x = -angularVelocity.x * xAxisDampingForce;
            dampingTorque.y = -angularVelocity.y * yAxisDampingForce;
            dampingTorque.z = -angularVelocity.z * zAxisDampingForce;
        }
        
        // Clamp the torque to prevent instability
        if (dampingTorque.magnitude > maxDampingTorque)
        {
            dampingTorque = dampingTorque.normalized * maxDampingTorque;
        }
        
        // Apply the damping torque
        rb.AddTorque(dampingTorque, forceMode);
        
        if (showDebugInfo)
        {
            Debug.Log($"Angular Velocity: {angularVelocity.magnitude:F2}, Damping Torque: {dampingTorque.magnitude:F2}");
        }
    }
    
    // Public methods to modify damping at runtime
    public void SetXAxisDamping(float dampingForce)
    {
        xAxisDampingForce = Mathf.Max(0f, dampingForce);
    }
    
    public void SetYAxisDamping(float dampingForce)
    {
        yAxisDampingForce = Mathf.Max(0f, dampingForce);
    }
    
    public void SetZAxisDamping(float dampingForce)
    {
        zAxisDampingForce = Mathf.Max(0f, dampingForce);
    }
    
    public void SetAllAxisDamping(float dampingForce)
    {
        float clampedForce = Mathf.Max(0f, dampingForce);
        xAxisDampingForce = clampedForce;
        yAxisDampingForce = clampedForce;
        zAxisDampingForce = clampedForce;
    }
    
    public void SetDamping(Vector3 dampingForces)
    {
        xAxisDampingForce = Mathf.Max(0f, dampingForces.x);
        yAxisDampingForce = Mathf.Max(0f, dampingForces.y);
        zAxisDampingForce = Mathf.Max(0f, dampingForces.z);
    }
    
    // Alternative method using exponential damping coefficient
    public void SetDampingCoefficient(float coefficient)
    {
        // Convert damping coefficient (0-1) to force values
        float forceValue = coefficient * 50f; // Scale as needed
        SetAllAxisDamping(forceValue);
    }
    
    // Getters
    public Vector3 GetDampingForces()
    {
        return new Vector3(xAxisDampingForce, yAxisDampingForce, zAxisDampingForce);
    }
    
    // Method to temporarily disable damping (useful for ragdoll death states)
    public void SetDampingEnabled(bool enabled)
    {
        this.enabled = enabled;
    }
}