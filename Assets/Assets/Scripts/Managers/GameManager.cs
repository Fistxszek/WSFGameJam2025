using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public NewActions controls;

    public int _sheepSaved = 0;

    private void Awake()
    {
        if (Instance != null)
                Destroy(this);
        Instance = this;

        controls = new NewActions();
    }
    
    private void OnEnable()
    {
        controls?.Enable();
    }
    
    private void OnDisable()
    {
        controls?.Disable();
    }

    private void Start()
    {
       TimerManager.Instance.StartTimer(); 
       AudioManager.Instance.InitializeSceneAudio();
    }

    public void OnPlayAgainBtn()
    {
        Time.timeScale = 1f;
        AudioManager.Instance.PlayOneShoot(FMODEvents.Instance.Click);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
    }

    public void OnMainMenuBtn()
    {
        Time.timeScale = 1f;
        AudioManager.Instance.PlayOneShoot(FMODEvents.Instance.Click);
        SceneManager.LoadScene(1, LoadSceneMode.Single);
    }
}
