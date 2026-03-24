using UnityEngine;

[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Config/Enemy")]
public class EnemyConfig : ScriptableObject
{
    public HealthConfig healthConfig;
    public AttackConfig attackConfig;
    public float moveSpeed = 3f;
    public float jumpForce = 11f;
    public float attackDistance = 1.1f;
    public float verticalAttackTolerance = 1f;
    public float jumpTriggerHeight = 1.35f;
    public float attackPointDistance = 0.6f;
    public float repathDelay = 0.5f;

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        jumpForce = Mathf.Max(0.1f, jumpForce);
        attackDistance = Mathf.Max(0.1f, attackDistance);
        verticalAttackTolerance = Mathf.Max(0.1f, verticalAttackTolerance);
        jumpTriggerHeight = Mathf.Max(0.1f, jumpTriggerHeight);
        attackPointDistance = Mathf.Max(0.1f, attackPointDistance);
        repathDelay = Mathf.Max(0.1f, repathDelay);
    }
}
