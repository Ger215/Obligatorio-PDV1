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

    private PlayerAbilityDefinition[] learnedAbilities;
    private readonly Dictionary<PlayerAbilityDefinition, float> cooldowns = new Dictionary<PlayerAbilityDefinition, float>();
    private AbilityPickup nearbyPickup;

    [Header("Berserk")]
    [Tooltip("Color del aura que rodea al jugador mientras Berserk está activo.")]
    [SerializeField] private Color berserkAuraColor = new Color(1f, 0.35f, 0.05f, 1f);

    [Header("Guardian Aura")]
    [Tooltip("Color del escudo que rodea al jugador mientras Guardian Aura está activa.")]
    [SerializeField] private Color guardianShieldColor = new Color(0.3f, 0.6f, 1f, 0.35f);

    [Header("Blade Storm")]
    [Tooltip("Color de las cuchillas que rotan alrededor del jugador durante Blade Storm.")]
    [SerializeField] private Color bladeStormColor = new Color(0.8f, 0.85f, 1f, 0.9f);

    private PlayerController playerController;
    private AttackSystem attackSystem;
    private HealthSystem healthSystem;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer berserkAuraRenderer;
    private Coroutine berserkAuraRoutine;
    private SpriteRenderer guardianShieldRenderer;
    private Coroutine guardianShieldRoutine;

    public IReadOnlyList<PlayerAbilityDefinition> LearnedAbilities => learnedAbilities;
    public IReadOnlyDictionary<PlayerAbilityDefinition, float> Cooldowns => cooldowns;
    public bool IsAbilityCapacityReached => System.Array.TrueForAll(learnedAbilities, a => a != null);

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        attackSystem = GetComponent<AttackSystem>();
        healthSystem = GetComponent<HealthSystem>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        maxLearnedAbilities = playerConfig != null ? playerConfig.maxLearnedAbilities : maxLearnedAbilities;
        learnedAbilities = new PlayerAbilityDefinition[maxLearnedAbilities];
    }

    public void SetMaxLearnedAbilities(int maxAbilities)
    {
        maxLearnedAbilities = Mathf.Clamp(maxAbilities, 1, 3);
    }

    public void SetNearbyPickup(AbilityPickup pickup)
    {
        nearbyPickup = pickup;
    }

    public void ClearNearbyPickup(AbilityPickup pickup)
    {
        if (nearbyPickup == pickup)
        {
            nearbyPickup = null;
        }
    }

    public bool LearnAbility(PlayerAbilityDefinition ability)
    {
        if (ability == null || System.Array.IndexOf(learnedAbilities, ability) >= 0)
        {
            return false;
        }

        int emptySlot = System.Array.IndexOf(learnedAbilities, null);
        if (emptySlot < 0)
        {
            return false;
        }

        learnedAbilities[emptySlot] = ability;
        GameHudController.Instance?.RefreshAbilitySlots(learnedAbilities, cooldowns);
        return true;
    }

    public bool LearnAbilityInSlot(PlayerAbilityDefinition ability, int slotIndex)
    {
        if (ability == null || slotIndex < 0 || slotIndex >= learnedAbilities.Length)
        {
            return false;
        }

        int existingIndex = System.Array.IndexOf(learnedAbilities, ability);
        if (existingIndex >= 0)
        {
            learnedAbilities[existingIndex] = null;
        }

        PlayerAbilityDefinition replaced = learnedAbilities[slotIndex];
        if (replaced != null)
        {
            cooldowns.Remove(replaced);
        }

        learnedAbilities[slotIndex] = ability;
        GameHudController.Instance?.RefreshAbilitySlots(learnedAbilities, cooldowns);
        return true;
    }

    public bool HandleAbilitySlotInput(int slotIndex)
    {
        if (nearbyPickup != null && nearbyPickup.AssignToSlot(slotIndex))
        {
            return true;
        }

        return TryUseAbilitySlot(slotIndex);
    }

    public bool TryUseAbilitySlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= learnedAbilities.Length)
        {
            return false;
        }

        PlayerAbilityDefinition ability = learnedAbilities[slotIndex];
        if (ability == null)
        {
            return false;
        }

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
                StartCoroutine(ShockwaveRoutine(ability));
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
        playerController.TriggerDash();

        while (playerController.IsDashing)
        {
            attackSystem.DealAreaDamage(playerController.AttackSystem.AttackPointTransform.position, ability.radius, 1 << 8, ability.power, true);
            yield return null;
        }
    }

    private IEnumerator ShockwaveRoutine(PlayerAbilityDefinition ability)
    {
        attackSystem.DealAreaDamage(transform.position, ability.radius, 1 << 8, ability.power, true);
        ApplyRadialKnockback(transform.position, ability.radius);
        yield return ShockwaveBurstRoutine(ability.radius);
    }

    private void ApplyRadialKnockback(Vector2 center, float radius)
    {
        float knockbackForce = attackSystem.KnockbackForce;
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(center, radius, 1 << 8);

        foreach (Collider2D hitCollider in hitColliders)
        {
            EnemyController enemy = hitCollider.GetComponentInParent<EnemyController>();
            if (enemy == null) continue;

            float direction = hitCollider.transform.position.x >= center.x ? 1f : -1f;
            enemy.ApplyKnockback(direction * knockbackForce);
        }
    }

    private IEnumerator ShockwaveBurstRoutine(float radius)
    {
        var burstGo = new GameObject("ShockwaveBurst");
        burstGo.transform.SetParent(transform, false);
        burstGo.transform.localPosition = Vector3.zero;

        SpriteRenderer burstRenderer = burstGo.AddComponent<SpriteRenderer>();
        burstRenderer.sprite = CreateGlowSprite();
        burstRenderer.color = new Color(1f, 0.9f, 0.2f, 0.6f);

        if (spriteRenderer != null)
        {
            burstRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            burstRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        }

        const float burstDuration = 0.3f;
        float elapsed = 0f;

        while (elapsed < burstDuration)
        {
            float t = elapsed / burstDuration;
            burstGo.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, radius, t);
            Color color = burstRenderer.color;
            color.a = Mathf.Lerp(0.6f, 0f, t);
            burstRenderer.color = color;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(burstGo);
    }

    private IEnumerator BerserkRoutine(PlayerAbilityDefinition ability)
    {
        attackSystem.SetBonusDamageMultiplier(1f + (ability.power * 0.2f));
        attackSystem.SetBonusCriticalChance(0.1f);

        if (berserkAuraRoutine != null)
        {
            StopCoroutine(berserkAuraRoutine);
        }
        berserkAuraRoutine = StartCoroutine(BerserkAuraRoutine(ability.duration));

        yield return new WaitForSeconds(ability.duration);

        attackSystem.SetBonusDamageMultiplier(1f);
        attackSystem.SetBonusCriticalChance(0f);
    }

    private IEnumerator BerserkAuraRoutine(float duration)
    {
        if (berserkAuraRenderer == null)
        {
            berserkAuraRenderer = CreateBerserkAura();
        }

        berserkAuraRenderer.gameObject.SetActive(true);

        const float baseScale = 1f;
        const float pulseAmplitude = 0.15f;
        const float pulseSpeed = 6f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            berserkAuraRenderer.transform.localScale = Vector3.one * (baseScale + pulse * pulseAmplitude);
            Color color = berserkAuraColor;
            color.a = 0.35f + pulse * 0.3f;
            berserkAuraRenderer.color = color;
            elapsed += Time.deltaTime;
            yield return null;
        }

        berserkAuraRenderer.gameObject.SetActive(false);
        berserkAuraRoutine = null;
    }

    private SpriteRenderer CreateBerserkAura()
    {
        var auraGo = new GameObject("BerserkAura");
        auraGo.transform.SetParent(transform, false);
        auraGo.transform.localPosition = Vector3.zero;

        SpriteRenderer auraRenderer = auraGo.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = CreateGlowSprite();
        auraRenderer.color = berserkAuraColor;

        if (spriteRenderer != null)
        {
            auraRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            auraRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        }

        auraGo.SetActive(false);
        return auraRenderer;
    }

    private static Sprite CreateGlowSprite()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxDist = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha *= alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
    }

    private IEnumerator GuardianAuraRoutine(PlayerAbilityDefinition ability)
    {
        healthSystem.SetDamageShield(true);

        if (guardianShieldRenderer == null)
        {
            guardianShieldRenderer = CreateGuardianShield();
        }

        if (guardianShieldRoutine != null)
        {
            StopCoroutine(guardianShieldRoutine);
        }
        guardianShieldRoutine = StartCoroutine(GuardianShieldVisualRoutine());
        yield return null;
    }

    private IEnumerator GuardianShieldVisualRoutine()
    {
        guardianShieldRenderer.gameObject.SetActive(true);

        const float baseScale = 0.3f;
        const float pulseAmplitude = 0.02f;
        const float pulseSpeed = 3f;
        const float rotationSpeed = 20f;

        while (healthSystem.HasDamageShield)
        {
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            guardianShieldRenderer.transform.localScale = Vector3.one * (baseScale + pulse * pulseAmplitude);
            guardianShieldRenderer.transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
            Color color = guardianShieldColor;
            color.a = guardianShieldColor.a + pulse * 0.2f;
            guardianShieldRenderer.color = color;
            yield return null;
        }

        guardianShieldRenderer.gameObject.SetActive(false);
        guardianShieldRoutine = null;
    }

    private SpriteRenderer CreateGuardianShield()
    {
        var shieldGo = new GameObject("GuardianShield");
        shieldGo.transform.SetParent(transform, false);
        shieldGo.transform.localPosition = Vector3.zero;

        SpriteRenderer shieldRenderer = shieldGo.AddComponent<SpriteRenderer>();
        shieldRenderer.sprite = CreateShieldRingSprite();
        shieldRenderer.color = guardianShieldColor;

        if (spriteRenderer != null)
        {
            shieldRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            shieldRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        }

        shieldGo.SetActive(false);
        return shieldRenderer;
    }

    private static Sprite CreateShieldRingSprite()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxDist = size / 2f;
        const float ringRadius = 0.82f;
        const float ringWidth = 0.08f;
        const float fillAlpha = 0.04f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float rim = Mathf.Clamp01(1f - Mathf.Abs(dist - ringRadius) / ringWidth);
                float fill = dist < ringRadius ? fillAlpha : 0f;
                float alpha = dist > 1f ? 0f : Mathf.Max(rim, fill);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
    }

    private IEnumerator BladeStormRoutine(PlayerAbilityDefinition ability)
    {
        const int bladeCount = 4;
        const float rotationSpeed = 420f;
        const float tickInterval = 0.35f;

        Transform pivot = CreateBladeStormBlades(bladeCount);

        float elapsed = 0f;
        float tickTimer = 0f;
        attackSystem.DealAreaDamage(transform.position, ability.radius, 1 << 8, ability.power, true);

        while (elapsed < ability.duration)
        {
            pivot.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);

            tickTimer += Time.deltaTime;
            if (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;
                attackSystem.DealAreaDamage(transform.position, ability.radius, 1 << 8, ability.power, true);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(pivot.gameObject);
    }

    private Transform CreateBladeStormBlades(int bladeCount)
    {
        var pivotGo = new GameObject("BladeStormPivot");
        pivotGo.transform.SetParent(transform, false);
        pivotGo.transform.localPosition = Vector3.zero;

        // El padre (Player) tiene una escala grande (m_LocalScale ~4.18), así que las
        // posiciones y tamaños locales se contrarrestan dividiendo por esa escala para
        // que las cuchillas terminen con el radio/tamaño correcto en unidades de mundo.
        float parentScale = Mathf.Max(0.01f, transform.localScale.x);
        const float visualOrbitRadius = 1.5f;
        float localRadius = visualOrbitRadius / parentScale;
        const float bladeWorldWidth = 0.35f;
        const float bladeWorldHeight = 1f;
        float bladeScaleX = bladeWorldWidth / (2f * parentScale);
        float bladeScaleY = bladeWorldHeight / (2f * parentScale);

        for (int i = 0; i < bladeCount; i++)
        {
            float angle = i * (360f / bladeCount);

            var bladeGo = new GameObject("Blade");
            bladeGo.transform.SetParent(pivotGo.transform, false);
            bladeGo.transform.localPosition = Quaternion.Euler(0f, 0f, angle) * Vector3.right * localRadius;
            bladeGo.transform.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);
            bladeGo.transform.localScale = new Vector3(bladeScaleX, bladeScaleY, 1f);

            SpriteRenderer bladeRenderer = bladeGo.AddComponent<SpriteRenderer>();
            bladeRenderer.sprite = CreateGlowSprite();
            bladeRenderer.color = bladeStormColor;

            if (spriteRenderer != null)
            {
                bladeRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                bladeRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            }
        }

        return pivotGo.transform;
    }
}
