using System;
using UnityEngine;
using FMODUnity;

public class FMODEvents : MonoBehaviour
{
    [field: Header("Ambience")]
    // [field: SerializeField] public EventReference ambience { get; private set; }
    
    [field: Header("Music")]
    [field: SerializeField] public EventReference MapMusic { get; private set; }
    [field: SerializeField] public EventReference MenuMusic { get; private set; }
    // [field: SerializeField] public EventReference mainMenuMusic { get; private set; }
    
    [field: Header("SFX")]
    // [field: SerializeField] public EventReference mindMapIdeaAddedSound { get; private set; }
    // [field: SerializeField] public EventReference mindMapIdeaSpawnedSound { get; private set; }
    // [field: SerializeField] public EventReference newMessage { get; private set; }
    [field: SerializeField] public EventReference Click { get; private set; }
    [field: SerializeField] public EventReference OwcaBee { get; private set; }
    [field: SerializeField] public EventReference OwcaRun { get; private set; }
    [field: SerializeField] public EventReference DogHau { get; private set; }
    [field: SerializeField] public EventReference DogRun { get; private set; }
    [field: SerializeField] public EventReference KaczkaQuak { get; private set; }
    [field: SerializeField] public EventReference DziadMumble { get; private set; }
    public static FMODEvents Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    
}
