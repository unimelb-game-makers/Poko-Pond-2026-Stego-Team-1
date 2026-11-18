using static FMODUnity.RuntimeManager;
using FMOD.Studio;
using UnityEngine;
using FMODUnityResonance;

// This script provides interfacing with FMOD

// Background Music Events - event:/BGM/
// Level
// StartMenu

// Sound Effects - event:/SFX/
// Big_Jump
// Blower
// Crusher
// Death
// Dialogue_Beep
// Door_Close
// Door_Open
// Drop_Merge
// Drop_Split
// Evaporator
// Freeze
// Humidifier
// Land
// Pressure_Plate
// Small_Jump
// Split_Machine
// UI_Button
// UI_Button_Simple
// Water_Battery
// Zap

// Global Parameters
// Volume_Master    - 0-1 (0.8 Default)
// Volume_Music     - 0-1 (0.8 Default)
// Volume_SFX       - 0-1 (0.8 Default)
// Wind_Strength    - 0-50
// State            - Liquid(0), Solid(1)
// Level            - Switch to 0 for Level 1

// Crusher Parameter
// Attenuation - 0-24 distance from camera




public static class AudioManager
{
    private static string currentBackgroundMusic;
    private static EventInstance backgroundMusicInstance;
    
    // This function handles backgroundMusic from level to level
    public static void SetBackgroundMusic(string track)
    {
        // special ending doesn't revert continues with ending track
        if (track == "StartMenu" & currentBackgroundMusic == "Ending") {
            currentBackgroundMusic = track;
            SetParameter("Ending", 0);
        }
        else if ((currentBackgroundMusic != track) || !backgroundMusicInstance.isValid()) ChangeBackgroundMusic(track);
    }

    private static void ChangeBackgroundMusic(string track)
    {
        // if there is an existing track, stop it
        if (backgroundMusicInstance.isValid())
        {
            backgroundMusicInstance.stop(STOP_MODE.IMMEDIATE);
            backgroundMusicInstance.release();
        }
        
        // Initialise and run the new music track
        backgroundMusicInstance = CreateInstance(PathToEventReference($"event:/BGM/{track}"));
        backgroundMusicInstance.start();

        Debug.Log("Current Track: " + track);

        currentBackgroundMusic = track;
    }

    public static void TriggerEnding()
    {
        SetBackgroundMusic("Level");
        SetParameter("Ending", 1);
        currentBackgroundMusic = "Ending";
    }

    // Volume controls should be a value between 0-1, 0.8 is the default
    public static void SetMasterVolume(float state)
    {
        StudioSystem.setParameterByName("Volume_Master", state);
    }

    public static void SetSFXVolume(float state)
    {
        StudioSystem.setParameterByName("Volume_SFX", state);
    }

    public static void SetMusicVolume(float state)
    {
        StudioSystem.setParameterByName("Volume_Music", state);
    }


    // Other global parameter controls can be changed here
    public static void SetParameter(string parameterName, float state)
    {
        StudioSystem.setParameterByName(parameterName, state);
    }

    public static void SetParameter(string parameterName, int state)
    {
        StudioSystem.setParameterByName(parameterName, state);
    }


    // One shot playback has option for parameter setting for instance specific parameters
    public static void SFXOneShot(string sfxName)
    {
        PlayOneShot($"event:/SFX/{sfxName}");
    }

    public static void SFXOneShot(string sfxName, string parameter, float value)
    {
        EventInstance sfxInstance = CreateInstance(PathToEventReference($"event:/SFX/{sfxName}"));
        sfxInstance.setParameterByName(parameter, value);
        sfxInstance.start();
        sfxInstance.release();
    }

    private static Bus sfxBus = GetBus("bus:/SFXUI/SFX");
    
    
    // Called on unloading of scene to stop any extra sound effects
    public static void StopAllSFX()
    {
        sfxBus.stopAllEvents(STOP_MODE.IMMEDIATE);
    }

    private static Bus masterBus = GetBus("bus:/");
    public static void StopAllSound()
    {
        masterBus.stopAllEvents(STOP_MODE.IMMEDIATE);
        backgroundMusicInstance.release();
    }
}