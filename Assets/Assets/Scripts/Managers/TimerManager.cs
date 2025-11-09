using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class TimerManager : MonoBehaviour
{
    public static TimerManager Instance;

    [Header("Timer Settings")]
    [SerializeField] private float gameDuration = 300f; // 5 minutes default
    
    [SerializeField] private TMP_Text _timerText;
    
    [Header("Timer Events")]
    public UnityEvent OnTimerStart;
    public UnityEvent OnTimerEnd;
    public UnityEvent<float> OnTimerTick; // Passes remaining time
    
    private float timeRemaining;
    private bool timerIsRunning = false;
    
    public float TimeRemaining => timeRemaining;
    public float GameDuration => gameDuration;
    public bool IsRunning => timerIsRunning;
    
    // Returns time as percentage (0-1)
    public float TimeRemainingNormalized => timeRemaining / gameDuration;

    private void Awake()
    {
        // Fixed singleton pattern
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        timeRemaining = gameDuration;
    }

    private bool _isPlayingSfx = false;
    private void Update()
    {
        if (timerIsRunning)
        {
            if (timeRemaining > 0)
            {
                timeRemaining -= Time.deltaTime;
                OnTimerTick?.Invoke(timeRemaining);
                if (timeRemaining <= 10 && _isPlayingSfx)
                {
                   AudioManager.Instance.PlayOneShot(FMODEvents.Instance.CzasTykanie);
                   _isPlayingSfx = true;
                }
            }
            else
            {
                timeRemaining = 0;
                timerIsRunning = false;
                OnTimerEnd?.Invoke();
            }
            _timerText.SetText(GetFormattedTime());
        }
    }

    public void StartTimer()
    {
        timerIsRunning = true;
        OnTimerStart?.Invoke();
    }
    
    public void StartTimer(float duration)
    {
        gameDuration = duration;
        timeRemaining = duration;
        StartTimer();
    }

    public void PauseTimer()
    {
        timerIsRunning = false;
    }

    public void ResumeTimer()
    {
        if (timeRemaining > 0)
            timerIsRunning = true;
    }

    public void ResetTimer()
    {
        timeRemaining = gameDuration;
        timerIsRunning = false;
    }

    public void AddTime(float seconds)
    {
        timeRemaining = Mathf.Min(timeRemaining + seconds, gameDuration);
    }

    public void RemoveTime(float seconds)
    {
        timeRemaining = Mathf.Max(timeRemaining - seconds, 0);
    }

    // Returns formatted time string (MM:SS)
    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(timeRemaining / 60);
        int seconds = Mathf.FloorToInt(timeRemaining % 60);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
    
    // Returns formatted time string with milliseconds (MM:SS:MS)
    public string GetFormattedTimeWithMilliseconds()
    {
        int minutes = Mathf.FloorToInt(timeRemaining / 60);
        int seconds = Mathf.FloorToInt(timeRemaining % 60);
        int milliseconds = Mathf.FloorToInt((timeRemaining * 100) % 100);
        return string.Format("{0:00}:{1:00}:{2:00}", minutes, seconds, milliseconds);
    }
}

