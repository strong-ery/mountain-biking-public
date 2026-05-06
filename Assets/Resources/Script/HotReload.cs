using UnityEngine;
using UnityEngine.SceneManagement;

public class HotReload : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKey(KeyCode.L))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}