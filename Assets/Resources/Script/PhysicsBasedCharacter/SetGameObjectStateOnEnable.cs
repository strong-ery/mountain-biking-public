using UnityEngine;

public class SetGameObjectStateOnEnable : MonoBehaviour
{
    public GameObject gm;
    public bool target;

    void OnEnable()
    {
        if (gm != null)
            gm.SetActive(target);
    }
}