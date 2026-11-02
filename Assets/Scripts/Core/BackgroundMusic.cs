using UnityEngine;
using UnityEngine.SceneManagement;

// Manages the background audio, ensuring it 
// 

public class BackgroundMusic : MonoBehaviour
{
    private GameObject[] musicObjects;

    // Check for Existing Background music and ensuring additional layers aren't created
    private bool MusicCheck()
    {
        musicObjects = GameObject.FindGameObjectsWithTag("Music");

        if (musicObjects.Length > 1) Destroy(this.gameObject);
        return !(musicObjects.Length > 1);
    }

    // Stops music when return to menu
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "StartMenu") Destroy(this.gameObject);
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded; 
        
        // Check for Duplicate BGM
        MusicCheck();

        DontDestroyOnLoad(this.gameObject);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}