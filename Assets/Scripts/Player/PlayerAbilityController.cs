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

    [Header("Dash Strike")]
    [Tooltip("Color del rastro de imágenes residuales que deja el jugador durante Dash Strike.")]
    [SerializeField] private Color dashTrailColor = new Color(0.6f, 0.9f, 1f, 0.5f);

    [Header("Heal Pulse")]
    [Tooltip("Color del pulso de curación que emite el jugador al usar Heal Pulse.")]
    [SerializeField] private Color healPulseColor = new Color(0.3f, 1f, 0.4f, 0.6f);

    [Header("Guardian Aura")]
    [Tooltip("Color del escudo que rodea al jugador mientras Guardian Aura está activa.")]
    [SerializeField] private Color guardianShieldColor = new Color(0.3f, 0.6f, 1f, 0.35f);

    [Header("Blade Storm")]
    [Tooltip("Color de las cuchillas que rotan alrededor del jugador durante Blade Storm.")]
    [SerializeField] private Color bladeStormColor = new Color(0.8f, 0.85f, 1f, 0.9f);

    [Header("Berserk")]
    [Tooltip("Color de tiñe pulsante aplicado al sprite del jugador mientras Berserk está activo.")]
    [SerializeField] private Color berserkTintColor = new Color(1f, 0.25f, 0.15f, 1f);

    private PlayerController playerController;
    private AttackSystem attackSystem;
    private HealthSystem healthSystem;
    private SpriteRenderer spriteRenderer;
    private readonly Dictionary<PlayerAbilityDefinition, float> activeEffects = new Dictionary<PlayerAbilityDefinition, float>();
    private SpriteRenderer guardianShieldRenderer;
    private Coroutine guardianShieldRoutine;

    public IReadOnlyList<PlayerAbilityDefinition> LearnedAbilities => learnedAbilities;
    public IReadOnlyDictionary<PlayerAbilityDefinition, float> Cooldowns => cooldowns;
    public IReadOnlyDictionary<PlayerAbilityDefinition, float> ActiveEffects => activeEffects;
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
        GameHudController.Instance?.RefreshAbilitySlots(learnedAbilities, cooldowns, activeEffects);
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
        GameHudController.Instance?.RefreshAbilitySlots(learnedAbilities, cooldowns, activeEffects);
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
        GameHudController.Instance?.RefreshAbilitySlots(learnedAbilities, cooldowns, activeEffects);
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
                StartCoroutine(HealPulseRoutine(ability));
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

        // El dash barre su trayectoria varios frames; este set asegura que a cada enemigo se le
        // aplique el daño una sola vez por dash (antes pegaba por frame, escalando con los FPS).
        var alreadyHit = new HashSet<HealthSystem>();

        while (playerController.IsDashing)
        {
            attackSystem.DealAreaDamage(playerController.AttackSystem.AttackPointTransform.position, ability.radius, 1 << 8, ability.power, true, alreadyHit);
            SpawnDashTrailGhost();
            yield return null;
        }
    }

    private void SpawnDashTrailGhost()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return;
        }

        var ghostGo = new GameObject("DashTrailGhost");
        ghostGo.transform.SetParent(transform.parent, true);
        ghostGo.transform.position = spriteRenderer.transform.position;
        ghostGo.transform.rotation = spriteRenderer.transform.rotation;
        ghostGo.transform.localScale = spriteRenderer.transform.lossyScale;

        SpriteRenderer ghostRenderer = ghostGo.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = spriteRenderer.sprite;
        ghostRenderer.flipX = spriteRenderer.flipX;
        ghostRenderer.flipY = spriteRenderer.flipY;
        ghostRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        ghostRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        ghostRenderer.color = dashTrailColor;

        StartCoroutine(FadeAndDestroy(ghostRenderer, 0.2f));
    }

    private static IEnumerator FadeAndDestroy(SpriteRenderer fadingRenderer, float duration)
    {
        float elapsed = 0f;
        Color startColor = fadingRenderer.color;

        while (elapsed < duration)
        {
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, elapsed / duration);
            fadingRenderer.color = color;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(fadingRenderer.gameObject);
    }

    private IEnumerator HealPulseRoutine(PlayerAbilityDefinition ability)
    {
        bool wasDamaged = healthSystem.CurrentHealth < healthSystem.MaxHealth;
        healthSystem.Heal(ability.power);

        if (wasDamaged)
        {
            yield return HealPulseBurstRoutine(ability.radius);
        }
    }

    private IEnumerator HealPulseBurstRoutine(float radius)
    {
        var burstGo = new GameObject("HealPulseBurst");
        burstGo.transform.SetParent(transform, false);
        burstGo.transform.localPosition = Vector3.zero;

        SpriteRenderer burstRenderer = burstGo.AddComponent<SpriteRenderer>();
        burstRenderer.sprite = CreateGlowSprite();
        burstRenderer.color = healPulseColor;

        if (spriteRenderer != null)
        {
            burstRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            burstRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        }

        const float burstDuration = 0.4f;
        float elapsed = 0f;

        while (elapsed < burstDuration)
        {
            float t = elapsed / burstDuration;
            burstGo.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, radius, t);
            Color color = burstRenderer.color;
            color.a = Mathf.Lerp(healPulseColor.a, 0f, t);
            burstRenderer.color = color;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(burstGo);
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

        var ringGo = new GameObject("ShockwaveRing");
        ringGo.transform.SetParent(transform, false);
        ringGo.transform.localPosition = Vector3.zero;

        SpriteRenderer ringRenderer = ringGo.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CreateRingOutlineSprite();
        ringRenderer.color = new Color(1f, 1f, 0.6f, 0.9f);

        if (spriteRenderer != null)
        {
            burstRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            burstRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
            ringRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            ringRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
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

            ringGo.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, radius * 0.7f, t);
            Color ringColor = ringRenderer.color;
            ringColor.a = Mathf.Lerp(0.9f, 0f, t);
            ringRenderer.color = ringColor;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(burstGo);
        Destroy(ringGo);
    }

    private IEnumerator BerserkRoutine(PlayerAbilityDefinition ability)
    {
        attackSystem.SetBonusDamageMultiplier(1f + (ability.power * 0.2f));
        attackSystem.SetBonusCriticalChance(0.1f);
        activeEffects[ability] = Time.time + ability.duration;

        Color originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        float elapsed = 0f;

        while (elapsed < ability.duration)
        {
            if (spriteRenderer != null)
            {
                float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
                spriteRenderer.color = Color.Lerp(originalColor, berserkTintColor, Mathf.Lerp(0.35f, 0.65f, pulse));
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        attackSystem.SetBonusDamageMultiplier(1f);
        attackSystem.SetBonusCriticalChance(0f);
        activeEffects.Remove(ability);
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

    private static Sprite CreateRingOutlineSprite()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxDist = size / 2f;
        const float ringRadius = 0.9f;
        const float ringWidth = 0.06f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float rim = Mathf.Clamp01(1f - Mathf.Abs(dist - ringRadius) / ringWidth);
                float alpha = dist > 1f ? 0f : rim;
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
        const float appearDuration = 0.2f;

        float appearElapsed = 0f;
        while (appearElapsed < appearDuration)
        {
            float t = appearElapsed / appearDuration;
            guardianShieldRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0f, baseScale, t);
            Color color = guardianShieldColor;
            color.a = Mathf.Lerp(guardianShieldColor.a + 0.35f, guardianShieldColor.a, t);
            guardianShieldRenderer.color = color;
            appearElapsed += Time.deltaTime;
            yield return null;
        }

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
        StartCoroutine(GuardianShieldShatterRoutine());
    }

    private IEnumerator GuardianShieldShatterRoutine()
    {
        var shatterGo = new GameObject("GuardianShieldShatter");
        shatterGo.transform.SetParent(transform, false);
        shatterGo.transform.localPosition = Vector3.zero;

        SpriteRenderer shatterRenderer = shatterGo.AddComponent<SpriteRenderer>();
        shatterRenderer.sprite = CreateGlowSprite();
        shatterRenderer.color = guardianShieldColor;

        if (spriteRenderer != null)
        {
            shatterRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            shatterRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        }

        const float shatterDuration = 0.35f;
        const float startScale = 0.3f;
        const float endScale = 0.7f;
        float elapsed = 0f;

        while (elapsed < shatterDuration)
        {
            float t = elapsed / shatterDuration;
            shatterGo.transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            Color color = guardianShieldColor;
            color.a = Mathf.Lerp(guardianShieldColor.a + 0.4f, 0f, t);
            shatterRenderer.color = color;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(shatterGo);
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
        const float fadeDuration = 0.15f;

        Transform pivot = CreateBladeStormBlades(bladeCount);
        SpriteRenderer[] bladeRenderers = pivot.GetComponentsInChildren<SpriteRenderer>();

        float elapsed = 0f;
        float tickTimer = 0f;
        attackSystem.DealAreaDamage(transform.position, ability.radius, 1 << 8, ability.power, true);
        SpawnBladeStormPulse();

        while (elapsed < ability.duration)
        {
            pivot.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);

            float fadeIn = Mathf.Clamp01(elapsed / fadeDuration);
            float fadeOut = Mathf.Clamp01((ability.duration - elapsed) / fadeDuration);
            float alphaMult = Mathf.Min(fadeIn, fadeOut);
            foreach (SpriteRenderer bladeRenderer in bladeRenderers)
            {
                Color color = bladeStormColor;
                color.a *= alphaMult;
                bladeRenderer.color = color;
            }

            tickTimer += Time.deltaTime;
            if (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;
                attackSystem.DealAreaDamage(transform.position, ability.radius, 1 << 8, ability.power, true);
                SpawnBladeStormPulse();
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(pivot.gameObject);
    }

    private void SpawnBladeStormPulse()
    {
        var pulseGo = new GameObject("BladeStormPulse");
        pulseGo.transform.SetParent(transform, false);
        pulseGo.transform.localPosition = Vector3.zero;

        SpriteRenderer pulseRenderer = pulseGo.AddComponent<SpriteRenderer>();
        pulseRenderer.sprite = CreateGlowSprite();
        pulseRenderer.color = bladeStormColor;

        if (spriteRenderer != null)
        {
            pulseRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            pulseRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        }

        StartCoroutine(BladeStormPulseRoutine(pulseGo, pulseRenderer));
    }

    private IEnumerator BladeStormPulseRoutine(GameObject pulseGo, SpriteRenderer pulseRenderer)
    {
        float parentScale = Mathf.Max(0.01f, transform.localScale.x);
        const float visualOrbitRadius = 1.5f;
        float targetScale = visualOrbitRadius / parentScale;

        const float pulseDuration = 0.25f;
        float elapsed = 0f;

        while (elapsed < pulseDuration)
        {
            float t = elapsed / pulseDuration;
            pulseGo.transform.localScale = Vector3.one * Mathf.Lerp(targetScale * 0.2f, targetScale, t);
            Color color = bladeStormColor;
            color.a = Mathf.Lerp(0.35f, 0f, t);
            pulseRenderer.color = color;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(pulseGo);
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
