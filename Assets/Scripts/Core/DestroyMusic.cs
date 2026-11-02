using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DestroyMusic : MonoBehaviour
{

    // Update is called once per frame
    void Update()
    {
        if (SceneManager.GetActiveScene().name == "StartMenu")
        {
            Destroy(this.gameObject);
        }
    }
}
