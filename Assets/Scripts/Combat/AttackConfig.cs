using UnityEngine;

[CreateAssetMenu(fileName = "AttackConfig", menuName = "Config/Combat/Attack")]
public class AttackConfig : ScriptableObject
{
    public float attackRange = 1.25f;
    public float attackCooldown = 0.45f;
    public int baseDamage = 1;
    [Range(0f, 1f)] public float criticalChance = 0.15f;
    public float criticalMultiplier = 2f;
    public bool debugAttackLogs = true;

    private void OnValidate()
    {
        attackRange = Mathf.Max(0.1f, attackRange);
        attackCooldown = Mathf.Max(0.01f, attackCooldown);
        baseDamage = Mathf.Max(1, baseDamage);
        criticalChance = Mathf.Clamp01(criticalChance);
        criticalMultiplier = Mathf.Max(1f, criticalMultiplier);
    }
}
