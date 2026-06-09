using UnityEngine;

/// <summary>
/// Pinchos como GameObject (no tileset). Lastiman al entrar en contacto a cualquier objeto
/// cuya capa esté en targetLayers (poné la del Player). Funciona tanto si el collider es
/// trigger (OnTriggerEnter2D) como sólido (OnCollisionEnter2D). El daño por golpe lo limita
/// el HealthSystem del Player vía sus frames de invencibilidad (activá Use Invincibility).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Spike : MonoBehaviour
{
    [Tooltip("Daño por golpe. 1 = un corazón.")]
    [SerializeField] private int damage = 1;

    [Tooltip("Capas que se lastiman con el pincho (seleccioná la capa del Player).")]
    [SerializeField] private LayerMask targetLayers;

    [Tooltip("Empuje hacia arriba al lastimarse, para 'rebotar' fuera del pincho. 0 = sin empuje.")]
    [SerializeField] private float knockbackForce = 0f;

    private void OnTriggerEnter2D(Collider2D other) => TryDamage(other.gameObject);
    private void OnCollisionEnter2D(Collision2D collision) => TryDamage(collision.gameObject);

    private void TryDamage(GameObject other)
    {
        if (((1 << other.layer) & targetLayers.value) == 0)
        {
            return;
        }

        HealthSystem health = other.GetComponentInParent<HealthSystem>();
        if (health == null)
        {
            return;
        }

        health.TakeDamage(damage);

        if (knockbackForce > 0f)
        {
            Rigidbody2D rb = other.GetComponentInParent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                rb.AddForce(Vector2.up * knockbackForce, ForceMode2D.Impulse);
            }
        }
    }
}
