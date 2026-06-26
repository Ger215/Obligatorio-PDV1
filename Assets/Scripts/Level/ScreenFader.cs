using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Transición de room con corte por fundido (negro por defecto):
/// CameraController.TransitionStarted → fade OUT;
/// CameraController.RoomChanged (ya bajo el negro, cámara saltada y fondo swapeado) → fade IN.
/// Se arma solo en runtime: agregá este componente a un GameObject (la cámara está bien).
/// El Fade Out debe terminar dentro del Cover Duration del CameraController.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    [Header("Color del fundido")]
    [SerializeField] private Color fadeColor = Color.black;

    [Header("Timing")]
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float fadeInDuration  = 0.28f;

    [Header("Inicio de escena")]
    [Tooltip("Si está activo, la escena arranca en negro y hace fade-in al cargar. " +
             "Útil para revelar un nivel al entrar (ej: después de la pantalla de carga).")]
    [SerializeField] private bool fadeInOnStart = false;

    private Canvas canvas;
    private Image image;
    private Coroutine routine;

    private void Awake()
    {
        BuildOverlay();
        // Empezamos cubiertos para revelar en Start, evitando un flash en el primer frame.
        if (fadeInOnStart) SetAlpha(1f);
    }

    private void Start()
    {
        if (fadeInOnStart)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Fade(1f, 0f, fadeInDuration));
        }
    }

    private void OnEnable()
    {
        CameraController.TransitionStarted += HandleTransitionStarted;
        CameraController.RoomChanged       += HandleRoomChanged;
    }

    private void OnDisable()
    {
        CameraController.TransitionStarted -= HandleTransitionStarted;
        CameraController.RoomChanged       -= HandleRoomChanged;
    }

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    private void BuildOverlay()
    {
        var go = new GameObject("ScreenFaderOverlay");
        go.transform.SetParent(transform, false);

        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760; // por encima de todo
        go.AddComponent<CanvasScaler>();

        var imgGo = new GameObject("FadeImage");
        imgGo.transform.SetParent(go.transform, false);
        image = imgGo.AddComponent<Image>();
        image.raycastTarget = false;

        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        SetAlpha(0f);
    }

    /// <summary>
    /// Fundido a negro manual, fuera del flujo de transición de rooms (ej: la pantalla de carga
    /// que oculta el "tirón" del cambio de escena antes de activar el próximo nivel).
    /// Devuelve la corutina para poder esperarla con `yield return`.
    /// </summary>
    public Coroutine FadeToBlack() => FadeToBlack(fadeOutDuration);

    public Coroutine FadeToBlack(float duration)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fade(GetAlpha(), 1f, duration));
        return routine;
    }

    // Inicio de transición: cubrir la pantalla.
    private void HandleTransitionStarted(RoomBoundary newRoom)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fade(GetAlpha(), 1f, fadeOutDuration));
    }

    // Room ya cambiado bajo el negro: revelar. En el arranque alpha=0 → no-op.
    private void HandleRoomChanged(RoomBoundary newRoom)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fade(GetAlpha(), 0f, fadeInDuration));
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f) { SetAlpha(to); routine = null; yield break; }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.034f);
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            SetAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }
        SetAlpha(to);
        routine = null;
    }

    private float GetAlpha() => image != null ? image.color.a : 0f;

    private void SetAlpha(float a)
    {
        Color c = fadeColor;
        c.a = a;
        if (image != null) image.color = c;
        if (canvas != null) canvas.enabled = a > 0.001f;
    }
}
