using UnityEngine;

namespace rayzngames
{
    public class IfXIsNear90AndInsufficientSpeedThenRagdoll : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BicycleVehicle bicycleVehicle;
        
        [Header("Ragdoll Conditions")]
        [SerializeField] private float angleThreshold = 1f; // Within 1 degree of 90
        [SerializeField] private float minimumSpeed = 5f; // Minimum speed to avoid ragdoll
        [SerializeField] private float checkDelay = 0.1f; // How often to check (seconds)
        [SerializeField] private float sustainedTime = 0.5f; // How long condition must be true
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        
        private float lastCheckTime = 0f;
        private float conditionMetTime = 0f;
        private bool conditionCurrentlyMet = false;
        
        void Start()
        {
            // Auto-find BicycleVehicle if not assigned
            if (bicycleVehicle == null)
            {
                bicycleVehicle = GetComponent<BicycleVehicle>();
                if (bicycleVehicle == null)
                {
                    bicycleVehicle = FindObjectOfType<BicycleVehicle>();
                }
                
                if (bicycleVehicle == null)
                {
                    Debug.LogError("IfXIsNear90AndInsufficientSpeedThenRagdoll: No BicycleVehicle found!");
                    enabled = false;
                    return;
                }
            }
        }
        
        void Update()
        {
            // Only check at specified intervals
            if (Time.time - lastCheckTime < checkDelay)
                return;
                
            lastCheckTime = Time.time;
            
            // Skip if already ragdolled
            if (bicycleVehicle.GetComponent<Rigidbody>().constraints == RigidbodyConstraints.None)
                return;
            
            CheckRagdollCondition();
        }
        
        private void CheckRagdollCondition()
        {
            // Get current rotation (X-axis)
            float xRotation = transform.eulerAngles.x;
            
            // Normalize angle to -180 to 180 range
            if (xRotation > 180f)
                xRotation -= 360f;
            
            // Get current speed
            float currentSpeed = bicycleVehicle.currentSpeed;
            
            // Check if X rotation is within threshold of 90 or -90 degrees
            bool nearUpsideDown = (Mathf.Abs(Mathf.Abs(xRotation) - 90f) <= angleThreshold);
            
            // Check if speed is insufficient
            bool insufficientSpeed = currentSpeed < minimumSpeed;
            
            // Both conditions must be true
            bool shouldRagdoll = nearUpsideDown && insufficientSpeed;
            
            if (debugMode)
            {
                Debug.Log($"X Rotation: {xRotation:F2}°, Speed: {currentSpeed:F2}, Near 90°: {nearUpsideDown}, Insufficient Speed: {insufficientSpeed}");
            }
            
            if (shouldRagdoll)
            {
                if (!conditionCurrentlyMet)
                {
                    // Condition just became true
                    conditionCurrentlyMet = true;
                    conditionMetTime = 0f;
                    
                    if (debugMode)
                        Debug.Log("Ragdoll condition met, starting timer...");
                }
                
                conditionMetTime += checkDelay;
                
                // Check if condition has been sustained long enough
                if (conditionMetTime >= sustainedTime)
                {
                    TriggerRagdoll();
                }
            }
            else
            {
                // Reset condition tracking
                if (conditionCurrentlyMet)
                {
                    conditionCurrentlyMet = false;
                    conditionMetTime = 0f;
                    
                    if (debugMode)
                        Debug.Log("Ragdoll condition no longer met, resetting timer.");
                }
            }
        }
        
        private void TriggerRagdoll()
        {
            if (debugMode)
                Debug.Log("Triggering ragdoll due to insufficient speed while nearly upside down!");
            
            // Access the TriggerRagdoll method through reflection since it's private
            // Or we can try to access it if it becomes public
            var bicycleType = bicycleVehicle.GetType();
            var ragdollMethod = bicycleType.GetMethod("TriggerRagdoll", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (ragdollMethod != null)
            {
                ragdollMethod.Invoke(bicycleVehicle, new object[] { 0f }); // Force value
            }
            else
            {
                Debug.LogError("Could not find TriggerRagdoll method!");
            }
            
            // Reset tracking
            conditionCurrentlyMet = false;
            conditionMetTime = 0f;
        }
        
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || bicycleVehicle == null)
                return;
            
            // Visualize the angle check
            Gizmos.color = conditionCurrentlyMet ? Color.red : Color.green;
            
            // Draw a line showing current X rotation
            Vector3 center = transform.position;
            Vector3 forward = transform.forward;
            Vector3 rotatedVector = Quaternion.AngleAxis(transform.eulerAngles.x, transform.right) * forward;
            
            Gizmos.DrawLine(center, center + rotatedVector * 2f);
            
            // Draw reference lines for 90 degree thresholds
            Gizmos.color = Color.yellow;
            Vector3 up90 = Quaternion.AngleAxis(90f, transform.right) * forward;
            Vector3 down90 = Quaternion.AngleAxis(-90f, transform.right) * forward;
            
            Gizmos.DrawLine(center, center + up90 * 1.5f);
            Gizmos.DrawLine(center, center + down90 * 1.5f);
        }
    }
}