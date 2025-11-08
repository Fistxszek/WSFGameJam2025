using System;
using System.Collections;
using System.Collections.Generic;
using FMOD.Studio;
using UnityEngine;
using FMODUnity;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private List<EventInstance> eventInstances;
    
    private EventInstance ambienceEventInstance;
    private EventInstance voEventInstance;
    private Coroutine _voCoroutine;
    [SerializeField, Range(0.1f, 1.5f)] private float _voDelayTime;
    public EventInstance musicEventInstance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        eventInstances = new List<EventInstance>();
        DontDestroyOnLoad(gameObject);
    }

    public void PlayOneShoot(EventReference sfx)
    {
        RuntimeManager.PlayOneShot(sfx);
    }
    public void InitializeSceneAudio()
    {
        // InitializeAmbience(FMODEvents.Instance.ambience);
        InitializeMusic(FMODEvents.Instance.MapMusic);
    }

    public void PlayOneShot(EventReference sound)
    {
        RuntimeManager.PlayOneShot(sound);
    }

    private void InitializeAmbience(EventReference ambienceEventReference)
    {
        ambienceEventInstance = CreateInstance(ambienceEventReference);
        ambienceEventInstance.start();
    }
    public void InitializeMusic(EventReference musicEventReference)
    {
        musicEventInstance = CreateInstance(musicEventReference);
        musicEventInstance.start();
        musicEventInstance.setParameterByName("music-intensity", 1);
    }

    public void SetAmbienceParameter(string parameterName, float parameterValue)
    {
        ambienceEventInstance.setParameterByName(parameterName, parameterValue);
    }
    public void SetMusicParameter(string parameterName, float parameterValue)
    {
        musicEventInstance.setParameterByName(parameterName, parameterValue);
    }

    public EventInstance CreateInstance(EventReference eventReference)
    {
        EventInstance eventInstance = RuntimeManager.CreateInstance(eventReference);
        eventInstances.Add(eventInstance);
        return eventInstance;
    }
}
