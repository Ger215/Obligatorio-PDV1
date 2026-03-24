using UnityEngine;

public class PrototypeTestBootstrap : MonoBehaviour
{
    private const int PlayerLayer = 8;
    private const int EnemyLayer = 9;
    private const int ArenaLayer = 10;

    [Header("Auto Setup")]
    [SerializeField] private bool buildOnStart = true;
    [SerializeField] private bool createCameraIfMissing = true;
    [SerializeField] private bool logControlsOnStart = true;
    [SerializeField] private StageConfig stageConfig;
    [SerializeField] private RhythmConfig rhythmConfig;
    [SerializeField] private PlayerConfig playerConfig;
    [SerializeField] private EnemyConfig enemyConfig;
    [SerializeField] private WaveConfig waveConfig;

    private bool hasBuilt;
    private static Sprite cachedSquareSprite;

    private void Start()
    {
        if (!HasRequiredConfigs())
        {
            Debug.LogError("PrototypeTestBootstrap needs all ScriptableObject config references assigned.");
            return;
        }

        if (!buildOnStart || hasBuilt)
        {
            return;
        }

        BuildPrototype();
    }

    [ContextMenu("Build Prototype Test Scene")]
    public void BuildPrototype()
    {
        if (!HasRequiredConfigs())
        {
            Debug.LogError("PrototypeTestBootstrap cannot build without all config assets assigned.");
            return;
        }
        if (hasBuilt)
        {
            return;
        }

        hasBuilt = true;

        if (createCameraIfMissing)
        {
            EnsureCamera();
        }

        CreateStage();
        BeatManager beatManager = CreateBeatManager();
        CreateBeatIndicator(beatManager);
        GameObject enemyTemplate = CreateEnemyTemplate();
        float[] platformHeights = GetPlatformHeights();
        Transform[] spawnPoints = CreateSpawnPoints(platformHeights);
        CreatePlayer(beatManager, platformHeights[0]);
        CreateGameManager(enemyTemplate, spawnPoints);

        if (logControlsOnStart)
        {
            Debug.Log("Side-view prototype ready. Move: A/D | Jump: W or Up Arrow | Attack: Space or Left Mouse | Dash: Left Shift");
        }
    }

