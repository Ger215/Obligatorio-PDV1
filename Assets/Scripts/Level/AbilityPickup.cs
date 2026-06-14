using UnityEngine;

/// <summary>
/// Pickup de habilidad. Cuando el Player entra en el trigger, se muestra un cartel
/// ("Apreta 1, 2 o 3 para equiparte la habilidad {nombre}") via SignUI. Al apretar
/// una de esas teclas, la habilidad se asigna al slot correspondiente (reemplazando
/// la que estuviera equipada ahí) via PlayerAbilityController.LearnAbilityInSlot()
/// y el pickup se consume. Usá un Collider2D en modo trigger.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AbilityPickup : MonoBehaviour
{
    [Tooltip("Habilidad que otorga al agarrarse.")]
    [SerializeField] private PlayerAbilityDefinition ability;

    [Tooltip("Capas que pueden agarrar el pickup (seleccioná la capa del Player).")]
    [SerializeField] private LayerMask targetLayers;

    [Tooltip("Sonido opcional al agarrarse.")]
    [SerializeField] private AudioClip pickupSound;

    [Tooltip("VFX opcional al agarrarse (se instancia en la posición del pickup).")]
    [SerializeField] private GameObject pickupVfx;

    private bool consumed;
    private PlayerAbilityController playerInRange;

    private void OnTriggerEnter2D(Collider2D other) => HandleEnter(other.gameObject);
    private void OnCollisionEnter2D(Collision2D collision) => HandleEnter(collision.gameObject);
    private void OnTriggerExit2D(Collider2D other) => HandleExit(other.gameObject);
    private void OnCollisionExit2D(Collision2D collision) => HandleExit(collision.gameObject);

    public bool AssignToSlot(int slotIndex)
    {
        if (consumed || ability == null || playerInRange == null)
        {
            return false;
        }

        if (!playerInRange.LearnAbilityInSlot(ability, slotIndex))
        {
            return false;
        }

        consumed = true;
        SignUI.Instance?.Hide();

        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }
        if (pickupVfx != null)
        {
            Instantiate(pickupVfx, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
        return true;
    }

    private void HandleEnter(GameObject other)
    {
        if (consumed || ability == null || playerInRange != null)
        {
            return;
        }

        if (((1 << other.layer) & targetLayers.value) == 0)
        {
            return;
        }

        PlayerAbilityController abilityController = other.GetComponentInParent<PlayerAbilityController>();
        if (abilityController == null)
        {
            return;
        }

        playerInRange = abilityController;
        playerInRange.SetNearbyPickup(this);
        SignUI.Instance?.Show($"Apreta 1, 2 o 3 para equiparte la habilidad {ability.displayName}");
    }

    private void HandleExit(GameObject other)
    {
        if (((1 << other.layer) & targetLayers.value) == 0)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerAbilityController>() != playerInRange)
        {
            return;
        }

        playerInRange.ClearNearbyPickup(this);
        playerInRange = null;
        SignUI.Instance?.Hide();
    }
}
