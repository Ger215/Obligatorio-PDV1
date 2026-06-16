using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class DoorTransition : MonoBehaviour
{
    [Header("Destinos")]
    [Tooltip("Punto donde aparece el jugador después del fade.")]
    [SerializeField] private Transform playerDestination;

    [Tooltip("Transform al que se mueve la cámara durante el negro (centro del nuevo cuarto).")]
    [SerializeField] private Transform cameraTarget;

    [Header("Fade")]
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private float fadeOutDuration = 0.25f;
    [SerializeField] private float fadeInDuration  = 0.35f;

    [Header("Audio")]
    [SerializeField] private AudioClip transitionClip;
    [Range(0f, 1f)]
    [SerializeField] private float transitionVolume = 1f;

    private bool isTransitioning;
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTransitioning) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
            StartCoroutine(TransitionRoutine(player.transform));
    }

    private IEnumerator TransitionRoutine(Transform player) // player es siempre el root del PlayerController
    {
        isTransitioning = true;

        if (transitionClip != null)
            AudioSource.PlayClipAtPoint(transitionClip, transform.position, transitionVolume);

        // Creamos el overlay negro encima de todo
        Image fadeImage = CreateFadeOverlay();

        yield return StartCoroutine(Fade(fadeImage, 0f, 1f, fadeOutDuration));

        // Bajo el negro: mover jugador y cámara
        if (playerDestination != null)
            player.position = playerDestination.position;

        if (mainCamera != null && cameraTarget != null)
            mainCamera.transform.position = new Vector3(
                cameraTarget.position.x,
                cameraTarget.position.y,
                mainCamera.transform.position.z);

        // Micro-pausa para que Unity procese las nuevas posiciones
        yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(Fade(fadeImage, 1f, 0f, fadeInDuration));

        // Limpieza: destruimos el canvas temporal
        if (fadeImage != null)
            Destroy(fadeImage.canvas.gameObject);

        isTransitioning = false;
    }

    private Image CreateFadeOverlay()
    {
        var canvasGo = new GameObject("_DoorFadeOverlay");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        canvasGo.AddComponent<CanvasScaler>();

        var imgGo = new GameObject("FadeImage");
        imgGo.transform.SetParent(canvasGo.transform, false);
        var image = imgGo.AddComponent<Image>();
        image.raycastTarget = false;

        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Color c = fadeColor;
        c.a = 0f;
        image.color = c;

        return image;
    }

    private IEnumerator Fade(Image image, float from, float to, float duration)
    {
        if (image == null) yield break;
        if (duration <= 0f) { SetAlpha(image, to); yield break; }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.034f);
            SetAlpha(image, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
            yield return null;
        }
        SetAlpha(image, to);
    }

    private void SetAlpha(Image image, float a)
    {
        Color c = fadeColor;
        c.a = a;
        image.color = c;
    }
}
