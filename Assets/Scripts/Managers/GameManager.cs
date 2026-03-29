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
    [SerializeField] private EnemyConfig enemyConfig;
    [SerializeField] private PlayerAbilityCatalog abilityCatalog;
    [SerializeField] private GameHudController hudController;
    [SerializeField] private AudioManager audioManager;

    [Header("Fallback Spawn Area")]
    [SerializeField] private float spawnRadius = 6f;

    private readonly List<HealthSystem> aliveEnemies = new List<HealthSystem>();
    private readonly List<PlayerAbilityDefinition> offeredAbilities = new List<PlayerAbilityDefinition>();

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

        if (enemyPrefab == null || waveConfig == null || enemyConfig == null)
        {
            Debug.LogError("GameManager needs enemyPrefab, waveConfig, and enemyConfig references.");
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
        Vector3 spawnPosition = GetSpawnPosition(enemyIndex);
        GameObject enemyInstance = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        enemyInstance.name = $"Enemy_{CurrentWave}_{enemyIndex + 1}";
        enemyInstance.SetActive(true);
        ApplyWaveScaling(enemyInstance);

        HealthSystem enemyHealth = enemyInstance.GetComponent<HealthSystem>();

        if (enemyHealth == null)
        {
            Debug.LogError("Spawned enemy is missing a HealthSystem component.");
            return;
        }

        enemyHealth.Died += HandleEnemyDeath;
        aliveEnemies.Add(enemyHealth);
        hudController?.RefreshWaveState(CurrentWave, aliveEnemies.Count);
    }

    private Vector3 GetSpawnPosition(int enemyIndex)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            return spawnPoint.position;
        }

        Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
        return transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);
    }

    private void HandleEnemyDeath(HealthSystem deadEnemy)
    {
        if (deadEnemy == null)
        {
            return;
        }

        deadEnemy.Died -= HandleEnemyDeath;
        aliveEnemies.Remove(deadEnemy);
        RewardPlayerForEnemyDeath();
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
        EnemyConfig newEnemyConfig,
        PlayerAbilityCatalog newAbilityCatalog,
        float newSpawnRadius)
    {
        enemyPrefab = newEnemyPrefab;
        spawnPoints = newSpawnPoints;
        waveConfig = newWaveConfig;
        enemyConfig = newEnemyConfig;
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

    private void RewardPlayerForEnemyDeath()
    {
        if (playerExperience == null)
        {
            return;
        }

        int reward = enemyConfig.baseExperienceReward + ((CurrentWave - 1) * waveConfig.bonusExperiencePerWave);
        playerExperience.AddExperience(reward);
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

    private void ApplyWaveScaling(GameObject enemyInstance)
    {
        int waveIndex = Mathf.Max(0, CurrentWave - 1);
        float healthMultiplier = 1f + (waveConfig.enemyHealthMultiplierPerWave * waveIndex);
        float damageMultiplier = 1f + (waveConfig.enemyDamageMultiplierPerWave * waveIndex);
        float moveSpeedMultiplier = 1f + (waveConfig.enemyMoveSpeedMultiplierPerWave * waveIndex);

        GetTypeMultipliers(enemyConfig.enemyType, out float typeHealth, out float typeDamage);
        healthMultiplier *= typeHealth;
        damageMultiplier *= typeDamage;

        HealthSystem healthSystem = enemyInstance.GetComponent<HealthSystem>();
        AttackSystem attackSystem = enemyInstance.GetComponent<AttackSystem>();
        EnemyController enemyController = enemyInstance.GetComponent<EnemyController>();

        if (healthSystem != null && enemyConfig.healthConfig != null)
        {
            healthSystem.Configure(
                Mathf.RoundToInt(enemyConfig.healthConfig.maxHealth * healthMultiplier),
                enemyConfig.healthConfig.destroyOnDeath,
                enemyConfig.healthConfig.deactivateOnDeath,
                enemyConfig.healthConfig.destroyDelay);
        }

        if (attackSystem != null && enemyConfig.attackConfig != null)
        {
            Transform attackPoint = attackSystem.AttackPointTransform != null ? attackSystem.AttackPointTransform : enemyInstance.transform;
            attackSystem.Configure(
                attackPoint,
                1 << 8,
                enemyConfig.attackConfig.attackRange,
                enemyConfig.attackConfig.attackCooldown,
                Mathf.RoundToInt(enemyConfig.attackConfig.baseDamage * damageMultiplier),
                enemyConfig.attackConfig.criticalChance,
                enemyConfig.attackConfig.criticalMultiplier,
                enemyConfig.attackConfig.debugAttackLogs);
        }

        enemyController?.ApplyDifficultyMultiplier(moveSpeedMultiplier);
        enemyController?.SetEnemyType(enemyConfig.enemyType);
    }

    private void OnValidate()
    {
        spawnRadius = Mathf.Max(1f, spawnRadius);
    }
}
