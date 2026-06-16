using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AttackPhase
{
    None,
    WindUp,
    Active,
    Recovery
}

public class AttackSystem : MonoBehaviour
{
    [Header("Attack Setup")]
    [SerializeField] private AttackConfig config;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private float attackRange = 1.25f;
    [SerializeField] private float attackCooldown = 0.45f;

    [Header("Attack Timing")]
    [SerializeField] private float windUpDuration = 0.12f;
    [SerializeField] private float hitDuration = 0.08f;
    [SerializeField] private float recoveryDuration = 0.25f;

    [Header("Damage")]
    [SerializeField] private int baseDamage = 1;
    [SerializeField] private float criticalChance = 0.15f;
    [SerializeField] private float criticalMultiplier = 2f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 100f;

    [Header("Hit Timing")]
    [SerializeField] private bool useAnimationEvent = false;

    [Header("Debug")]
    [SerializeField] private bool debugAttackLogs = false;

    private float lastAttackTime = -Mathf.Infinity;
    private float bonusDamageMultiplier = 1f;
    private float bonusCriticalChance;
    private float bonusCriticalMultiplier = 1f;
    private HealthSystem ownerHealth;
    private Coroutine attackRoutine;
    private AttackPhase currentPhase = AttackPhase.None;
    private bool hitEventReceived;

    public Transform AttackPointTransform => attackPoint;
    public float AttackCooldown => attackCooldown;
    public float AttackRange => attackRange;
    public float KnockbackForce => knockbackForce;
    public bool LastAttackWasCritical { get; private set; }
    public AttackPhase CurrentPhase => currentPhase;
    public bool IsAttacking => currentPhase != AttackPhase.None;
    public float WindUpDuration => windUpDuration;
    public float RecoveryDuration => recoveryDuration;

    public event Action AttackStarted;
    public event Action<bool, int, int> AttackResolved;
    public event Action AttackCompleted;

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
        if (IsAttacking)
        {
            return false;
        }

        float totalCooldown = attackCooldown > 0.01f ? attackCooldown : windUpDuration + hitDuration + recoveryDuration;
        if (Time.time < lastAttackTime + totalCooldown)
        {
            return false;
        }

        lastAttackTime = Time.time;
        currentPhase = AttackPhase.WindUp;
        AttackStarted?.Invoke();

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
        }
        attackRoutine = StartCoroutine(AttackRoutine());

        return true;
    }

    public void TriggerHit()
    {
        hitEventReceived = true;
    }

    public void CancelAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        currentPhase = AttackPhase.None;
        hitEventReceived = false;
    }

    private IEnumerator AttackRoutine()
    {
        currentPhase = AttackPhase.WindUp;
        hitEventReceived = false;

        if (useAnimationEvent)
        {
            while (!hitEventReceived)
            {
                yield return null;
            }
        }
        else
        {
            float elapsed = 0f;
            while (!hitEventReceived && elapsed < windUpDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        currentPhase = AttackPhase.Active;
        LastAttackWasCritical = RollCritical();
        int finalDamage = CalculateDamage(LastAttackWasCritical);
        int hitCount = ApplyDamage(attackPoint.position, attackRange, targetLayers, finalDamage);
        AttackResolved?.Invoke(LastAttackWasCritical, finalDamage, hitCount);

        yield return new WaitForSeconds(hitDuration);

        currentPhase = AttackPhase.Recovery;

        yield return new WaitForSeconds(recoveryDuration);

        currentPhase = AttackPhase.None;
        attackRoutine = null;
        AttackCompleted?.Invoke();
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

    public void ConfigureTiming(float newWindUp, float newHitDuration, float newRecovery)
    {
        windUpDuration = Mathf.Max(0f, newWindUp);
        hitDuration = Mathf.Max(0.01f, newHitDuration);
        recoveryDuration = Mathf.Max(0f, newRecovery);
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
        attackCooldown = newConfig.EffectiveCooldown;
        baseDamage = Mathf.Max(1, newConfig.baseDamage);
        criticalChance = Mathf.Clamp01(newConfig.criticalChance);
        criticalMultiplier = Mathf.Max(1f, newConfig.criticalMultiplier);
        debugAttackLogs = newConfig.debugAttackLogs;
        windUpDuration = Mathf.Max(0f, newConfig.windUpDuration);
        hitDuration = Mathf.Max(0.01f, newConfig.hitDuration);
        recoveryDuration = Mathf.Max(0f, newConfig.recoveryDuration);
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
        windUpDuration = Mathf.Max(0f, windUpDuration);
        hitDuration = Mathf.Max(0.01f, hitDuration);
        recoveryDuration = Mathf.Max(0f, recoveryDuration);
    }
}