using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private GameObject _creditsObj;
    private void Start()
    {
        AudioManager.Instance.InitializeMusic(FMODEvents.Instance.MenuMusic);
        AudioManager.Instance.PlayOneShoot(FMODEvents.Instance.Click);
    }

    public void OnStartGameBtn()
    {
        AudioManager.Instance.PlayOneShoot(FMODEvents.Instance.Click);
        AudioManager.Instance.StopAllSounds();
        SceneManager.LoadScene(2, LoadSceneMode.Single);
    }
    
    public void OnCreditsBtn()
    {
        AudioManager.Instance.PlayOneShoot(FMODEvents.Instance.Click);
        _creditsObj.SetActive(!_creditsObj.activeSelf);
    }

    public void OnButtonExit()
    {
        AudioManager.Instance.PlayOneShoot(FMODEvents.Instance.Click);
        Application.Quit();
    }
}
