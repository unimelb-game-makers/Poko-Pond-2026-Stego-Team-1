using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif


// Rework this, copies func from the wider scene changing systems

public class temp_scene_changer : MonoBehaviour
{

    [SerializeField] private string targetScene;
    public LayerMask PlayerSoftBodyLayer;
#if UNITY_EDITOR
    [SerializeField] private SceneAsset targetSceneAsset;
#endif

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerEnter2D(Collider2D collision) {
        if ((1 << collision.gameObject.layer) == PlayerSoftBodyLayer.value)
        {
            SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
        }
    }
}
