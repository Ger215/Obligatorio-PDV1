using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : SingletonBehaviour<GameManager>
{
    [Header("Wave Setup")]
    [SerializeField] private bool enableEnemySpawning = true;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private WaveConfig waveConfig;
    [SerializeField] private EnemyTypeConfig[] enemyTypeConfigs;
    [SerializeField] private PlayerAbilityCatalog abilityCatalog;
    [SerializeField] private GameHudController hudController;
    [SerializeField] private AudioManager audioManager;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Death FX")]
    [SerializeField] private GameObject enemyDeathFXPrefab;
    [SerializeField] private GameObject floatingTextPrefab;
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private Color xpFloatingTextColor = new Color(0.3f, 1f, 0.4f);

    [Header("Fallback Spawn Area")]
    [SerializeField] private float spawnRadius = 6f;

    private readonly List<HealthSystem> aliveEnemies = new List<HealthSystem>();
    private readonly List<PlayerAbilityDefinition> offeredAbilities = new List<PlayerAbilityDefinition>();
    private readonly Dictionary<HealthSystem, int> enemyXpRewards = new Dictionary<HealthSystem, int>();

    private bool waitingForNextWave;
    private bool shopOpen;
    private bool isPaused;
    private bool isGameOver;
    private HealthSystem playerHealth;
    private ExperienceSystem playerExperience;
    private PlayerAbilityController playerAbilities;

    public int CurrentWave { get; private set; }
    public int AliveEnemyCount => aliveEnemies.Count;
    public IReadOnlyList<PlayerAbilityDefinition> OfferedAbilities => offeredAbilities;
    public bool ShopOpen => shopOpen;
    public bool IsPaused => isPaused;
    public bool IsGameOver => isGameOver;

    private void Start()
    {
        if (PlayerController.Instance != null)
        {
            playerExperience = PlayerController.Instance.GetComponent<ExperienceSystem>();
            playerAbilities = PlayerController.Instance.GetComponent<PlayerAbilityController>();
            playerHealth = PlayerController.Instance.GetComponent<HealthSystem>();

            if (playerHealth != null)
            {
                playerHealth.Died += HandlePlayerDeath;
            }
        }

        hudController?.Bind(this, PlayerController.Instance);
        audioManager?.PlayBackgroundMusic();
        StartNextWave();
    }

    private void Update()
    {
        if (isGameOver)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame
            && !isGameOver && !isPaused && !shopOpen && !waitingForNextWave)
        {
            CheatSkipWave();
        }
#endif

        if (!waitingForNextWave && !isPaused)
        {
            ValidateAliveEnemies();
        }
    }

    private void ValidateAliveEnemies()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            HealthSystem e = aliveEnemies[i];
            if (e == null || e.IsDead || !e.gameObject.activeInHierarchy)
            {
                aliveEnemies.RemoveAt(i);
            }
        }

        hudController?.RefreshWaveState(CurrentWave, aliveEnemies.Count);
        CheckWaveCleared();
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.Died -= HandlePlayerDeath;
        }
    }

    private void StartNextWave()
    {
        if (!enableEnemySpawning)
        {
            aliveEnemies.Clear();
            return;
        }

        if (waveConfig == null || enemyTypeConfigs == null || enemyTypeConfigs.Length == 0)
        {
            Debug.LogError("GameManager needs waveConfig and at least one enemyTypeConfig.");
            return;
        }

        waitingForNextWave = false;
        shopOpen = false;
        Time.timeScale = 1f;
        CurrentWave++;
        aliveEnemies.Clear();
        enemyXpRewards.Clear();

        int enemiesToSpawn = Mathf.Min(
            waveConfig.startingEnemyCount + ((CurrentWave - 1) * waveConfig.additionalEnemiesPerWave),
            waveConfig.maxEnemiesPerWave);

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            SpawnEnemy(i);
        }

        hudController?.RefreshWaveState(CurrentWave, aliveEnemies.Count);
        hudController?.ShowWaveAnnouncement(CurrentWave);
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

        GameObject prefabToUse = GetPrefabForType(type);

        if (prefabToUse == null)
        {
            Debug.LogError($"No prefab configured for EnemyType {type} in EnemyTypeConfigs.");
            return;
        }

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

        EnemyType[] fallback = waveConfig.fallbackTypes;
        if (fallback != null && fallback.Length > 0)
        {
            return fallback[Random.Range(0, fallback.Length)];
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
        float spawnX = basePosition.x + offsetX;

        if (Camera.main != null)
        {
            float halfWidth = Camera.main.orthographicSize * Camera.main.aspect;
            float camX = Camera.main.transform.position.x;
            spawnX = Mathf.Clamp(spawnX, camX - halfWidth + 1f, camX + halfWidth - 1f);
        }

        return new Vector3(spawnX, basePosition.y, basePosition.z);
    }

    private void HandleEnemyDeath(HealthSystem deadEnemy)
    {
        if (deadEnemy != null)
        {
            deadEnemy.Died -= HandleEnemyDeath;
        }

        aliveEnemies.Remove(deadEnemy);

        if (deadEnemy != null && enemyDeathFXPrefab != null)
        {
            Vector3 spawnPos = deadEnemy.transform.position + Vector3.up * 0.5f;
            GameObject fx = Instantiate(enemyDeathFXPrefab, spawnPos, Quaternion.identity);
            float scale = Random.Range(2.5f, 3.5f);
            fx.transform.localScale = new Vector3(scale, scale, 1f);
            Animator fxAnimator = fx.GetComponent<Animator>();
            if (fxAnimator != null)
                fxAnimator.speed = 1.5f;
            Destroy(fx, 0.35f);
        }

        if (deadEnemy != null)
        {
            RewardPlayerForEnemyDeath(deadEnemy, deadEnemy.transform.position);
        }

        hudController?.RefreshWaveState(CurrentWave, aliveEnemies.Count);
        CheckWaveCleared();
    }

    private void CheckWaveCleared()
    {
        if (waitingForNextWave || isGameOver)
        {
            return;
        }

        if (aliveEnemies.Count == 0)
        {
            waitingForNextWave = true;
            StartCoroutine(StartNextWaveAfterDelay());
        }
    }


    private IEnumerator StartNextWaveAfterDelay()
    {
        waitingForNextWave = true;

        HealPlayerEndOfWave();

        if (TryOpenAbilityShop())
        {
            yield break;
        }

        yield return new WaitForSeconds(waveConfig.timeBetweenWaves);
        StartNextWave();
    }

    private void HealPlayerEndOfWave()
    {
        if (waveConfig == null || waveConfig.hpRewardPerWave <= 0 || playerHealth == null)
        {
            return;
        }

        playerHealth.Heal(waveConfig.hpRewardPerWave);

        if (floatingTextPrefab != null && hudCanvas != null && PlayerController.Instance != null)
        {
            Vector3 worldPos = PlayerController.Instance.transform.position + Vector3.up * 1.5f;
            GameObject instance = Instantiate(floatingTextPrefab, hudCanvas.transform);
            Vector2 screenPoint = Camera.main.WorldToScreenPoint(worldPos);
            Camera uiCamera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)hudCanvas.transform, screenPoint, uiCamera, out Vector2 canvasPos);
            ((RectTransform)instance.transform).anchoredPosition = canvasPos;
            FloatingText floatingText = instance.GetComponent<FloatingText>();
            floatingText?.Play($"+{waveConfig.hpRewardPerWave} HP", new Color(0.4f, 0.9f, 1f));
        }
    }

    public void Configure(
        Transform[] newSpawnPoints,
        WaveConfig newWaveConfig,
        EnemyTypeConfig[] newEnemyTypeConfigs,
        PlayerAbilityCatalog newAbilityCatalog,
        float newSpawnRadius)
    {
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

    private void RewardPlayerForEnemyDeath(HealthSystem deadEnemy, Vector3 worldPosition)
    {
        if (playerExperience == null)
        {
            return;
        }

        if (!enemyXpRewards.TryGetValue(deadEnemy, out int reward))
        {
            return;
        }

        enemyXpRewards.Remove(deadEnemy);
        playerExperience.AddExperience(reward);

        if (floatingTextPrefab != null && hudCanvas != null)
        {
            GameObject instance = Instantiate(floatingTextPrefab, hudCanvas.transform);
            Vector2 screenPoint = Camera.main.WorldToScreenPoint(worldPosition);
            Camera uiCamera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)hudCanvas.transform, screenPoint, uiCamera, out Vector2 canvasPos);
            ((RectTransform)instance.transform).anchoredPosition = canvasPos;
            FloatingText floatingText = instance.GetComponent<FloatingText>();
            floatingText?.Play($"+{reward} XP", xpFloatingTextColor);
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
                healthMult = 2.0f;
                damageMult = 1.0f;
                break;
            default:
                healthMult = 1f;
                damageMult = 1f;
                break;
        }
    }

    private void ApplyWaveScaling(GameObject enemyInstance, EnemyConfig config, EnemyType type)
    {
        int scalingIndex = Mathf.Max(0, CurrentWave - waveConfig.scalingStartWave);
        float healthMultiplier = 1f + (waveConfig.enemyHealthMultiplierPerWave * scalingIndex);
        float damageMultiplier = 1f + (waveConfig.enemyDamageMultiplierPerWave * scalingIndex);
        float moveSpeedMultiplier = 1f + (waveConfig.enemyMoveSpeedMultiplierPerWave * scalingIndex);

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
                1 << 7,
                config.attackConfig.attackRange,
                config.attackConfig.attackCooldown,
                Mathf.RoundToInt(config.attackConfig.baseDamage * damageMultiplier),
                config.attackConfig.criticalChance,
                config.attackConfig.criticalMultiplier,
                config.attackConfig.debugAttackLogs);
        }

        if (enemyController != null && config != null)
        {
            enemyController.ApplyMovementConfig(
                config.moveSpeed,
                config.jumpForce,
                config.attackDistance,
                config.verticalAttackTolerance,
                config.jumpTriggerHeight,
                config.repathDelay,
                config.attackPointDistance);
        }

        enemyController?.ApplyDifficultyMultiplier(moveSpeedMultiplier);
        enemyController?.SetEnemyType(type);
    }

    private void HandlePlayerDeath(HealthSystem _)
    {
        TriggerGameOver();
    }

    private void TriggerGameOver()
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;
        isPaused = false;
        Time.timeScale = 0f;
        hudController?.ShowGameOver(CurrentWave);
    }

    public void TogglePause()
    {
        if (shopOpen)
        {
            return;
        }

        isPaused = !isPaused;

        if (isPaused)
        {
            Time.timeScale = 0f;
            hudController?.ShowPause();
        }
        else
        {
            Time.timeScale = 1f;
            hudController?.HidePause();
        }
    }

    public void ResumeGame()
    {
        if (!isPaused)
        {
            return;
        }

        isPaused = false;
        Time.timeScale = 1f;
        hudController?.HidePause();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            Application.Quit();
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void CheatSkipWave()
    {
        HealthSystem[] enemies = aliveEnemies.ToArray();
        foreach (HealthSystem enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(999999);
            }
        }
    }
#endif

    private void OnValidate()
    {
        spawnRadius = Mathf.Max(1f, spawnRadius);
    }
}
