using UnityEngine;

public class SetCameraForceStateOnEnable : MonoBehaviour
{
    public CameraXRotator cxr;
    public HeadAnimatedRigTwistCamera hartc;
    public bool enableState = true;

    void OnEnable()
    {
        if (cxr != null)
        {
            cxr.enabled = enableState;
        }

        if (hartc != null)
        {
            hartc.enabled = enableState;
        }
    }
}