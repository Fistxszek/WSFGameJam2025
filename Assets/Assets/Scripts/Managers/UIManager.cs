using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [SerializeField] private GameObject _endScreen;
    [SerializeField] private TMP_Text _savedCountTxt;
    [SerializeField] private GameObject DogChad, DogSrednio, DogSlabo;
    [SerializeField] private int GoodScore, MidScore, BadScore;
    private void Awake()
    {
        if (Instance != null)
            Destroy(this);
        Instance = this;
    }

    public void ShowEndScreen()
    {
        AudioManager.Instance.StopAllSounds();
       _endScreen.SetActive(true);
       var score = GameManager.Instance._sheepSaved;
       if (score <= BadScore)
       {
           AudioManager.Instance.PlayOneShot(FMODEvents.Instance.FailureSFX);
           DogSlabo.SetActive(true);
       }
       else if (score > BadScore && score <= MidScore)
       {
           AudioManager.Instance.PlayOneShot(FMODEvents.Instance.SuccesSfx);
           DogSrednio.SetActive(true);
       }
       else if (score > MidScore)
       {
           AudioManager.Instance.PlayOneShot(FMODEvents.Instance.SuccesSfx);
           DogChad.SetActive(true);
       }
       
       _savedCountTxt.SetText("Sheep herded: {0}", GameManager.Instance._sheepSaved);
       Time.timeScale = 0;
    }
}
