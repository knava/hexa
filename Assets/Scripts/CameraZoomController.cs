using UnityEngine;

public class CameraZoomController : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float minZoom = 5f;
    public float maxZoom = 15f;
    public float zoomSpeed = 0.5f;
    public float touchZoomSpeed = 0.1f;

    private Camera mainCamera;
    private float initialDistance;
    private Vector3 initialPosition;

    void Awake()
    {
        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        // Solo permitir zoom durante la fase 1
        if (GameManager.Instance != null && GameManager.Instance.currentPhase != GamePhase.BoardConstruction)
            return;

        // Zoom con scroll de ratón (para testing en editor)
        if (Input.mouseScrollDelta.y != 0)
        {
            float scroll = Input.mouseScrollDelta.y;
            ZoomCamera(scroll * zoomSpeed);
        }

        // Zoom con gesto táctil (pellizco)
        if (Input.touchCount == 2)
        {
            HandleTouchZoom();
        }
    }

    private void HandleTouchZoom()
    {
        Touch touchZero = Input.GetTouch(0);
        Touch touchOne = Input.GetTouch(1);

        if (touchZero.phase == TouchPhase.Began || touchOne.phase == TouchPhase.Began)
        {
            initialDistance = Vector2.Distance(touchZero.position, touchOne.position);
            initialPosition = mainCamera.transform.position;
        }
        else if (touchZero.phase == TouchPhase.Moved || touchOne.phase == TouchPhase.Moved)
        {
            float currentDistance = Vector2.Distance(touchZero.position, touchOne.position);
            float difference = initialDistance - currentDistance;

            ZoomCamera(difference * touchZoomSpeed);
        }
    }

    private void ZoomCamera(float increment)
    {
        float newSize = mainCamera.orthographicSize + increment;
        mainCamera.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
    }

    // Método para resetear la cámara si es necesario
    public void ResetCamera()
    {
        if (mainCamera != null)
        {
            mainCamera.orthographicSize = Mathf.Lerp(minZoom, maxZoom, 0.5f);
        }
    }
}