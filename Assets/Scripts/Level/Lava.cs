using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lava: daña y empuja hacia arriba a cualquier objeto cuya capa esté en targetLayers (el Player).
/// Mientras el objeto sigue dentro, vuelve a dañarlo cada damageInterval segundos y lo rebota hacia
/// arriba para sacarlo. Poné un Collider2D en modo trigger sobre el tilemap/zona de lava.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Lava : MonoBehaviour
{
    [Tooltip("Daño por golpe. 1 = un corazón.")]
    [SerializeField] private int damage = 1;

    [Tooltip("Capas que se dañan con la lava (seleccioná la del Player).")]
    [SerializeField] private LayerMask targetLayers;

    [Tooltip("Empuje hacia arriba al tocar la lava.")]
    [SerializeField] private float knockbackForce = 12f;

    [Tooltip("Segundos entre golpes mientras el objeto sigue dentro de la lava.")]
    [SerializeField] private float damageInterval = 0.6f;

    // Próximo momento en que cada víctima puede volver a recibir daño (cooldown por objeto).
    private readonly Dictionary<HealthSystem, float> nextHitTime = new Dictionary<HealthSystem, float>();

    private void OnTriggerEnter2D(Collider2D other) => TryHit(other.gameObject);
    private void OnTriggerStay2D(Collider2D other) => TryHit(other.gameObject);
    private void OnCollisionEnter2D(Collision2D collision) => TryHit(collision.gameObject);
    private void OnCollisionStay2D(Collision2D collision) => TryHit(collision.gameObject);

    private void TryHit(GameObject other)
    {
        if (((1 << other.layer) & targetLayers.value) == 0)
        {
            return;
        }

        HealthSystem health = other.GetComponentInParent<HealthSystem>();
        if (health == null || health.IsDead)
        {
            return;
        }

        // Respetar el cooldown propio de la lava para no drenar la vida cada frame.
        if (nextHitTime.TryGetValue(health, out float readyAt) && Time.time < readyAt)
        {
            return;
        }
        nextHitTime[health] = Time.time + damageInterval;

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
