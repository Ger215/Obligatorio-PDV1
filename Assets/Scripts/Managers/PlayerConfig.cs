using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfig", menuName = "Config/Player")]
public class PlayerConfig : ScriptableObject
{
    public HealthConfig healthConfig;
    public AttackConfig attackConfig;
    public float moveSpeed = 6f;
    public float jumpForce = 13f;
    public float dashSpeed = 14f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.75f;
    public float attackPointDistance = 0.75f;

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        jumpForce = Mathf.Max(0.1f, jumpForce);
        dashSpeed = Mathf.Max(moveSpeed, dashSpeed);
        dashDuration = Mathf.Max(0.01f, dashDuration);
        dashCooldown = Mathf.Max(0.01f, dashCooldown);
        attackPointDistance = Mathf.Max(0.1f, attackPointDistance);
    }
}
