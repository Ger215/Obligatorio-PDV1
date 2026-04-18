using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfig", menuName = "Config/Player")]
public class PlayerConfig : ScriptableObject
{
    public HealthConfig healthConfig;
    public AttackConfig attackConfig;
    public float moveSpeed = 6f;
    public float acceleration = 60f;
    public float deceleration = 80f;
    public float jumpForce = 13f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.1f;
    public float fallGravityMultiplier = 2.5f;
    public float jumpCutGravityMultiplier = 2f;
    public float maxFallSpeed = 18f;
    public bool canDoubleJump = true;
    public float dashSpeed = 14f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.75f;
    public float attackPointDistance = 0.75f;
    public int maxLearnedAbilities = 3;

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        acceleration = Mathf.Max(0.1f, acceleration);
        deceleration = Mathf.Max(0.1f, deceleration);
        jumpForce = Mathf.Max(0.1f, jumpForce);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
        fallGravityMultiplier = Mathf.Max(1f, fallGravityMultiplier);
        jumpCutGravityMultiplier = Mathf.Max(1f, jumpCutGravityMultiplier);
        maxFallSpeed = Mathf.Max(1f, maxFallSpeed);
        dashSpeed = Mathf.Max(moveSpeed, dashSpeed);
        dashDuration = Mathf.Max(0.01f, dashDuration);
        dashCooldown = Mathf.Max(0.01f, dashCooldown);
        attackPointDistance = Mathf.Max(0.1f, attackPointDistance);
        maxLearnedAbilities = Mathf.Clamp(maxLearnedAbilities, 1, 3);
    }
}
