using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controlador de la ESCENA de carga ("Loading"). Es una escena in-game: el personaje real corre con
/// su propio Animator (y, si querés, se mueve / hay parallax de fondo) mientras esta clase carga la
/// próxima escena en segundo plano (async). Cuando la escena destino está lista y pasó un tiempo mínimo
/// (para que se vea la corrida), salta a la escena nueva.
///
/// Esta clase NO dibuja al personaje: solo maneja la carga async + el texto "Cargando Nivel 2...".
/// El personaje es un objeto real de la escena con su Animator en el estado de correr.
///
/// Cómo se usa: quien dispara el salto de nivel (ej: GameManager) primero setea <see cref="TargetScene"/>
/// con el nombre de la escena destino, y luego carga la escena "Loading". Si nadie lo setea, usa
/// <see cref="fallbackScene"/>. El estado del Player (vida, XP, habilidades) viaja por su cuenta en
/// PlayerStateStore (estático), así que sobrevive a esta escena intermedia.
/// </summary>
public class LevelLoadingScreen : MonoBehaviour
{
    /// <summary>Escena destino que se cargará al terminar la pantalla de carga. La setea quien dispara
    /// la transición justo antes de cargar la escena "Loading".</summary>
    public static string TargetScene;

    [Header("Texto")]
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private string label = "Cargando Nivel 2";
    [Tooltip("Agrega puntitos animados al final (Cargando Nivel 2 . .. ...).")]
    [SerializeField] private bool animatedDots = true;
    [SerializeField] private float dotInterval = 0.35f;

    [Header("Tiempos")]
    [Tooltip("Tiempo mínimo que se ve la pantalla aunque la escena destino cargue al instante. " +
             "Subilo para que se vea más la corrida del personaje.")]
    [SerializeField] private float minDisplayTime = 3f;

    [Header("Fade de salida")]
    [Tooltip("Opcional: fundido a negro antes de saltar a la escena nueva, para que la transición no " +
             "sea brusca. Si está vacío, salta directo sin fundido.")]
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("Fallback")]
    [Tooltip("Escena a cargar si nadie seteó TargetScene (ej: si probás esta escena sola).")]
    [SerializeField] private string fallbackScene = "Level 2";

    private void Start()
    {
        // Por si venimos de una escena que dejó el juego en pausa (timeScale = 0).
        Time.timeScale = 1f;

        string sceneToLoad = string.IsNullOrEmpty(TargetScene) ? fallbackScene : TargetScene;
        TargetScene = null; // consumimos el destino para que no quede pegado.

        StartCoroutine(LoadRoutine(sceneToLoad));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            yield break;
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.deltaTime;

            UpdateText(elapsed);

            // Listo cuando la escena terminó de cargar (0.9 = lista para activar) y pasó el tiempo mínimo.
            bool sceneReady = op.progress >= 0.9f;
            if (sceneReady && elapsed >= minDisplayTime)
            {
                break;
            }

            yield return null;
        }

        // Fundido a negro para suavizar el salto (y ocultar el pequeño tirón de activar la escena).
        if (screenFader != null)
        {
            yield return screenFader.FadeToBlack(fadeOutDuration);
        }

        // Activamos la escena nueva: esto descarga la escena de carga (y este objeto con ella).
        op.allowSceneActivation = true;
    }

    private void UpdateText(float time)
    {
        if (loadingText == null)
        {
            return;
        }

        if (!animatedDots)
        {
            loadingText.text = label;
            return;
        }

        int dots = (Mathf.FloorToInt(time / Mathf.Max(0.05f, dotInterval)) % 4);
        loadingText.text = label + new string('.', dots);
    }
}
