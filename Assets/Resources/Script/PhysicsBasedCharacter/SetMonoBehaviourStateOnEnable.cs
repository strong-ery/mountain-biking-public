using UnityEngine;

public class SetMonoBehaviourStateOnEnable : MonoBehaviour
{
    [Header("MonoBehaviour Settings")]
    public MonoBehaviour targetMonoBehaviour;
    public bool enableState = true;

    void OnEnable()
    {
        if (targetMonoBehaviour != null)
        {
            targetMonoBehaviour.enabled = enableState;
        }
    }
}