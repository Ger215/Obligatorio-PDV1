using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossTrigger : MonoBehaviour
{
    [SerializeField] private SkeletonKingController boss;

    private bool triggered;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || boss == null) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        boss.Activate();
        gameObject.SetActive(false);
    }
}
