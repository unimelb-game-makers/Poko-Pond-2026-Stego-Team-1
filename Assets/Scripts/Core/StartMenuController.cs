using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuController : MonoBehaviour
{
    [Header("Scene Navigation")]
    [Tooltip("Type the exact name of your gameplay scene here.")]
    [SerializeField] private string sceneToLoad;

    public void PlayGame()
    {
        // Safety check to ensure you didn't leave the Inspector box blank
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogError("Error: You forgot to type the scene name in the Inspector!");
        }
    }

    public void QuitGame()
    {
        // This log proves the button works while testing in the Editor
        Debug.Log("Quit Game triggered! (The app will close in the final build)");
        Application.Quit();
    }
}