using UnityEngine;

public class AirplaneOutsideView : MonoBehaviour
{
    [Header("Prefab of the indicator")]
    [SerializeField] private GameObject _arrow;
    [Header("Distance from edge")]
    [SerializeField] private float spacing = 0.5f;
    
    private GameObject indicatorObj;
    private bool isOutsideView;
    private Camera mainCam;
    
    private void Start()
    {
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (mainCam == null) return;

        // Get fresh camera bounds every frame
        float camHeight = 2f * mainCam.orthographicSize;
        float camWidth = camHeight * mainCam.aspect;
        Vector3 camPos = mainCam.transform.position;

        float maxLeftX = camPos.x - (camWidth / 2f);
        float maxRightX = camPos.x + (camWidth / 2f);
        float maxTopY = camPos.y + (camHeight / 2f);
        float maxBottomY = camPos.y - (camHeight / 2f);

        // Check if airplane is outside camera bounds
        bool outsideView = transform.position.x < maxLeftX || 
                          transform.position.x > maxRightX ||
                          transform.position.y < maxBottomY || 
                          transform.position.y > maxTopY;

        // Show/hide indicator
        if (outsideView && !isOutsideView)
        {
            ShowIndicator();
        }
        else if (!outsideView && isOutsideView)
        {
            HideIndicator();
        }

        // Update indicator position and rotation
        if (isOutsideView && indicatorObj != null)
        {
            UpdateIndicator(maxLeftX, maxRightX, maxTopY, maxBottomY, camPos);
        }
    }

    private void ShowIndicator()
    {
        if (indicatorObj == null)
        {
            indicatorObj = Instantiate(_arrow);
        }
        isOutsideView = true;
    }

    private void HideIndicator()
    {
        if (indicatorObj != null)
        {
            Destroy(indicatorObj);
            indicatorObj = null;
        }
        isOutsideView = false;
    }

    private void UpdateIndicator(float maxLeftX, float maxRightX, float maxTopY, float maxBottomY, Vector3 camPos)
    {
        Vector3 targetPos = transform.position;
        
        // Screen center
        Vector3 screenCenter = camPos;

        // Direction from center to target
        Vector3 direction = (targetPos - screenCenter).normalized;

        // Screen half-extents
        float halfWidth = (maxRightX - maxLeftX) / 2f;
        float halfHeight = (maxTopY - maxBottomY) / 2f;

        // Calculate position on screen edge
        Vector3 indicatorPos = screenCenter;
        float angle = Mathf.Atan2(direction.y, direction.x);
        
        // Determine which edge to clamp to
        float tan = direction.y / direction.x;
        
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            // Left or right edge
            if (direction.x > 0)
            {
                indicatorPos.x = maxRightX - spacing;
                indicatorPos.y = screenCenter.y + tan * (halfWidth - spacing);
            }
            else
            {
                indicatorPos.x = maxLeftX + spacing;
                indicatorPos.y = screenCenter.y + tan * (-halfWidth + spacing);
            }
        }
        else
        {
            // Top or bottom edge
            if (direction.y > 0)
            {
                indicatorPos.y = maxTopY - spacing;
                indicatorPos.x = screenCenter.x + (halfHeight - spacing) / tan;
            }
            else
            {
                indicatorPos.y = maxBottomY + spacing;
                indicatorPos.x = screenCenter.x + (-halfHeight + spacing) / tan;
            }
        }

        // Clamp to ensure it stays on screen
        indicatorPos.x = Mathf.Clamp(indicatorPos.x, maxLeftX + spacing, maxRightX - spacing);
        indicatorPos.y = Mathf.Clamp(indicatorPos.y, maxBottomY + spacing, maxTopY - spacing);
        indicatorPos.z = 0;

        // Apply position
        indicatorObj.transform.position = indicatorPos;

        // Rotate arrow toward target
        float rotationAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        indicatorObj.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);
    }

    private void OnDestroy()
    {
        if (indicatorObj != null)
        {
            Destroy(indicatorObj);
        }
    }
}
