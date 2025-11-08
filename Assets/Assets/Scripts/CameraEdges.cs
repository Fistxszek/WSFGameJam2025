using UnityEngine;

public struct Edges
{
    public float maxLeftX;
    public float maxRightX;
    public float maxTopY;
    public float maxBottomY;
}

public class CameraEdges : MonoBehaviour
{
    public static CameraEdges Instance;
    public Edges CamEdges;

    private void Awake()
    {
        // Fixed singleton pattern
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void CalculateCameraEdges(float camSize, Vector3 camPos)
    {
        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera is missing!");
            return;
        }

        var camHeight = 2f * camSize;
        var camWidth = camHeight * mainCamera.aspect;

        CamEdges.maxLeftX = camPos.x - (camWidth / 2f);
        CamEdges.maxRightX = camPos.x + (camWidth / 2f);
        CamEdges.maxTopY = camPos.y + (camHeight / 2f);
        CamEdges.maxBottomY = camPos.y - (camHeight / 2f);
    }
}