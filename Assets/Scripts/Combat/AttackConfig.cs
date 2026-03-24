using UnityEngine;

[CreateAssetMenu(fileName = "AttackConfig", menuName = "Config/Combat/Attack")]
public class AttackConfig : ScriptableObject
{
    public float attackRange = 1.25f;
    public float attackCooldown = 0.45f;
    public int baseDamage = 1;
    public float perfectHitMultiplier = 2f;
    public float weakHitMultiplier = 0.5f;
    public bool useRhythmTiming;
    public bool debugAttackLogs = true;

    private void OnValidate()
    {
        attackRange = Mathf.Max(0.1f, attackRange);
        attackCooldown = Mathf.Max(0.01f, attackCooldown);
        baseDamage = Mathf.Max(1, baseDamage);
        perfectHitMultiplier = Mathf.Max(1f, perfectHitMultiplier);
        weakHitMultiplier = Mathf.Clamp(weakHitMultiplier, 0.1f, 1f);
    }
}
