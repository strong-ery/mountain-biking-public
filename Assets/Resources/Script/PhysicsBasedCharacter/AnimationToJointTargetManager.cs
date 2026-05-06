using UnityEngine;

public class AnimationToJointTargetManager : MonoBehaviour
{   
    [System.Serializable]
    public struct AnimatedJointTargetPair
    {
        public GameObject animatedObject;
        public ConfigurableJoint targetingJoint;
    }

    public AnimatedJointTargetPair[] jointTargetPairs;

    [Header("Configuration")]
    public bool useWorldSpace = false;  // Set this to match your setup
    
    // Cache the starting rotations for each joint
    private Quaternion[] startRotations;

    void Start()
    {
        // Initialize the starting rotations array
        startRotations = new Quaternion[jointTargetPairs.Length];
        
        for (int i = 0; i < jointTargetPairs.Length; i++)
        {
            var joint = jointTargetPairs[i].targetingJoint;
            
            if (joint == null)
            {
                Debug.LogError($"Joint at index {i} is null!", this);
                continue;
            }
            
            // Set up the joint as a character joint with specified coordinate space
            joint.SetupAsCharacterJoint(useWorldSpace);
            
            // Cache the starting rotation based on joint's world space configuration
            if (joint.configuredInWorldSpace)
            {
                startRotations[i] = joint.transform.rotation;
            }
            else
            {
                startRotations[i] = joint.transform.localRotation;
            }
        }
    }

    // Use LateUpdate to ensure animations have finished
    void LateUpdate()
    {
        for (int i = 0; i < jointTargetPairs.Length; i++)
        {
            var pair = jointTargetPairs[i];
            
            if (pair.animatedObject == null || pair.targetingJoint == null)
                continue;
            
            var joint = pair.targetingJoint;
            
            // Set target rotation based on joint's world space configuration
            if (joint.configuredInWorldSpace)
            {
                joint.SetTargetRotation(pair.animatedObject.transform.rotation, startRotations[i]);
            }
            else
            {
                joint.SetTargetRotationLocal(pair.animatedObject.transform.localRotation, startRotations[i]);
            }
        }
    }
}