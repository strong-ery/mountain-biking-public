using UnityEngine;

public class RagdollRigManager : MonoBehaviour
{
    public Rigidbody[] rbList;
    public Rigidbody root;
    public Camera camera;
    public GameObject head;
    public GameObject body;
    public GameObject[] materialSwapObjects;
    public TransformCopyManager tcm;

    public bool isRagdolled = false;

    void Start()
    {
        tcm.enabled = true;
    }

    public void RequestRagdoll(PlayerConfigData pcd, Rigidbody rb, Camera cam, GameObject disableObject)
    {

        foreach (Rigidbody rB in rbList)
        {
            rB.isKinematic = false;
        }

        root.transform.position = rb.transform.position;
        root.transform.rotation = rb.transform.rotation;
        root.linearVelocity = rb.linearVelocity;
        root.angularVelocity = rb.angularVelocity;

        foreach (GameObject go in materialSwapObjects)
        {
            go.GetComponent<SkinnedMeshRenderer>().material = pcd.playerMat;
        }

        cam.enabled = false;
        cam.gameObject.SetActive(false);
        camera.enabled = true;

        disableObject.SetActive(false);
        tcm.enabled = false;
        root.gameObject.SetActive(true);
        head.SetActive(true);
        body.SetActive(true);

        isRagdolled = true;
    }
}
