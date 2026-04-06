using UnityEngine;

public class ProjectileLauncher : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float cooldown = 2f;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask targetLayers;

    private float lastLaunchTime = -999f;

    public void Configure(int dmg, LayerMask layers)
    {
        damage = dmg;
        targetLayers = layers;
    }

    public void TryLaunch(Vector2 direction)
    {
        if (projectilePrefab == null)
        {
            return;
        }

        if (Time.time < lastLaunchTime + cooldown)
        {
            return;
        }

        lastLaunchTime = Time.time;

        GameObject proj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        Projectile projectile = proj.GetComponent<Projectile>();
        projectile?.Launch(direction, damage, targetLayers);
    }
}
