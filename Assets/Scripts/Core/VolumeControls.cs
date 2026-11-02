using UnityEngine;
using UnityEngine.Rendering;

public class VolumeControls : MonoBehaviour
{
    void Start()
    {
        // For when it comes to implement volume controls, these are the methods used to control each parameter in FMOD.
        // Each control is defaulted to 0.8, and ranges from 0-1, which can be thought of as percentages. 

        float masterVolume = 0.8f;
        float musicVolume = 0.8f;
        float sfxVolume = 0.8f;

        FMODUnity.RuntimeManager.StudioSystem.setParameterByName("parameter:/Volume_Master", masterVolume);
        FMODUnity.RuntimeManager.StudioSystem.setParameterByName("parameter:/Volume_Music", musicVolume);
        FMODUnity.RuntimeManager.StudioSystem.setParameterByName("parameter:/Volume_SFX", sfxVolume);
    }
}
