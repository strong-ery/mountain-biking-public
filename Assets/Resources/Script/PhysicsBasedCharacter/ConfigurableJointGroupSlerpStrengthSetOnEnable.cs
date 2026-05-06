using UnityEngine;

public class ConfigurableJointGroupSlerpStrengthSetOnEnable : MonoBehaviour
{
    public ConfigurableJoint[] cjList;
    public float slerpStrength = 1000;

    void OnEnable()
    {
        if (cjList != null)
        {
            foreach (ConfigurableJoint cj in cjList)
            {
                // Create a temporary copy of the drive, modify it, then assign it back
                JointDrive slerpDrive = cj.slerpDrive;
                slerpDrive.positionSpring = slerpStrength;
                cj.slerpDrive = slerpDrive;
            }
        }
    }
}