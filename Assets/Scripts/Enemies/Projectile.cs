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
        if (hit)
        {
            return;
        }

        if (((1 << other.gameObject.layer) & targetLayers.value) == 0)
        {
            return;
        }

        hit = true;
        HealthSystem health = other.GetComponent<HealthSystem>();
        health?.TakeDamage(damage);
        Destroy(gameObject);
    }
}
