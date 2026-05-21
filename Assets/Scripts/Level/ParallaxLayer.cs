using UnityEngine;

[ExecuteAlways]
public class ParallaxLayer : MonoBehaviour
{
    [Header("Parallax")]
    [Tooltip("0 = se mueve con el mundo (sin parallax, capa pegada). 1 = anclada a la cámara (cielo a infinito).")]
    [Range(0f, 1f)]
    [SerializeField] private Vector2 parallaxFactor = new Vector2(0.5f, 0.5f);

    [Tooltip("Si está vacío usa Camera.main.")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("En Y normalmente queremos parallax suave o nulo (true = aplica factor.y). Si false, solo X se parallaxea.")]
    [SerializeField] private bool applyY = true;

    private Vector3 startLayerPos;
    private Vector3 startCameraPos;
    private bool initialized;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private void OnEnable()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        initialized = false; // forzar re-anchor en primer LateUpdate (después de todos los Start)
    }

    /// Re-ancla la capa: la posición y cámara actuales se vuelven el "cero" del parallax.
    /// Llamar después de un cambio de cuarto (CameraController) si la capa es por-cuarto.
    public void CaptureAnchor()
    {
        if (cameraTransform == null) return;
        startLayerPos = transform.position;
        startCameraPos = cameraTransform.position;
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
            if (cameraTransform == null) return;
            CaptureAnchor();
            return;
        }

        Vector3 delta = cameraTransform.position - startCameraPos;
        float x = startLayerPos.x + delta.x * parallaxFactor.x;
        float y = applyY ? startLayerPos.y + delta.y * parallaxFactor.y : startLayerPos.y;
        transform.position = new Vector3(x, y, startLayerPos.z);

        if (debugLogs)
        {
            Debug.Log($"[Parallax:{name}] cam.x={cameraTransform.position.x:F2} delta.x={delta.x:F2} factor.x={parallaxFactor.x:F2} layer.x={transform.position.x:F2}");
        }
    }
}
