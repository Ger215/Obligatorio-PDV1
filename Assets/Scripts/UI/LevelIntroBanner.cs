using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Banner cinematográfico de inicio de nivel/zona. Entra deslizándose desde la izquierda, se mantiene
/// visible un rato y se desvanece mientras sigue desplazándose hacia la derecha. NO pausa el juego ni
/// bloquea controles: corre en una corrutina con tiempo no escalado y es puramente visual.
///
/// Reutilizable: cambiás título, subtítulo y retrato desde el Inspector. Se puede disparar solo al
/// cargar la escena (playOnStart) o manualmente desde un trigger llamando a Play().
/// </summary>
public class LevelIntroBanner : MonoBehaviour
{
    [Header("Referencias UI")]
    [Tooltip("RectTransform del banner que se mueve (la tarjeta entera). Hijo de un Canvas.")]
    [SerializeField] private RectTransform bannerRoot;
    [Tooltip("CanvasGroup en el banner, para el fade. Si está vacío se busca/crea en bannerRoot.")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;

    [Header("Contenido (editable por nivel)")]
    [Tooltip("Retrato del personaje (ej: mostrando la espada). Dejalo vacío para usar el que ya tenga el Image.")]
    [SerializeField] private Sprite portraitSprite;
    [SerializeField] private string levelTitle = "NIVEL 1";
    [TextArea]
    [SerializeField] private string levelSubtitle = "Las Cuevas Olvidadas";

    [Header("Disparo")]
    [Tooltip("Si está activo, el banner se reproduce solo al cargar la escena.")]
    [SerializeField] private bool playOnStart = true;
    [Tooltip("Espera (segundos) antes de empezar, por si querés un respiro al cargar.")]
    [SerializeField] private float startDelay = 0.3f;

    [Header("Animación")]
    [Tooltip("Posición visible final del banner (anchoredPosition). 0,0 = centrado; X positivo = centro-derecha.")]
    [SerializeField] private Vector2 shownPosition = new Vector2(120f, 0f);
    [Tooltip("Qué tan a la derecha arranca, fuera de pantalla (en px de UI).")]
    [SerializeField] private float hiddenOffsetX = 1400f;
    [Tooltip("Cuánto se desplaza hacia la izquierda mientras se desvanece al final.")]
    [SerializeField] private float driftLeftDistance = 180f;
    [SerializeField] private float slideInDuration = 0.7f;
    [SerializeField] private float holdDuration = 2f;
    [SerializeField] private float fadeOutDuration = 0.8f;

    private Coroutine routine;

    private void Awake()
    {
        if (bannerRoot == null)
        {
            bannerRoot = transform as RectTransform;
        }

        if (canvasGroup == null && bannerRoot != null)
        {
            canvasGroup = bannerRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // Arranca oculto para que no parpadee en el primer frame.
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    /// <summary>Dispara el banner. Podés llamarlo desde un trigger de zona, un evento, etc.</summary>
    public void Play()
    {
        if (bannerRoot == null)
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }
        routine = StartCoroutine(PlayRoutine());
    }

    /// <summary>Variante para cambiar el contenido en el momento (ej: distintas zonas con un mismo banner).</summary>
    public void Play(string title, string subtitle, Sprite portrait)
    {
        levelTitle = title;
        levelSubtitle = subtitle;
        if (portrait != null)
        {
            portraitSprite = portrait;
        }
        Play();
    }

    private IEnumerator PlayRoutine()
    {
        ApplyContent();

        Vector2 hiddenPosition = shownPosition + Vector2.right * hiddenOffsetX;
        Vector2 driftPosition = shownPosition + Vector2.left * driftLeftDistance;

        bannerRoot.anchoredPosition = hiddenPosition;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        bannerRoot.gameObject.SetActive(true);

        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(startDelay);
        }

        // 1) Slide in con ease-out (rápido al entrar, frena suave al llegar).
        yield return Animate(slideInDuration, t =>
        {
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
            bannerRoot.anchoredPosition = Vector2.LerpUnclamped(hiddenPosition, shownPosition, eased);
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Clamp01(t * 2f); // aparece en la primera mitad
        });

        bannerRoot.anchoredPosition = shownPosition;
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // 2) Hold.
        if (holdDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(holdDuration);
        }

        // 3) Fade out + drift a la derecha (ease-in suave).
        yield return Animate(fadeOutDuration, t =>
        {
            float eased = t * t; // ease-in
            bannerRoot.anchoredPosition = Vector2.LerpUnclamped(shownPosition, driftPosition, eased);
            if (canvasGroup != null) canvasGroup.alpha = 1f - t;
        });

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        bannerRoot.gameObject.SetActive(false);
        routine = null;
    }

    private void ApplyContent()
    {
        if (portraitImage != null && portraitSprite != null)
        {
            portraitImage.sprite = portraitSprite;
        }
        if (titleText != null)
        {
            titleText.text = levelTitle;
        }
        if (subtitleText != null)
        {
            subtitleText.text = levelSubtitle;
        }
    }

    private IEnumerator Animate(float duration, System.Action<float> step)
    {
        if (duration <= 0f)
        {
            step(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            step(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        step(1f);
    }
}
