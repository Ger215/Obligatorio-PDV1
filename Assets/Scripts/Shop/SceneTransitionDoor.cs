using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Puerta que cambia de ESCENA al apretar E (a diferencia de DoorTransition, que solo mueve al
/// Player y la cámara dentro de la misma escena). Antes de cargar la escena destino captura el
/// estado del Player (vida, XP y habilidades) en <see cref="PlayerStateStore"/>, así la escena que
/// arranca puede volcarlo sobre su propio Player (cada escena tiene su Player; no usamos
/// DontDestroyOnLoad).
///
/// Uso: ponelo en la puerta de la casita en Level 2 (target = "Shop") y en la puerta de salida de
/// la Shop (target = el nivel al que querés volver). Necesita un Collider2D en modo trigger.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SceneTransitionDoor : MonoBehaviour
{
    // Escena desde la que se entró a la Shop. La graban las puertas con recordAsOrigin (las de los
    // niveles que llevan a la Shop) y la lee la puerta de salida con returnToOrigin, para volver al
    // nivel correcto sea cual sea (Level 2, Level 3, etc.) sin hardcodear el destino.
    public static string OriginScene { get; private set; }

    [Header("Destino")]
    [Tooltip("Nombre EXACTO de la escena a cargar (tiene que estar en Build Settings). Se ignora si " +
             "Return To Origin está activo.")]
    [SerializeField] private string targetScene;
    [Tooltip("Id del SceneSpawnPoint donde aparecer en la escena destino. Vacío = posición por " +
             "defecto de la escena.")]
    [SerializeField] private string targetSpawnId;

    [Tooltip("Activalo en las puertas que ENTRAN a la Shop (una en cada nivel). Recuerda de qué " +
             "nivel venís para que la puerta de salida sepa a dónde volver.")]
    [SerializeField] private bool recordAsOrigin;
    [Tooltip("Activalo en la puerta de SALIDA de la Shop. Ignora Target Scene y vuelve al nivel " +
             "desde el que entraste (el que grabó Record As Origin).")]
    [SerializeField] private bool returnToOrigin;

    [Header("Estado del Player")]
    [Tooltip("Si está activo, guarda vida/XP/habilidades del Player antes de cambiar de escena " +
             "para que la próxima escena lo restaure. Dejalo activo salvo que sea una puerta sin " +
             "continuidad de estado.")]
    [SerializeField] private bool capturePlayerState = true;

    [Header("Prompt")]
    [Tooltip("Cartel opcional tipo 'Apretá E' que se muestra cuando el Player está cerca.")]
    [SerializeField] private GameObject promptObject;

    [Header("Fundido")]
    [Tooltip("ScreenFader opcional para fundir a negro antes de cargar. Si está vacío, busca uno en " +
             "la escena; si no hay, carga sin fundido.")]
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip transitionClip;
    [Range(0f, 1f)]
    [SerializeField] private float transitionVolume = 1f;

    private Transform playerInRange;
    private bool isTransitioning;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        if (promptObject != null) promptObject.SetActive(false);
        if (screenFader == null) screenFader = FindFirstObjectByType<ScreenFader>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        playerInRange = player.transform;
        if (promptObject != null) promptObject.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.transform != playerInRange) return;

        playerInRange = null;
        if (promptObject != null) promptObject.SetActive(false);
    }

    private void Update()
    {
        if (playerInRange == null || isTransitioning) return;

        bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);

        if (pressed)
        {
            StartCoroutine(TravelRoutine());
        }
    }

    private IEnumerator TravelRoutine()
    {
        // Grabar de qué nivel venimos ANTES de resolver el destino, así la puerta de salida sabe
        // a dónde volver (Level 2, Level 3, etc.).
        if (recordAsOrigin)
        {
            OriginScene = SceneManager.GetActiveScene().name;
        }

        string destination = returnToOrigin ? OriginScene : targetScene;

        if (string.IsNullOrEmpty(destination))
        {
            Debug.LogWarning($"{name}: no hay escena destino en SceneTransitionDoor" +
                             (returnToOrigin ? " (Return To Origin activo pero no se grabó ningún origen)." : "."), this);
            yield break;
        }

        isTransitioning = true;
        if (promptObject != null) promptObject.SetActive(false);

        if (transitionClip != null)
        {
            AudioSource.PlayClipAtPoint(transitionClip, transform.position, transitionVolume);
        }

        if (capturePlayerState)
        {
            CaptureState();
        }

        SceneSpawnPoint.RequestedId = string.IsNullOrEmpty(targetSpawnId) ? null : targetSpawnId;

        if (screenFader != null)
        {
            yield return screenFader.FadeToBlack(fadeDuration);
        }

        SceneManager.LoadScene(destination);
    }

    private void CaptureState()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null && playerInRange != null)
        {
            player = playerInRange.GetComponentInParent<PlayerController>();
        }
        if (player == null) return;

        HealthSystem health = player.HealthSystem;
        ExperienceSystem experience = player.GetComponent<ExperienceSystem>();
        PlayerAbilityController abilities = player.GetComponent<PlayerAbilityController>();

        if (health == null || experience == null || abilities == null) return;

        PlayerStateStore.Capture(
            health.CurrentHealth,
            experience.CurrentExperience,
            experience.TotalExperienceEarned,
            abilities.LearnedAbilities);
    }
}
