using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : SingletonBehaviour<GameManager>
{
    [Header("Wave Setup")]
    [SerializeField] private bool enableEnemySpawning = true;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private WaveConfig waveConfig;
    [SerializeField] private EnemyTypeConfig[] enemyTypeConfigs;
    [SerializeField] private PlayerAbilityCatalog abilityCatalog;
    [SerializeField] private GameHudController hudController;
    [SerializeField] private AudioManager audioManager;

    [Header("Death FX")]
    [SerializeField] private GameObject enemyDeathFXPrefab;

    [Header("Fallback Spawn Area")]
    [SerializeField] private float spawnRadius = 6f;

    private readonly List<HealthSystem> aliveEnemies = new List<HealthSystem>();
    private readonly List<PlayerAbilityDefinition> offeredAbilities = new List<PlayerAbilityDefinition>();
    private readonly Dictionary<HealthSystem, int> enemyXpRewards = new Dictionary<HealthSystem, int>();

    private bool waitingForNextWave;
    private bool shopOpen;
    private ExperienceSystem playerExperience;
    private PlayerAbilityController playerAbilities;

    public int CurrentWave { get; private set; }
    public int AliveEnemyCount => aliveEnemies.Count;
    public IReadOnlyList<PlayerAbilityDefinition> OfferedAbilities => offeredAbilities;
    public bool ShopOpen => shopOpen;

    private void Start()
    {
        if (PlayerController.Instance != null)
        {
            playerExperience = PlayerController.Instance.GetComponent<ExperienceSystem>();
            playerAbilities = PlayerController.Instance.GetComponent<PlayerAbilityController>();
        }

        hudController?.Bind(this, PlayerController.Instance);
        audioManager?.PlayBackgroundMusic();
        StartNextWave();
    }

    private void StartNextWave()
    {
        if (!enableEnemySpawning)
        {
            aliveEnemies.Clear();
            return;
        }

        if (enemyPrefab == null || waveConfig == null || enemyTypeConfigs == null || enemyTypeConfigs.Length == 0)
        {
            Debug.LogError("GameManager needs enemyPrefab, waveConfig, and at least one enemyTypeConfig.");
            return;
        }

        waitingForNextWave = false;
        shopOpen = false;
        Time.timeScale = 1f;
        CurrentWave++;

        int enemiesToSpawn = waveConfig.startingEnemyCount + ((CurrentWave - 1) * waveConfig.additionalEnemiesPerWave);

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            SpawnEnemy(i);
        }

        hudController?.RefreshWaveState(CurrentWave, aliveEnemies.Count);
    }

    private void SpawnEnemy(int enemyIndex)
    {
        EnemyType type = PickEnemyTypeForWave();
        EnemyConfig config = GetConfigForType(type);

        if (config == null)
        {
            Debug.LogError($"No EnemyConfig found for type {type}.");
            return;
        }

        GameObject prefabToUse = GetPrefabForType(type) ?? enemyPrefab;
        Vector3 spawnPosition = GetSpawnPosition(enemyIndex);
        GameObject enemyInstance = Instantiate(prefabToUse, spawnPosition, Quaternion.identity);
        enemyInstance.name = $"Enemy_{CurrentWave}_{enemyIndex + 1}_{type}";
        ApplyWaveScaling(enemyInstance, config, type);

        HealthSystem enemyHealth = enemyInstance.GetComponent<HealthSystem>();

        if (enemyHealth == null)
        {
            Debug.LogError("Spawned enemy is missing a HealthSystem component.");
            return;
        }

        int xpReward = config.baseExperienceReward + ((CurrentWave - 1) * waveConfig.bonusExperiencePerWave);
        enemyXpRewards[enemyHealth] = xpReward;
        enemyHealth.Died += HandleEnemyDeath;
        aliveEnemies.Add(enemyHealth);
        hudController?.RefreshWaveState(CurrentWave, aliveEnemies.Count);
    }

    private EnemyType PickEnemyTypeForWave()
    {
        if (waveConfig.waveDefinitions != null && CurrentWave - 1 < waveConfig.waveDefinitions.Length)
        {
            EnemyType[] allowed = waveConfig.waveDefinitions[CurrentWave - 1].allowedTypes;
            if (allowed != null && allowed.Length > 0)
            {
                return allowed[Random.Range(0, allowed.Length)];
            }
        }

        return EnemyType.Chaser;
    }

    private EnemyConfig GetConfigForType(EnemyType type)
    {
        foreach (EnemyTypeConfig entry in enemyTypeConfigs)
        {
            if (entry.type == type)
            {
                return entry.config;
            }
        }

        return enemyTypeConfigs[0].config;
    }

    private GameObject GetPrefabForType(EnemyType type)
    {
        foreach (EnemyTypeConfig entry in enemyTypeConfigs)
        {
            if (entry.type == type && entry.prefab != null)
            {
                return entry.prefab;
            }
        }

        return null;
    }

    private Vector3 GetSpawnPosition(int enemyIndex)
    {
        Vector3 basePosition;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            basePosition = spawnPoint.position;
        }
        else
        {
            basePosition = transform.position;
        }

        float offsetX = (enemyIndex % 2 == 0 ? 1f : -1f) * (1f + enemyIndex * 0.8f);
        return basePosition + new Vector3(offsetX, 0f, 0f);
    }

    private void HandleEnemyDeath(HealthSystem deadEnemy)
    {
        if (deadEnemy == null)
        {
            return;
        }

        deadEnemy.Died -= HandleEnemyDeath;
        aliveEnemies.Remove(deadEnemy);

        if (enemyDeathFXPrefab != null)
        {
            GameObject fx = Instantiate(enemyDeathFXPrefab, deadEnemy.transform.position, Quaternion.identity);
            Destroy(fx, 1f);
        }

        RewardPlayerForEnemyDeath(deadEnemy);
        hudController?.RefreshWaveState(CurrentWave, aliveEnemies.Count);

        if (aliveEnemies.Count == 0 && !waitingForNextWave)
        {
            StartCoroutine(StartNextWaveAfterDelay());
        }
    }

    private IEnumerator StartNextWaveAfterDelay()
    {
        waitingForNextWave = true;

        if (TryOpenAbilityShop())
        {
            yield break;
        }

        yield return new WaitForSeconds(waveConfig.timeBetweenWaves);
        StartNextWave();
    }

    public void Configure(
        GameObject newEnemyPrefab,
        Transform[] newSpawnPoints,
        WaveConfig newWaveConfig,
        EnemyTypeConfig[] newEnemyTypeConfigs,
        PlayerAbilityCatalog newAbilityCatalog,
        float newSpawnRadius)
    {
        enemyPrefab = newEnemyPrefab;
        spawnPoints = newSpawnPoints;
        waveConfig = newWaveConfig;
        enemyTypeConfigs = newEnemyTypeConfigs;
        abilityCatalog = newAbilityCatalog;
        spawnRadius = Mathf.Max(1f, newSpawnRadius);
    }

    public void SelectOfferedAbility(int index)
    {
        if (!shopOpen || playerAbilities == null || playerExperience == null || index < 0 || index >= offeredAbilities.Count)
        {
            return;
        }

        PlayerAbilityDefinition selectedAbility = offeredAbilities[index];

        if (!playerExperience.TrySpend(selectedAbility.cost))
        {
            return;
        }

        playerAbilities.LearnAbility(selectedAbility);
        CloseShopAndContinue();
    }

    public void SkipAbilityShop()
    {
        if (!shopOpen)
        {
            return;
        }

        CloseShopAndContinue();
    }

    private void CloseShopAndContinue()
    {
        shopOpen = false;
        offeredAbilities.Clear();
        hudController?.HideAbilityShop();
        StartCoroutine(BeginNextWaveAfterDelay());
    }

    private IEnumerator BeginNextWaveAfterDelay()
    {
        Time.timeScale = 1f;
        yield return new WaitForSeconds(waveConfig != null ? waveConfig.timeBetweenWaves : 1f);
        StartNextWave();
    }

    private bool TryOpenAbilityShop()
    {
        if (abilityCatalog == null || playerAbilities == null || playerExperience == null || playerAbilities.IsAbilityCapacityReached)
        {
            return false;
        }

        List<PlayerAbilityDefinition> availableAbilities = abilityCatalog.GetUnlearnedAbilities(playerAbilities.LearnedAbilities);

        if (availableAbilities.Count == 0)
        {
            return false;
        }

        offeredAbilities.Clear();

        while (offeredAbilities.Count < waveConfig.offeredAbilitiesCount && availableAbilities.Count > 0)
        {
            int randomIndex = Random.Range(0, availableAbilities.Count);
            offeredAbilities.Add(availableAbilities[randomIndex]);
            availableAbilities.RemoveAt(randomIndex);
        }

        shopOpen = true;
        Time.timeScale = 0f;
        hudController?.ShowAbilityShop(offeredAbilities, playerExperience.CurrentExperience);
        return true;
    }

    private void RewardPlayerForEnemyDeath(HealthSystem deadEnemy)
    {
        if (playerExperience == null)
        {
            return;
        }

        if (enemyXpRewards.TryGetValue(deadEnemy, out int reward))
        {
            enemyXpRewards.Remove(deadEnemy);
            playerExperience.AddExperience(reward);
        }
    }

    private static void GetTypeMultipliers(EnemyType type, out float healthMult, out float damageMult)
    {
        switch (type)
        {
            case EnemyType.Fast:
                healthMult = 0.6f;
                damageMult = 0.8f;
                break;
            case EnemyType.Tank:
                healthMult = 2.5f;
                damageMult = 1.5f;
                break;
            default:
                healthMult = 1f;
                damageMult = 1f;
                break;
        }
    }

    private void ApplyWaveScaling(GameObject enemyInstance, EnemyConfig config, EnemyType type)
    {
        int waveIndex = Mathf.Max(0, CurrentWave - 1);
        float healthMultiplier = 1f + (waveConfig.enemyHealthMultiplierPerWave * waveIndex);
        float damageMultiplier = 1f + (waveConfig.enemyDamageMultiplierPerWave * waveIndex);
        float moveSpeedMultiplier = 1f + (waveConfig.enemyMoveSpeedMultiplierPerWave * waveIndex);

        GetTypeMultipliers(type, out float typeHealth, out float typeDamage);
        healthMultiplier *= typeHealth;
        damageMultiplier *= typeDamage;

        HealthSystem healthSystem = enemyInstance.GetComponent<HealthSystem>();
        AttackSystem attackSystem = enemyInstance.GetComponent<AttackSystem>();
        EnemyController enemyController = enemyInstance.GetComponent<EnemyController>();

        if (healthSystem != null && config.healthConfig != null)
        {
            healthSystem.Configure(
                Mathf.RoundToInt(config.healthConfig.maxHealth * healthMultiplier),
                config.healthConfig.destroyOnDeath,
                config.healthConfig.deactivateOnDeath,
                config.healthConfig.destroyDelay);
        }

        if (attackSystem != null && config.attackConfig != null)
        {
            Transform attackPoint = attackSystem.AttackPointTransform != null ? attackSystem.AttackPointTransform : enemyInstance.transform;
            attackSystem.Configure(
                attackPoint,
                1 << 8,
                config.attackConfig.attackRange,
                config.attackConfig.attackCooldown,
                Mathf.RoundToInt(config.attackConfig.baseDamage * damageMultiplier),
                config.attackConfig.criticalChance,
                config.attackConfig.criticalMultiplier,
                config.attackConfig.debugAttackLogs);
        }

        enemyController?.ApplyDifficultyMultiplier(moveSpeedMultiplier);
        enemyController?.SetEnemyType(type);
    }

    private void OnValidate()
    {
        spawnRadius = Mathf.Max(1f, spawnRadius);
    }
}
