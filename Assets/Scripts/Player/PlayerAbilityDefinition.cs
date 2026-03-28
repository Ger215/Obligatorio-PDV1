using UnityEngine;

public enum PlayerAbilityType
{
    DashStrike,
    HealPulse,
    Shockwave,
    Berserk,
    GuardianAura,
    BladeStorm
}

[CreateAssetMenu(fileName = "PlayerAbility", menuName = "Config/Abilities/Player Ability")]
public class PlayerAbilityDefinition : ScriptableObject
{
    public string displayName;
    [TextArea] public string description;
    public PlayerAbilityType abilityType;
    public int cost = 10;
    public float cooldown = 8f;
    public int power = 3;
    public float duration = 3f;
    public float radius = 2f;

    private void OnValidate()
    {
        cost = Mathf.Max(0, cost);
        cooldown = Mathf.Max(0.1f, cooldown);
        power = Mathf.Max(1, power);
        duration = Mathf.Max(0.1f, duration);
        radius = Mathf.Max(0.1f, radius);
    }
}
