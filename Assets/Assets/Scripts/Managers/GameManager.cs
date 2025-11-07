using System;
using UnityEngine;

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
}
