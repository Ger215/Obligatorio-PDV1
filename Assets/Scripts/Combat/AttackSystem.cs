using System.Collections.Generic;
using UnityEngine;

public class AttackSystem : MonoBehaviour
{
    [Header("Attack Setup")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private float attackRange = 1.25f;
    [SerializeField] private float attackCooldown = 0.45f;

    [Header("Damage")]
    [SerializeField] private int baseDamage = 1;
    [SerializeField] private float perfectHitMultiplier = 2f;
    [SerializeField] private float weakHitMultiplier = 0.5f;

    [Header("Rhythm")]
    [SerializeField] private bool useRhythmTiming;
    [SerializeField] private RhythmChecker rhythmChecker;

    [Header("Debug")]
    [SerializeField] private bool debugAttackLogs = true;

    private float lastAttackTime = -Mathf.Infinity;
    private HealthSystem ownerHealth;

    public Transform AttackPointTransform => attackPoint;
    public float AttackCooldown => attackCooldown;
    public RhythmHitResult LastHitResult { get; private set; } = RhythmHitResult.None;

    private void Awake()
    {
        ownerHealth = GetComponent<HealthSystem>();

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
        LastHitResult = ResolveHitResult();
        int finalDamage = CalculateDamage(LastHitResult);

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, targetLayers);
        HashSet<HealthSystem> damagedTargets = new HashSet<HealthSystem>();

        foreach (Collider2D hitCollider in hitColliders)
        {
            HealthSystem targetHealth = hitCollider.GetComponentInParent<HealthSystem>();

            if (targetHealth == null || targetHealth == ownerHealth || damagedTargets.Contains(targetHealth))
            {
                continue;
            }

            damagedTargets.Add(targetHealth);
            targetHealth.TakeDamage(finalDamage);
        }

        if (debugAttackLogs)
        {
            Debug.Log($"{name} attacked with result {LastHitResult} and dealt {finalDamage} damage to {damagedTargets.Count} target(s).");
        }

        return true;
    }

    private RhythmHitResult ResolveHitResult()
    {
        if (!useRhythmTiming)
        {
            return RhythmHitResult.None;
        }

        if (rhythmChecker == null)
        {
            Debug.LogWarning($"AttackSystem on {name} is set to use rhythm timing but has no RhythmChecker.");
            return RhythmHitResult.Weak;
        }

        return rhythmChecker.CheckTiming();
    }

    private int CalculateDamage(RhythmHitResult hitResult)
    {
        float multiplier = 1f;

        if (hitResult == RhythmHitResult.Perfect)
        {
            multiplier = perfectHitMultiplier;
        }
        else if (hitResult == RhythmHitResult.Weak)
        {
            multiplier = weakHitMultiplier;
        }

        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
    }

    public void Configure(
        Transform newAttackPoint,
        LayerMask newTargetLayers,
        float newAttackRange,
        float newAttackCooldown,
        int newBaseDamage,
        float newPerfectHitMultiplier,
        float newWeakHitMultiplier,
        bool shouldUseRhythmTiming,
        RhythmChecker newRhythmChecker,
        bool shouldDebugAttackLogs)
    {
        attackPoint = newAttackPoint != null ? newAttackPoint : transform;
        targetLayers = newTargetLayers;
        attackRange = Mathf.Max(0.1f, newAttackRange);
        attackCooldown = Mathf.Max(0.01f, newAttackCooldown);
        baseDamage = Mathf.Max(1, newBaseDamage);
        perfectHitMultiplier = Mathf.Max(1f, newPerfectHitMultiplier);
        weakHitMultiplier = Mathf.Clamp(newWeakHitMultiplier, 0.1f, 1f);
        useRhythmTiming = shouldUseRhythmTiming;
        rhythmChecker = newRhythmChecker;
        debugAttackLogs = shouldDebugAttackLogs;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 gizmoPosition = attackPoint != null ? attackPoint.position : transform.position;
        Gizmos.DrawWireSphere(gizmoPosition, attackRange);
    }

    private void OnValidate()
    {
        attackRange = Mathf.Max(0.1f, attackRange);
        attackCooldown = Mathf.Max(0.01f, attackCooldown);
        baseDamage = Mathf.Max(1, baseDamage);
        perfectHitMultiplier = Mathf.Max(1f, perfectHitMultiplier);
        weakHitMultiplier = Mathf.Clamp(weakHitMultiplier, 0.1f, 1f);
    }
}
