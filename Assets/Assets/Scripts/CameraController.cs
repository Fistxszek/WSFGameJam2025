using UnityEngine;

public class CameraController : MonoBehaviour
{
    private Camera cam;
    
    private void Start()
    {
        cam = GetComponent<Camera>();
        UpdateCameraEdges();
    }

    private void Update()
    {
        // Update if camera moves or zooms
        UpdateCameraEdges();
    }

    private void UpdateCameraEdges()
    {
        if (CameraEdges.Instance != null && cam != null)
        {
            CameraEdges.Instance.CalculateCameraEdges(cam.orthographicSize, transform.position);
        }
    }
}

