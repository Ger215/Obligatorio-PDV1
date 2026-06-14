using UnityEngine;

/// <summary>
/// Pickup de vida. Al entrar en contacto un objeto cuya capa esté en targetLayers (la del Player),
/// le cura healAmount de vida y se consume. Si el Player ya está a vida llena no se consume, así no
/// se desperdicia. Usá un Collider2D en modo trigger.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HealthPickup : MonoBehaviour
{
    [Tooltip("Vida que cura al agarrarse. 1 = un corazón.")]
    [SerializeField] private int healAmount = 1;

    [Tooltip("Capas que pueden agarrar el pickup (seleccioná la capa del Player).")]
    [SerializeField] private LayerMask targetLayers;

    [Tooltip("Sonido opcional al agarrarse.")]
    [SerializeField] private AudioClip pickupSound;

    [Tooltip("VFX opcional al agarrarse (se instancia en la posición del pickup).")]
    [SerializeField] private GameObject pickupVfx;

    private bool consumed;

    private void OnTriggerEnter2D(Collider2D other) => TryPickup(other.gameObject);
    private void OnCollisionEnter2D(Collision2D collision) => TryPickup(collision.gameObject);

    private void TryPickup(GameObject other)
    {
        if (consumed)
        {
            return;
        }

        if (((1 << other.layer) & targetLayers.value) == 0)
        {
            return;
        }

        HealthSystem health = other.GetComponentInParent<HealthSystem>();
        if (health == null || health.IsDead || health.CurrentHealth >= health.MaxHealth)
        {
            return;
        }

        consumed = true;
        health.Heal(healAmount);

        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }
        if (pickupVfx != null)
        {
            Instantiate(pickupVfx, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
