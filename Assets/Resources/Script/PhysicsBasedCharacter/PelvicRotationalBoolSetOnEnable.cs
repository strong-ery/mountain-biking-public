using System.Data.Common;
using UnityEngine;

// Alternative: Even more optimized - only runs once per enable
public class PelvicRotationalBoolSetOnEnable : MonoBehaviour
{
    public PelvicRotationalLocker prl;
    public GroundingStateManager gsm;
    public COMBalancer COMB;
    public bool target;

    void OnEnable()
    {
        if (prl != null)
            prl.enabled = target;
        if (gsm != null)
            gsm.enabled = target;
        if (COMB != null)
            COMB.enabled = target;
    }
}