    private void EnsureCamera()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
        }

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = stageConfig.arenaSize.y * 0.45f;
        mainCamera.transform.position = new Vector3(0f, 1.5f, -10f);
        mainCamera.backgroundColor = new Color(0.11f, 0.12f, 0.16f);
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
    }

    private BeatManager CreateBeatManager()
    {
        GameObject beatManagerObject = new GameObject("BeatManager");
        beatManagerObject.transform.SetParent(transform);

        BeatManager beatManager = beatManagerObject.AddComponent<BeatManager>();
        beatManager.Configure(rhythmConfig.beatsPerMinute, 0f, rhythmConfig.debugBeatLogs);
        return beatManager;
    }

    private void CreateBeatIndicator(BeatManager beatManager)
    {
        GameObject indicatorObject = new GameObject("BeatIndicator");
        indicatorObject.transform.SetParent(transform);
        indicatorObject.transform.position = new Vector3(0f, stageConfig.arenaSize.y * 0.38f, 0f);

        SpriteRenderer spriteRenderer = indicatorObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetSquareSprite();
        spriteRenderer.sortingOrder = 20;

        BeatIndicator beatIndicator = indicatorObject.AddComponent<BeatIndicator>();
        beatIndicator.Configure(
            beatManager,
            rhythmConfig.perfectWindow,
            rhythmConfig.warningWindow,
            new Color(0.2f, 1f, 0.35f),
            new Color(1f, 0.85f, 0.2f),
            new Color(0.95f, 0.25f, 0.25f),
            new Vector3(0.6f, 0.6f, 1f),
            new Vector3(1.1f, 1.1f, 1f));
    }

    private PlayerController CreatePlayer(BeatManager beatManager, float groundTopY)
    {
        Vector2 playerSpawnPosition = new Vector2(-stageConfig.arenaSize.x * 0.28f, groundTopY + 0.5f);
        GameObject playerObject = CreateActor("Player", playerSpawnPosition, new Vector2(1f, 1f), new Color(0.25f, 0.85f, 0.4f), PlayerLayer, 3f);
        playerObject.tag = "Player";

        HealthSystem healthSystem = playerObject.AddComponent<HealthSystem>();
        healthSystem.Configure(playerConfig.healthConfig);

        RhythmChecker rhythmChecker = playerObject.AddComponent<RhythmChecker>();
        rhythmChecker.Configure(beatManager, rhythmConfig.perfectWindow, rhythmConfig.debugRhythmLogs);

        Transform attackPoint = CreateChildMarker(playerObject.transform, "PlayerAttackPoint", new Vector3(playerConfig.attackPointDistance, 0f, 0f));
        Transform groundCheck = CreateChildMarker(playerObject.transform, "PlayerGroundCheck", new Vector3(0f, -0.56f, 0f));

        AttackSystem attackSystem = playerObject.AddComponent<AttackSystem>();
        attackSystem.Configure(
            playerConfig.attackConfig,
            attackPoint,
            1 << EnemyLayer,
            rhythmChecker);

        PlayerController playerController = playerObject.AddComponent<PlayerController>();
        playerController.Configure(
            playerConfig.moveSpeed,
            playerConfig.jumpForce,
            playerConfig.dashSpeed,
            playerConfig.dashDuration,
            playerConfig.dashCooldown,
            playerConfig.attackPointDistance,
            groundCheck,
            1 << ArenaLayer,
            0.18f);

        return playerController;
    }

    private GameObject CreateEnemyTemplate()
    {
        GameObject enemyTemplate = CreateActor("EnemyTemplate", new Vector2(0f, 20f), new Vector2(0.9f, 0.9f), new Color(0.9f, 0.3f, 0.3f), EnemyLayer, 3f);
        enemyTemplate.transform.SetParent(transform);
        enemyTemplate.SetActive(false);

        HealthSystem healthSystem = enemyTemplate.AddComponent<HealthSystem>();
        healthSystem.Configure(enemyConfig.healthConfig);

        Transform attackPoint = CreateChildMarker(enemyTemplate.transform, "EnemyAttackPoint", new Vector3(enemyConfig.attackPointDistance, 0f, 0f));
        Transform groundCheck = CreateChildMarker(enemyTemplate.transform, "EnemyGroundCheck", new Vector3(0f, -0.51f, 0f));

        AttackSystem attackSystem = enemyTemplate.AddComponent<AttackSystem>();
        attackSystem.Configure(
            enemyConfig.attackConfig,
            attackPoint,
            1 << PlayerLayer,
            null);

        EnemyController enemyController = enemyTemplate.AddComponent<EnemyController>();
        enemyController.Configure(
            enemyConfig.moveSpeed,
            enemyConfig.jumpForce,
            enemyConfig.attackDistance,
            enemyConfig.verticalAttackTolerance,
            enemyConfig.jumpTriggerHeight,
            enemyConfig.repathDelay,
            enemyConfig.attackPointDistance,
            groundCheck,
            1 << ArenaLayer,
            0.18f);

        return enemyTemplate;
    }

    private void CreateGameManager(GameObject enemyTemplate, Transform[] spawnPoints)
    {
        GameObject gameManagerObject = new GameObject("GameManager");
        gameManagerObject.transform.SetParent(transform);

        GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
        gameManager.Configure(
            enemyTemplate,
            spawnPoints,
            waveConfig.startingEnemyCount,
            waveConfig.additionalEnemiesPerWave,
            waveConfig.timeBetweenWaves,
            stageConfig.arenaSize.x * 0.25f);
    }

    private Transform[] CreateSpawnPoints(float[] platformHeights)
    {
        GameObject spawnRoot = new GameObject("SpawnPoints");
        spawnRoot.transform.SetParent(transform);

        Vector2[] positions =
        {
            new Vector2(stageConfig.arenaSize.x * 0.28f, platformHeights[0] + 0.45f),
            new Vector2(-stageConfig.arenaSize.x * 0.08f, platformHeights[1] + 0.45f),
            new Vector2(stageConfig.arenaSize.x * 0.18f, platformHeights[1] + 0.45f),
            new Vector2(0f, platformHeights[2] + 0.45f)
        };

        Transform[] spawnPoints = new Transform[positions.Length];

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject spawnPoint = new GameObject($"SpawnPoint_{i + 1}");
            spawnPoint.transform.SetParent(spawnRoot.transform);
            spawnPoint.transform.position = positions[i];
            spawnPoints[i] = spawnPoint.transform;
        }

        return spawnPoints;
    }

    private void CreateStage()
    {
        GameObject stageRoot = new GameObject("Stage");
        stageRoot.transform.SetParent(transform);

        float[] platformHeights = GetPlatformHeights();
        float groundWidth = stageConfig.arenaSize.x;
        float lowPlatformWidth = stageConfig.arenaSize.x * 0.28f;
        float midPlatformWidth = stageConfig.arenaSize.x * 0.24f;
        float topPlatformWidth = stageConfig.arenaSize.x * 0.2f;

        CreateStagePiece(stageRoot.transform, "Background", new Vector2(0f, 1f), new Vector2(stageConfig.arenaSize.x * 1.1f, stageConfig.arenaSize.y * 0.95f), new Color(0.14f, 0.15f, 0.2f), false, -1);
        CreateStagePiece(stageRoot.transform, "Ground", new Vector2(0f, platformHeights[0] - (stageConfig.platformThickness * 0.5f)), new Vector2(groundWidth, stageConfig.platformThickness), new Color(0.27f, 0.29f, 0.34f), true, 0);
        CreateStagePiece(stageRoot.transform, "PlatformLeft", new Vector2(-stageConfig.arenaSize.x * 0.18f, platformHeights[1] - (stageConfig.platformThickness * 0.5f)), new Vector2(lowPlatformWidth, stageConfig.platformThickness), new Color(0.34f, 0.36f, 0.42f), true, 0);
        CreateStagePiece(stageRoot.transform, "PlatformRight", new Vector2(stageConfig.arenaSize.x * 0.2f, platformHeights[1] - (stageConfig.platformThickness * 0.5f)), new Vector2(midPlatformWidth, stageConfig.platformThickness), new Color(0.34f, 0.36f, 0.42f), true, 0);
        CreateStagePiece(stageRoot.transform, "PlatformTop", new Vector2(0f, platformHeights[2] - (stageConfig.platformThickness * 0.5f)), new Vector2(topPlatformWidth, stageConfig.platformThickness), new Color(0.4f, 0.42f, 0.48f), true, 0);
        CreateStagePiece(stageRoot.transform, "WallLeft", new Vector2(-stageConfig.arenaSize.x * 0.5f, 0f), new Vector2(stageConfig.wallThickness, stageConfig.arenaSize.y), new Color(0.22f, 0.24f, 0.29f), true, 0);
        CreateStagePiece(stageRoot.transform, "WallRight", new Vector2(stageConfig.arenaSize.x * 0.5f, 0f), new Vector2(stageConfig.wallThickness, stageConfig.arenaSize.y), new Color(0.22f, 0.24f, 0.29f), true, 0);
    }

    private float[] GetPlatformHeights()
    {
        return new[]
        {
            -stageConfig.arenaSize.y * 0.33f,
            0f,
            stageConfig.arenaSize.y * 0.24f
        };
    }

    private GameObject CreateStagePiece(
        Transform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        Color color,
        bool addCollider,
        int sortingOrder)
    {
        GameObject piece = new GameObject(objectName);
        piece.transform.SetParent(parent);
        piece.transform.position = position;
        piece.layer = ArenaLayer;

        SpriteRenderer spriteRenderer = piece.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetSquareSprite();
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;
        piece.transform.localScale = new Vector3(size.x, size.y, 1f);

        if (addCollider)
        {
            BoxCollider2D collider = piece.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
        }

        return piece;
    }

    private GameObject CreateActor(string objectName, Vector2 position, Vector2 size, Color color, int layer, float gravityScale)
    {
        GameObject actor = new GameObject(objectName);
        actor.transform.SetParent(transform);
        actor.transform.position = position;
        actor.layer = layer;

        SpriteRenderer spriteRenderer = actor.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetSquareSprite();
        spriteRenderer.color = color;
        actor.transform.localScale = new Vector3(size.x, size.y, 1f);

        Rigidbody2D rigidbody2D = actor.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = gravityScale;
        rigidbody2D.freezeRotation = true;
        rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D collider = actor.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        return actor;
    }

    private Transform CreateChildMarker(Transform parent, string objectName, Vector3 localPosition)
    {
        GameObject marker = new GameObject(objectName);
        marker.transform.SetParent(parent);
        marker.transform.localPosition = localPosition;
        return marker.transform;
    }

    private static Sprite GetSquareSprite()
    {
        if (cachedSquareSprite != null)
        {
            return cachedSquareSprite;
        }

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.filterMode = FilterMode.Point;

        cachedSquareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        cachedSquareSprite.name = "GeneratedSquareSprite";
        return cachedSquareSprite;
    }

    private bool HasRequiredConfigs()
    {
        return stageConfig != null &&
               rhythmConfig != null &&
               playerConfig != null &&
               playerConfig.healthConfig != null &&
               playerConfig.attackConfig != null &&
               enemyConfig != null &&
               enemyConfig.healthConfig != null &&
               enemyConfig.attackConfig != null &&
               waveConfig != null;
    }
}
