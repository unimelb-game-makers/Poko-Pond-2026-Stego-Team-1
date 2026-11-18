using UnityEngine;


// Makes a request to change background music on the loading of a new scene

public class BackgroundMusic : MonoBehaviour
{
    [Header("Background Music Track")]
    [Tooltip("The name of the BGM Event")]
    [SerializeField] private string backgroundMusic;

    private void Awake()
    {
        if (backgroundMusic == "Ending") AudioManager.TriggerEnding();
        else AudioManager.SetBackgroundMusic(backgroundMusic);
    }

    private void OnDestroy()
    {
        AudioManager.StopAllSFX();
    }

    private void OnApplicationQuit()
    {
        AudioManager.StopAllSound();
    }
}