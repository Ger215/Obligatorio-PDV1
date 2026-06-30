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

public enum AbilityRarity
{
    Common,
    Rare,
    Epic
}

[CreateAssetMenu(fileName = "PlayerAbility", menuName = "Config/Abilities/Player Ability")]
public class PlayerAbilityDefinition : ScriptableObject
{
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    public PlayerAbilityType abilityType;
    public int cost = 10;
    public float cooldown = 8f;
    public int power = 3;
    public float duration = 3f;
    public float radius = 2f;

    [Header("Ruleta")]
    [Tooltip("Rareza de la habilidad. Solo informativa/visual; el peso real lo define rouletteWeight.")]
    public AbilityRarity rarity = AbilityRarity.Common;
    [Tooltip("Peso para la ruleta: a mayor valor, más probable que salga. Las habilidades fuertes " +
             "deberían tener peso bajo (más raras).")]
    public float rouletteWeight = 1f;

    private void OnValidate()
    {
        cost = Mathf.Max(0, cost);
        cooldown = Mathf.Max(0.1f, cooldown);
        power = Mathf.Max(1, power);
        duration = Mathf.Max(0.1f, duration);
        radius = Mathf.Max(0.1f, radius);
        rouletteWeight = Mathf.Max(0.01f, rouletteWeight);
    }
}
