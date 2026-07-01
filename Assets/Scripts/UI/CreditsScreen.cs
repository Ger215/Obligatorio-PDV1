using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Pantalla de CRÉDITOS. Scrollea un texto (RectTransform) de arriba hacia abajo a velocidad
/// configurable y, al terminar (o al apretar una tecla), carga la escena siguiente (ej: MainMenu).
///
/// Armado en Unity:
/// 1. Escena nueva "Credits" con un Canvas (Screen Space - Overlay).
/// 2. Dentro del Canvas, un TextMeshPro - Text (UI) con todo el texto de créditos. Alargá su
///    RectTransform hacia abajo para que quepan todas las líneas.
/// 3. Poné este componente en un GameObject de la escena y arrastrá el RectTransform del texto a
///    "Scrolling Text".
/// 4. Agregá la escena "Credits" a Build Settings. Para llegar acá, cargá "Credits" desde donde
///    quieras (ej: GameManager al terminar el juego, o un botón).
/// </summary>
public class CreditsScreen : MonoBehaviour
{
    [Header("Scroll")]
    [Tooltip("El RectTransform del texto que se desplaza.")]
    [SerializeField] private RectTransform scrollingText;
    [Tooltip("Velocidad en píxeles por segundo. Positivo = baja (arriba hacia abajo).")]
    [SerializeField] private float scrollSpeed = 300f;
    [Tooltip("Posición Y (local) desde la que arranca el texto. Suele ser negativa para empezar arriba de la pantalla.")]
    [SerializeField] private float startY = -1200f;
    [Tooltip("Posición Y (local) a la que termina. Al pasarla, se acaban los créditos.")]
    [SerializeField] private float endY = 1200f;

    [Header("Salida")]
    [Tooltip("Escena a cargar al terminar los créditos. Vacío = no carga nada (se queda).")]
    [SerializeField] private string nextScene = "MainMenu";
    [Tooltip("Si true, se puede saltear los créditos con cualquier tecla / click.")]
    [SerializeField] private bool allowSkip = true;
    [Tooltip("Segundos al entrar en los que NO se puede saltear. Evita que el click/tecla con el que " +
             "mataste al boss cierre los créditos al instante.")]
    [SerializeField] private float skipDelay = 1f;

    [Header("Fade de salida (opcional)")]
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private float fadeOutDuration = 0.5f;

    private bool finished;
    private float elapsed;

    private void Start()
    {
        Time.timeScale = 1f;

        if (scrollingText != null)
        {
            Vector2 pos = scrollingText.anchoredPosition;
            pos.y = startY;
            scrollingText.anchoredPosition = pos;
        }
    }

    private void Update()
    {
        if (finished) return;

        elapsed += Time.deltaTime;

        if (allowSkip && elapsed >= skipDelay)
        {
            bool skipPressed =
                (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (skipPressed)
            {
                Finish();
                return;
            }
        }

        if (scrollingText == null) return;

        Vector2 pos = scrollingText.anchoredPosition;
        pos.y += scrollSpeed * Time.deltaTime;
        scrollingText.anchoredPosition = pos;

        if (pos.y >= endY)
        {
            Finish();
        }
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;

        if (string.IsNullOrEmpty(nextScene)) return;

        if (screenFader != null)
        {
            StartCoroutine(FadeAndLoad());
        }
        else
        {
            SceneManager.LoadScene(nextScene);
        }
    }

    private System.Collections.IEnumerator FadeAndLoad()
    {
        yield return screenFader.FadeToBlack(fadeOutDuration);
        SceneManager.LoadScene(nextScene);
    }
}
