using UnityEngine;
using System.Collections;
using FMODUnity;

public class RandomSoundPlayer : MonoBehaviour
{
    [Header("FMOD Settings")]
    [SerializeField] private float minInterval = 5f;
    [SerializeField] private float maxInterval = 7f;
    [SerializeField] private bool playOnStart = true;
    
    private Coroutine soundCoroutine;
    
    void Start()
    {
        if (playOnStart)
            StartRandomSounds();
    }
    
    public void StartRandomSounds()
    {
        if (soundCoroutine != null)
            StopCoroutine(soundCoroutine);
            
        soundCoroutine = StartCoroutine(PlayRandomSoundsRoutine());
    }
    
    public void StopRandomSounds()
    {
        if (soundCoroutine != null)
        {
            StopCoroutine(soundCoroutine);
            soundCoroutine = null;
        }
    }
    
    private IEnumerator PlayRandomSoundsRoutine()
    {
        while (true)
        {
            // Wait random time between min and max interval
            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);
            
            // Play one-shot sound
            if (!FMODEvents.Instance.OwcaBee.IsNull)
            {
                AudioManager.Instance.PlayOneShot(FMODEvents.Instance.OwcaBee);
            }
        }
    }
    
    void OnDestroy()
    {
        StopRandomSounds();
    }
}

