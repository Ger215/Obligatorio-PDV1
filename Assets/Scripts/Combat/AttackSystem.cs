using System;
using System.Collections.Generic;
using UnityEngine;

public class AttackSystem : MonoBehaviour
{
    [Header("Attack Setup")]
    [SerializeField] private AttackConfig config;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private float attackRange = 1.25f;
    [SerializeField] private float attackCooldown = 0.45f;

    [Header("Damage")]
    [SerializeField] private int baseDamage = 1;
    [SerializeField] private float criticalChance = 0.15f;
    [SerializeField] private float criticalMultiplier = 2f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 6f;

    [Header("Debug")]
    [SerializeField] private bool debugAttackLogs = false;

    private float lastAttackTime = -Mathf.Infinity;
    private float bonusDamageMultiplier = 1f;
    private float bonusCriticalChance;
    private float bonusCriticalMultiplier = 1f;
    private HealthSystem ownerHealth;

    public Transform AttackPointTransform => attackPoint;
    public float AttackCooldown => attackCooldown;
    public float AttackRange => attackRange;
    public float KnockbackForce => knockbackForce;
    public bool LastAttackWasCritical { get; private set; }
    public event Action<bool, int, int> AttackResolved;

    private void Awake()
    {
        ownerHealth = GetComponent<HealthSystem>();
        ApplyConfig(config);

        if (attackPoint == null)
        {
            attackPoint = transform;
        }
    }

    public bool TryAttack()
    {
        if (Time.time < lastAttackTime + attackCooldown)
        {
            return false;
        }

        lastAttackTime = Time.time;
        LastAttackWasCritical = RollCritical();
        int finalDamage = CalculateDamage(LastAttackWasCritical);
        int hitCount = ApplyDamage(attackPoint.position, attackRange, targetLayers, finalDamage);
        AttackResolved?.Invoke(LastAttackWasCritical, finalDamage, hitCount);

        if (debugAttackLogs)
        {
            Debug.Log($"{name} attacked for {finalDamage} damage. Critical: {LastAttackWasCritical}. Targets hit: {hitCount}.");
        }

        return true;
    }

    public int DealAreaDamage(Vector2 center, float radius, LayerMask targets, int damage, bool canCrit)
    {
        LastAttackWasCritical = canCrit && RollCritical();
        int finalDamage = CalculateDamage(LastAttackWasCritical, damage);
        int hitCount = ApplyDamage(center, radius, targets, finalDamage);
        AttackResolved?.Invoke(LastAttackWasCritical, finalDamage, hitCount);
        return hitCount;
    }

    public void SetBonusDamageMultiplier(float multiplier)
    {
        bonusDamageMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void SetBonusCriticalChance(float chance)
    {
        bonusCriticalChance = Mathf.Max(0f, chance);
    }

    public void SetBonusCriticalMultiplier(float multiplier)
    {
        bonusCriticalMultiplier = Mathf.Max(1f, multiplier);
    }

    public void Configure(AttackConfig newConfig, Transform newAttackPoint, LayerMask newTargetLayers)
    {
        config = newConfig;
        ApplyConfig(config);
        attackPoint = newAttackPoint != null ? newAttackPoint : transform;
        targetLayers = newTargetLayers;
    }

    public void Configure(
        Transform newAttackPoint,
        LayerMask newTargetLayers,
        float newAttackRange,
        float newAttackCooldown,
        int newBaseDamage,
        float newCriticalChance,
        float newCriticalMultiplier,
        bool shouldDebugAttackLogs)
    {
        config = null;
        attackPoint = newAttackPoint != null ? newAttackPoint : transform;
        targetLayers = newTargetLayers;
        attackRange = Mathf.Max(0.1f, newAttackRange);
        attackCooldown = Mathf.Max(0.01f, newAttackCooldown);
        baseDamage = Mathf.Max(1, newBaseDamage);
        criticalChance = Mathf.Clamp01(newCriticalChance);
        criticalMultiplier = Mathf.Max(1f, newCriticalMultiplier);
        debugAttackLogs = shouldDebugAttackLogs;
    }

    private int ApplyDamage(Vector2 center, float radius, LayerMask targets, int damage)
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(center, radius, targets);
        HashSet<HealthSystem> damagedTargets = new HashSet<HealthSystem>();

        foreach (Collider2D hitCollider in hitColliders)
        {
            HealthSystem targetHealth = hitCollider.GetComponentInParent<HealthSystem>();

            if (targetHealth == null || targetHealth == ownerHealth || damagedTargets.Contains(targetHealth))
            {
                continue;
            }

            damagedTargets.Add(targetHealth);
            targetHealth.TakeDamage(damage);
        }

        return damagedTargets.Count;
    }

    private bool RollCritical()
    {
        return UnityEngine.Random.value <= Mathf.Clamp01(criticalChance + bonusCriticalChance);
    }

    private int CalculateDamage(bool isCritical)
    {
        return CalculateDamage(isCritical, baseDamage);
    }

    private int CalculateDamage(bool isCritical, int sourceDamage)
    {
        float damage = Mathf.Max(1, sourceDamage) * bonusDamageMultiplier;

        if (isCritical)
        {
            damage *= criticalMultiplier * bonusCriticalMultiplier;
        }

        return Mathf.Max(1, Mathf.RoundToInt(damage));
    }

    private void ApplyConfig(AttackConfig newConfig)
    {
        if (newConfig == null)
        {
            return;
        }

        attackRange = Mathf.Max(0.1f, newConfig.attackRange);
        attackCooldown = Mathf.Max(0.01f, newConfig.attackCooldown);
        baseDamage = Mathf.Max(1, newConfig.baseDamage);
        criticalChance = Mathf.Clamp01(newConfig.criticalChance);
        criticalMultiplier = Mathf.Max(1f, newConfig.criticalMultiplier);
        debugAttackLogs = newConfig.debugAttackLogs;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 gizmoPosition = attackPoint != null ? attackPoint.position : transform.position;
        Gizmos.DrawWireSphere(gizmoPosition, attackRange);
    }

    private void OnValidate()
    {
        ApplyConfig(config);
        attackRange = Mathf.Max(0.1f, attackRange);
        attackCooldown = Mathf.Max(0.01f, attackCooldown);
        baseDamage = Mathf.Max(1, baseDamage);
        criticalChance = Mathf.Clamp01(criticalChance);
        criticalMultiplier = Mathf.Max(1f, criticalMultiplier);
    }
}
