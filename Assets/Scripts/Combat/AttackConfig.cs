using UnityEngine;

[CreateAssetMenu(fileName = "AttackConfig", menuName = "Config/Combat/Attack")]
public class AttackConfig : ScriptableObject
{
    [Header("Range & Damage")]
    public float attackRange = 1.25f;
    public int baseDamage = 1;
    [Range(0f, 1f)] public float criticalChance = 0.15f;
    public float criticalMultiplier = 2f;

    [Header("Attack Timing")]
    [Tooltip("Time from animation start to the hit frame (wind-up)")]
    public float windUpDuration = 0.12f;
    [Tooltip("Duration of the active hit window")]
    public float hitDuration = 0.08f;
    [Tooltip("Recovery time after the hit (cannot attack again)")]
    public float recoveryDuration = 0.25f;
    [Tooltip("Total cooldown between attacks. Auto-calculated if zero or less")]
    public float attackCooldown = 0f;

    [Header("Debug")]
    public bool debugAttackLogs = true;

    public float EffectiveCooldown => attackCooldown > 0.01f
        ? attackCooldown
        : windUpDuration + hitDuration + recoveryDuration;

    private void OnValidate()
    {
        attackRange = Mathf.Max(0.1f, attackRange);
        windUpDuration = Mathf.Max(0f, windUpDuration);
        hitDuration = Mathf.Max(0.01f, hitDuration);
        recoveryDuration = Mathf.Max(0f, recoveryDuration);
        baseDamage = Mathf.Max(1, baseDamage);
        criticalChance = Mathf.Clamp01(criticalChance);
        criticalMultiplier = Mathf.Max(1f, criticalMultiplier);
    }
}
