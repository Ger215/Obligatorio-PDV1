using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puerta que transporta al jugador a la escena de la ruleta.
/// Muestra un banner con la tecla "E" mientras el jugador está cerca
/// y al pulsarla reproduce un sonido de apertura y carga la escena destino.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RouletteDoor : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("Banner que indica 'Pulsa E para entrar' mientras el jugador está cerca.")]
    [SerializeField] private GameObject interactionBanner;

    [Header("Gamefeel")]
    [Tooltip("AudioSource desde el que se reproducirá el sonido de apertura.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Clip que suena al abrir la puerta.")]
    [SerializeField] private AudioClip doorOpenClip;

    [Tooltip("Sistema de partículas opcional para reforzar la apertura (polvo, brillo, etc.).")]
    [SerializeField] private ParticleSystem doorOpenVFX;

    [Header("Configuración")]
    [Tooltip("Tag del GameObject del jugador.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Tecla utilizada para entrar por la puerta.")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    [Tooltip("Nombre exacto de la escena a cargar (debe estar añadida en Build Settings).")]
    [SerializeField] private string targetSceneName = "RouletteRoom";

    [Tooltip("Pequeño retraso antes de cambiar de escena para que se escuche el clip y se vea el VFX.")]
    [SerializeField] private float transitionDelay = 0.4f;

    private bool playerInRange;
    private bool isTransitioning;

    private void Awake()
    {
        // Aseguramos que el banner empiece oculto.
        if (interactionBanner != null) interactionBanner.SetActive(false);
    }

    private void Update()
    {
        if (isTransitioning) return;

        if (playerInRange && Input.GetKeyDown(interactionKey))
        {
            OpenDoor();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInRange = true;

        if (interactionBanner != null) interactionBanner.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInRange = false;

        if (interactionBanner != null) interactionBanner.SetActive(false);
    }

    /// <summary>
    /// Dispara el feedback audiovisual y programa la carga de la escena destino.
    /// </summary>
    private void OpenDoor()
    {
        isTransitioning = true;

        // Ocultamos el banner para evitar inputs adicionales durante la transición.
        if (interactionBanner != null) interactionBanner.SetActive(false);

        // Feedback de audio.
        if (audioSource != null && doorOpenClip != null)
        {
            audioSource.PlayOneShot(doorOpenClip);
        }

        // Feedback visual.
        if (doorOpenVFX != null)
        {
            doorOpenVFX.Play();
        }

        // Esperamos un breve instante antes de cargar la escena para no cortar el feedback.
        Invoke(nameof(LoadTargetScene), transitionDelay);
    }

    private void LoadTargetScene()
    {
        SceneManager.LoadScene(targetSceneName);
    }
}
