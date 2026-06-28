using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifetime = 4f;
    [Tooltip("Si true, el sprite rota para apuntar hacia donde viaja.")]
    [SerializeField] private bool rotateToDirection = true;
    [Tooltip("Grados a sumar a la rotación. 0 = el arte apunta a la DERECHA por defecto. " +
             "Si tu sprite apunta hacia arriba poné -90, hacia abajo 90, hacia la izquierda 180.")]
    [SerializeField] private float spriteAngleOffset = 0f;

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

        if (rotateToDirection && direction.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + spriteAngleOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

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
