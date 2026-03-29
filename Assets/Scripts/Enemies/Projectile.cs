using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifetime = 4f;

    private Rigidbody2D rb;
    private Vector2 direction;
    private int damage;
    private LayerMask targetLayers;
    private bool hit;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Launch(Vector2 dir, int dmg, LayerMask layers)
    {
        direction = dir.normalized;
        damage = dmg;
        targetLayers = layers;
        rb.linearVelocity = direction * speed;
        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = direction * speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.gameObject);
    }

    private void HandleHit(GameObject other)
    {
        if (hit)
        {
            return;
        }

        bool isTarget = ((1 << other.layer) & targetLayers.value) != 0;

        if (isTarget)
        {
            hit = true;
            other.GetComponent<HealthSystem>()?.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Ground") || other.CompareTag("Platform"))
        {
            hit = true;
            Destroy(gameObject);
        }
    }
}
