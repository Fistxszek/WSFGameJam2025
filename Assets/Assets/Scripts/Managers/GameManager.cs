using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public NewActions controls;

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
    }

    public void OnPlayAgainBtn()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
    }

    public void OnMainMenuBtn()
    {
        SceneManager.LoadScene(0, LoadSceneMode.Single);
    }
}
