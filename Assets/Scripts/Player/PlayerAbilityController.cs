using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(AttackSystem))]
[RequireComponent(typeof(HealthSystem))]
public class PlayerAbilityController : MonoBehaviour
{
    [SerializeField] private PlayerConfig playerConfig;
    [SerializeField] private int maxLearnedAbilities = 3;

    private readonly List<PlayerAbilityDefinition> learnedAbilities = new List<PlayerAbilityDefinition>();
    private readonly Dictionary<PlayerAbilityDefinition, float> cooldowns = new Dictionary<PlayerAbilityDefinition, float>();

    private PlayerController playerController;
    private AttackSystem attackSystem;
    private HealthSystem healthSystem;

    public IReadOnlyList<PlayerAbilityDefinition> LearnedAbilities => learnedAbilities;
    public IReadOnlyDictionary<PlayerAbilityDefinition, float> Cooldowns => cooldowns;
    public bool IsAbilityCapacityReached => learnedAbilities.Count >= maxLearnedAbilities;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        attackSystem = GetComponent<AttackSystem>();
        healthSystem = GetComponent<HealthSystem>();
        maxLearnedAbilities = playerConfig != null ? playerConfig.maxLearnedAbilities : maxLearnedAbilities;
    }

    public void SetMaxLearnedAbilities(int maxAbilities)
    {
        maxLearnedAbilities = Mathf.Clamp(maxAbilities, 1, 3);
    }

    public bool LearnAbility(PlayerAbilityDefinition ability)
    {
        if (ability == null || learnedAbilities.Contains(ability) || IsAbilityCapacityReached)
        {
            return false;
        }

        learnedAbilities.Add(ability);
        GameHudController.Instance?.RefreshAbilitySlots(learnedAbilities, cooldowns);
        return true;
    }

    public bool TryUseAbilitySlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= learnedAbilities.Count)
        {
            return false;
        }

        PlayerAbilityDefinition ability = learnedAbilities[slotIndex];

        if (cooldowns.TryGetValue(ability, out float availableTime) && Time.time < availableTime)
        {
            return false;
        }

        ExecuteAbility(ability);
        cooldowns[ability] = Time.time + ability.cooldown;
        GameHudController.Instance?.RefreshAbilitySlots(learnedAbilities, cooldowns);
        AudioManager.Instance?.PlayAbility(ability.abilityType);
        return true;
    }

    private void ExecuteAbility(PlayerAbilityDefinition ability)
    {
        switch (ability.abilityType)
        {
            case PlayerAbilityType.DashStrike:
                StartCoroutine(DashStrikeRoutine(ability));
                break;
            case PlayerAbilityType.HealPulse:
                healthSystem.Heal(ability.power);
                break;
            case PlayerAbilityType.Shockwave:
                attackSystem.DealAreaDamage(transform.position, ability.radius, 1 << 9, ability.power, true);
                break;
            case PlayerAbilityType.Berserk:
                StartCoroutine(BerserkRoutine(ability));
                break;
            case PlayerAbilityType.GuardianAura:
                StartCoroutine(GuardianAuraRoutine(ability));
                break;
            case PlayerAbilityType.BladeStorm:
                StartCoroutine(BladeStormRoutine(ability));
                break;
        }
    }

    private IEnumerator DashStrikeRoutine(PlayerAbilityDefinition ability)
    {
        Rigidbody2D rb = playerController.Rigidbody;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(playerController.FacingDirection * Mathf.Max(10f, ability.power * 2f), 0f);
        float elapsed = 0f;

        while (elapsed < 0.2f)
        {
            attackSystem.DealAreaDamage(playerController.AttackSystem.AttackPointTransform.position, ability.radius, 1 << 9, ability.power, true);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.gravityScale = originalGravity;
    }

    private IEnumerator BerserkRoutine(PlayerAbilityDefinition ability)
    {
        attackSystem.SetBonusDamageMultiplier(1f + (ability.power * 0.2f));
        attackSystem.SetBonusCriticalChance(0.1f);
        yield return new WaitForSeconds(ability.duration);
        attackSystem.SetBonusDamageMultiplier(1f);
        attackSystem.SetBonusCriticalChance(0f);
    }

    private IEnumerator GuardianAuraRoutine(PlayerAbilityDefinition ability)
    {
        healthSystem.SetIncomingDamageMultiplier(Mathf.Clamp(1f - (ability.power * 0.1f), 0.3f, 1f));
        yield return new WaitForSeconds(ability.duration);
        healthSystem.SetIncomingDamageMultiplier(1f);
    }

    private IEnumerator BladeStormRoutine(PlayerAbilityDefinition ability)
    {
        float elapsed = 0f;

        while (elapsed < ability.duration)
        {
            attackSystem.DealAreaDamage(transform.position, ability.radius, 1 << 9, ability.power, true);
            elapsed += 0.35f;
            yield return new WaitForSeconds(0.35f);
        }
    }
}
