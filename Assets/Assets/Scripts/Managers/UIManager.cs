using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [SerializeField] private GameObject _endScreen;
    private void Awake()
    {
        if (Instance != null)
            Destroy(this);
        Instance = this;
    }

    public void ShowEndScreen()
    {
       _endScreen.SetActive(true);
       Time.timeScale = 0;
    }
}
