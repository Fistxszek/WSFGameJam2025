using System.Collections;
using UnityEngine;
using FMODUnity;

public class FMODBankLoader : MonoBehaviour
{
    IEnumerator Start()
    {
        // Load Master Bank
        RuntimeManager.LoadBank("Master", true);
        
        // Wait until bank is loaded
        while (!RuntimeManager.HasBankLoaded("Master"))
        {
            yield return null;
        }
    }

    public void LoadMenu()
    {
        // Now load your main scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(1);
    }
}
