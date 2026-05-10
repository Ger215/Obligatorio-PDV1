using System.Collections;
using UnityEngine;

[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(SpriteRenderer))]
public class DebugPunchingBag : MonoBehaviour
{
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;

    private HealthSystem healthSystem;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    private void Awake()
    {
        healthSystem = GetComponent<HealthSystem>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
    }

    private void OnEnable()
    {
        healthSystem.Damaged += HandleDamaged;
    }

    private void OnDisable()
    {
        healthSystem.Damaged -= HandleDamaged;
    }

    private void HandleDamaged(int current, int max)
    {
        healthSystem.ResetHealth();
        StopAllCoroutines();
        StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        spriteRenderer.color = hitColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }
}
