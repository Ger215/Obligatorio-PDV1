using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ChestProp : MonoBehaviour
{
    [Header("Interacción")]
    [Tooltip("GameObject con el prompt 'Presiona E'. Se activa al entrar en rango y se desactiva al salir o al abrir el cofre.")]
    [SerializeField] private GameObject promptObject;

    [Tooltip("Cantidad de XP que otorga este cofre al abrirse.")]
    [SerializeField] private int experienceReward = 10;

    [Header("Gamefeel")]
    [Tooltip("Clip que se reproduce al abrir el cofre.")]
    [SerializeField] private AudioClip openClip;

    [Range(0f, 1f)]
    [SerializeField] private float openVolume = 1f;

    [Tooltip("Prefab de partículas que se instancia al abrir el cofre.")]
    [SerializeField] private GameObject lootVFXPrefab;

    [Tooltip("Segundos tras los cuales se destruye el VFX instanciado.")]
    [SerializeField] private float vfxLifetime = 2f;

    [Header("Animación")]
    [Tooltip("Animator del cofre.")]
    [SerializeField] private Animator chestAnimator;

    [Tooltip("Nombre del Trigger en el Animator que dispara la apertura.")]
    [SerializeField] private string openTriggerName = "Open";

    private bool isPlayerInRange;
    private bool isOpen;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        if (promptObject != null) promptObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || isOpen) return;
        isPlayerInRange = true;
        if (promptObject != null) promptObject.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        isPlayerInRange = false;
        if (promptObject != null) promptObject.SetActive(false);
    }

    private void Update()
    {
        if (!isPlayerInRange || isOpen) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            OpenChest();
        }
    }

    private void OpenChest()
    {
        isOpen = true;

        if (promptObject != null) promptObject.SetActive(false);

        if (chestAnimator != null)
            chestAnimator.SetTrigger(openTriggerName);

        if (lootVFXPrefab != null)
        {
            GameObject vfx = Instantiate(lootVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, vfxLifetime);
        }

        if (openClip != null)
            AudioSource.PlayClipAtPoint(openClip, transform.position, openVolume);

        Debug.Log($"[ChestProp] Add {experienceReward} XP from Chest!");
    }
}
