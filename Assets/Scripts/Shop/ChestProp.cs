using UnityEngine;

/// <summary>
/// Cofre interactivo que otorga XP al ser golpeado por el arma del jugador.
/// En lugar de destruirse, reproduce una animación de apertura y se desactiva.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ChestProp : MonoBehaviour
{
    [Header("Recompensa")]
    [Tooltip("Cantidad de XP que otorga este cofre al abrirse.")]
    [SerializeField] private int experienceReward = 10;

    [Header("Daño")]
    [Tooltip("Puntos de vida del cofre. Se abre cuando llega a 0.")]
    [SerializeField] private int hitPoints = 1;

    [Header("Gamefeel - Audio")]
    [Tooltip("Clip que se reproducirá al abrir el cofre.")]
    [SerializeField] private AudioClip openClip;

    [Tooltip("Volumen del clip de apertura.")]
    [Range(0f, 1f)]
    [SerializeField] private float openVolume = 1f;

    [Header("Gamefeel - Animación y VFX")]
    [Tooltip("Animator del cofre para reproducir la animación.")]
    [SerializeField] private Animator chestAnimator;

    [Tooltip("Nombre del Trigger en el Animator para iniciar la apertura.")]
    [SerializeField] private string openTriggerName = "Open";

    [Tooltip("Prefab de partículas (ej. brillos o monedas) que salta al abrirse.")]
    [SerializeField] private GameObject lootVFXPrefab;

    [Tooltip("Tiempo (en segundos) tras el cual se destruirá el VFX.")]
    [SerializeField] private float vfxLifetime = 2f;

    private bool isOpen = false;
    private Collider2D chestCollider;

    private void Awake()
    {
        chestCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// Aplica daño al cofre. Debe ser llamado por el sistema de combate.
    /// </summary>
    public void TakeDamage(int damage = 1)
    {
        if (isOpen) return;

        hitPoints -= damage;

        if (hitPoints <= 0)
        {
            OpenChest();
        }
    }

    /// <summary>
    /// Ejecuta la rutina de apertura: Animación, VFX, audio y recompensa.
    /// </summary>
    private void OpenChest()
    {
        isOpen = true;

        // 1. Desactivamos el collider para que la espada no lo detecte más
        if (chestCollider != null)
        {
            chestCollider.enabled = false;
        }

        // 2. Disparamos la animación de apertura
        if (chestAnimator != null)
        {
            chestAnimator.SetTrigger(openTriggerName);
        }

        // 3. Instanciamos el VFX
        if (lootVFXPrefab != null)
        {
            GameObject vfxInstance = Instantiate(lootVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfxInstance, vfxLifetime);
        }

        // 4. Reproducimos el sonido
        if (openClip != null)
        {
            AudioSource.PlayClipAtPoint(openClip, transform.position, openVolume);
        }

        // 5. Otorgamos la XP al jugador
        // TODO: Conectar con el ExperienceSystem real.
        Debug.Log($"Add {experienceReward} XP from Chest!");
    }
}
