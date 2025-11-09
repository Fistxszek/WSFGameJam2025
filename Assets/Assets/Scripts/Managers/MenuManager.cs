using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private GameObject _creditsObj;
    private void Start()
    {
        AudioManager.Instance.InitializeMusic(FMODEvents.Instance.MenuMusic);
    }

    public void OnStartGameBtn()
    {
        AudioManager.Instance.StopAllSounds();
        SceneManager.LoadScene(1, LoadSceneMode.Single);
    }
    
    public void OnCreditsBtn()
    {
        _creditsObj.SetActive(!_creditsObj.activeSelf);
    }

    public void OnButtonExit()
    {
        Application.Quit();
    }
}
