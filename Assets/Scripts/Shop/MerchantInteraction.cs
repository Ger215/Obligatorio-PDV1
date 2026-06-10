using UnityEngine;

/// <summary>
/// Controla la interacción del jugador con el NPC mercader.
/// Muestra un bocadillo de bienvenida la primera vez que el jugador se acerca,
/// un banner con la tecla "E" mientras permanece en la zona,
/// y abre el Canvas de la tienda al pulsar dicha tecla.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MerchantInteraction : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("Bocadillo de bienvenida que aparece la primera vez que el jugador se acerca.")]
    [SerializeField] private GameObject welcomeBubble;

    [Tooltip("Banner que indica 'Pulsa E para interactuar' mientras el jugador está cerca.")]
    [SerializeField] private GameObject interactionBanner;

    [Tooltip("Canvas raíz de la tienda que se activa al interactuar.")]
    [SerializeField] private GameObject shopUI;

    [Header("Gamefeel")]
    [Tooltip("AudioSource desde el que se reproducirá el clip de interacción.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Clip de audio que suena al abrir la tienda.")]
    [SerializeField] private AudioClip interactionClip;

    [Tooltip("Sistema de partículas opcional que se dispara al abrir la tienda (humo, brillo, etc.).")]
    [SerializeField] private ParticleSystem interactionVFX;

    [Header("Configuración")]
    [Tooltip("Tag del GameObject del jugador.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Tecla utilizada para interactuar con el mercader.")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    // Flags internos
    private bool hasGreetedPlayer;   // Asegura que el bocadillo de bienvenida se muestre solo una vez.
    private bool playerInRange;      // True mientras el jugador permanece dentro del trigger.

    private void Awake()
    {
        // Aseguramos un estado inicial limpio por si los GameObjects quedaron activos en el editor.
        if (interactionBanner != null) interactionBanner.SetActive(false);
        if (welcomeBubble != null) welcomeBubble.SetActive(false);
        if (shopUI != null) shopUI.SetActive(false);
    }

    private void Update()
    {
        // Solo escuchamos la tecla cuando el jugador está dentro del trigger.
        if (playerInRange && Input.GetKeyDown(interactionKey))
        {
            OpenShop();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInRange = true;

        // Mostramos el bocadillo de bienvenida solo la primera vez.
        if (!hasGreetedPlayer)
        {
            hasGreetedPlayer = true;
            if (welcomeBubble != null) welcomeBubble.SetActive(true);
        }

        // Mostramos el banner "Pulsa E".
        if (interactionBanner != null) interactionBanner.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInRange = false;

        // Ocultamos el banner al salir de la zona.
        if (interactionBanner != null) interactionBanner.SetActive(false);

        // El bocadillo de bienvenida también se oculta al alejarse.
        if (welcomeBubble != null) welcomeBubble.SetActive(false);
    }

    /// <summary>
    /// Abre el Canvas de la tienda y dispara el feedback audiovisual.
    /// </summary>
    private void OpenShop()
    {
        // Ocultamos el banner ya que la UI tomará el foco.
        if (interactionBanner != null) interactionBanner.SetActive(false);

        // Feedback de audio.
        if (audioSource != null && interactionClip != null)
        {
            audioSource.PlayOneShot(interactionClip);
        }

        // Feedback visual.
        if (interactionVFX != null)
        {
            interactionVFX.Play();
        }

        // Activamos la UI de la tienda.
        if (shopUI != null) shopUI.SetActive(true);
    }
}
