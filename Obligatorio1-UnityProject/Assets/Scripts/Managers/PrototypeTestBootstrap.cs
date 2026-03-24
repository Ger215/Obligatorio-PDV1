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

    [Header("Stage")]
    [SerializeField] private Vector2 arenaSize = new Vector2(24f, 14f);
    [SerializeField] private float platformThickness = 0.8f;
    [SerializeField] private float wallThickness = 1f;

    [Header("Rhythm")]
    [SerializeField] private float beatsPerMinute = 120f;
    [SerializeField] private float perfectWindow = 0.2f;
    [SerializeField] private float warningWindow = 0.35f;
    [SerializeField] private bool debugBeatLogs = true;
    [SerializeField] private bool debugRhythmLogs = true;

    [Header("Player")]
    [SerializeField] private int playerMaxHealth = 10;
    [SerializeField] private float playerMoveSpeed = 6f;
    [SerializeField] private float playerJumpForce = 13f;
    [SerializeField] private float playerDashSpeed = 14f;
    [SerializeField] private float playerDashDuration = 0.15f;
    [SerializeField] private float playerDashCooldown = 0.75f;
    [SerializeField] private float playerAttackRange = 1.2f;
    [SerializeField] private float playerAttackCooldown = 0.35f;
    [SerializeField] private int playerBaseDamage = 2;
    [SerializeField] private float playerPerfectDamageMultiplier = 2f;
    [SerializeField] private float playerWeakDamageMultiplier = 0.5f;
    [SerializeField] private float playerAttackPointDistance = 0.75f;

    [Header("Enemy")]
    [SerializeField] private int enemyMaxHealth = 3;
    [SerializeField] private float enemyMoveSpeed = 3f;
    [SerializeField] private float enemyJumpForce = 11f;
    [SerializeField] private float enemyAttackDistance = 1.1f;
    [SerializeField] private float enemyVerticalAttackTolerance = 1f;
    [SerializeField] private float enemyJumpTriggerHeight = 1.35f;
    [SerializeField] private float enemyAttackRange = 0.9f;
    [SerializeField] private float enemyAttackCooldown = 0.8f;
    [SerializeField] private int enemyBaseDamage = 1;
    [SerializeField] private float enemyAttackPointDistance = 0.6f;
    [SerializeField] private float enemyRepathDelay = 0.5f;

    [Header("Waves")]
    [SerializeField] private int startingEnemyCount = 2;
    [SerializeField] private int additionalEnemiesPerWave = 1;
    [SerializeField] private float timeBetweenWaves = 2f;

    private bool hasBuilt;
    private static Sprite cachedSquareSprite;

    private void Start()
    {
        if (!buildOnStart || hasBuilt)
        {
            return;
        }

        BuildPrototype();
    }

    [ContextMenu("Build Prototype Test Scene")]
    public void BuildPrototype()
    {
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
        mainCamera.orthographicSize = arenaSize.y * 0.45f;
        mainCamera.transform.position = new Vector3(0f, 1.5f, -10f);
        mainCamera.backgroundColor = new Color(0.11f, 0.12f, 0.16f);
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
    }

    private BeatManager CreateBeatManager()
    {
        GameObject beatManagerObject = new GameObject("BeatManager");
        beatManagerObject.transform.SetParent(transform);

        BeatManager beatManager = beatManagerObject.AddComponent<BeatManager>();
        beatManager.Configure(beatsPerMinute, 0f, debugBeatLogs);
        return beatManager;
    }

    private void CreateBeatIndicator(BeatManager beatManager)
    {
        GameObject indicatorObject = new GameObject("BeatIndicator");
        indicatorObject.transform.SetParent(transform);
        indicatorObject.transform.position = new Vector3(0f, arenaSize.y * 0.38f, 0f);

        SpriteRenderer spriteRenderer = indicatorObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetSquareSprite();
        spriteRenderer.sortingOrder = 20;

        BeatIndicator beatIndicator = indicatorObject.AddComponent<BeatIndicator>();
        beatIndicator.Configure(
            beatManager,
            perfectWindow,
            warningWindow,
            new Color(0.2f, 1f, 0.35f),
            new Color(1f, 0.85f, 0.2f),
            new Color(0.95f, 0.25f, 0.25f),
            new Vector3(0.6f, 0.6f, 1f),
            new Vector3(1.1f, 1.1f, 1f));
    }

    private PlayerController CreatePlayer(BeatManager beatManager, float groundTopY)
    {
        Vector2 playerSpawnPosition = new Vector2(-arenaSize.x * 0.28f, groundTopY + 0.5f);
        GameObject playerObject = CreateActor("Player", playerSpawnPosition, new Vector2(1f, 1f), new Color(0.25f, 0.85f, 0.4f), PlayerLayer, 3f);
        playerObject.tag = "Player";

        HealthSystem healthSystem = playerObject.AddComponent<HealthSystem>();
        healthSystem.Configure(playerMaxHealth, false, false, 0f);

        RhythmChecker rhythmChecker = playerObject.AddComponent<RhythmChecker>();
        rhythmChecker.Configure(beatManager, perfectWindow, debugRhythmLogs);

        Transform attackPoint = CreateChildMarker(playerObject.transform, "PlayerAttackPoint", new Vector3(playerAttackPointDistance, 0f, 0f));
        Transform groundCheck = CreateChildMarker(playerObject.transform, "PlayerGroundCheck", new Vector3(0f, -0.56f, 0f));

        AttackSystem attackSystem = playerObject.AddComponent<AttackSystem>();
        attackSystem.Configure(
            attackPoint,
            1 << EnemyLayer,
            playerAttackRange,
            playerAttackCooldown,
            playerBaseDamage,
            playerPerfectDamageMultiplier,
            playerWeakDamageMultiplier,
            true,
            rhythmChecker,
            true);

        PlayerController playerController = playerObject.AddComponent<PlayerController>();
        playerController.Configure(
            playerMoveSpeed,
            playerJumpForce,
            playerDashSpeed,
            playerDashDuration,
            playerDashCooldown,
            playerAttackPointDistance,
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
        healthSystem.Configure(enemyMaxHealth, true, false, 0f);

        Transform attackPoint = CreateChildMarker(enemyTemplate.transform, "EnemyAttackPoint", new Vector3(enemyAttackPointDistance, 0f, 0f));
        Transform groundCheck = CreateChildMarker(enemyTemplate.transform, "EnemyGroundCheck", new Vector3(0f, -0.51f, 0f));

        AttackSystem attackSystem = enemyTemplate.AddComponent<AttackSystem>();
        attackSystem.Configure(
            attackPoint,
            1 << PlayerLayer,
            enemyAttackRange,
            enemyAttackCooldown,
            enemyBaseDamage,
            1f,
            1f,
            false,
            null,
            false);

        EnemyController enemyController = enemyTemplate.AddComponent<EnemyController>();
        enemyController.Configure(
            enemyMoveSpeed,
            enemyJumpForce,
            enemyAttackDistance,
            enemyVerticalAttackTolerance,
            enemyJumpTriggerHeight,
            enemyRepathDelay,
            enemyAttackPointDistance,
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
            startingEnemyCount,
            additionalEnemiesPerWave,
            timeBetweenWaves,
            arenaSize.x * 0.25f);
    }

    private Transform[] CreateSpawnPoints(float[] platformHeights)
    {
        GameObject spawnRoot = new GameObject("SpawnPoints");
        spawnRoot.transform.SetParent(transform);

        Vector2[] positions =
        {
            new Vector2(arenaSize.x * 0.28f, platformHeights[0] + 0.45f),
            new Vector2(-arenaSize.x * 0.08f, platformHeights[1] + 0.45f),
            new Vector2(arenaSize.x * 0.18f, platformHeights[1] + 0.45f),
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
        float groundWidth = arenaSize.x;
        float lowPlatformWidth = arenaSize.x * 0.28f;
        float midPlatformWidth = arenaSize.x * 0.24f;
        float topPlatformWidth = arenaSize.x * 0.2f;

        CreateStagePiece(stageRoot.transform, "Background", new Vector2(0f, 1f), new Vector2(arenaSize.x * 1.1f, arenaSize.y * 0.95f), new Color(0.14f, 0.15f, 0.2f), false, -1);
        CreateStagePiece(stageRoot.transform, "Ground", new Vector2(0f, platformHeights[0] - (platformThickness * 0.5f)), new Vector2(groundWidth, platformThickness), new Color(0.27f, 0.29f, 0.34f), true, 0);
        CreateStagePiece(stageRoot.transform, "PlatformLeft", new Vector2(-arenaSize.x * 0.18f, platformHeights[1] - (platformThickness * 0.5f)), new Vector2(lowPlatformWidth, platformThickness), new Color(0.34f, 0.36f, 0.42f), true, 0);
        CreateStagePiece(stageRoot.transform, "PlatformRight", new Vector2(arenaSize.x * 0.2f, platformHeights[1] - (platformThickness * 0.5f)), new Vector2(midPlatformWidth, platformThickness), new Color(0.34f, 0.36f, 0.42f), true, 0);
        CreateStagePiece(stageRoot.transform, "PlatformTop", new Vector2(0f, platformHeights[2] - (platformThickness * 0.5f)), new Vector2(topPlatformWidth, platformThickness), new Color(0.4f, 0.42f, 0.48f), true, 0);
        CreateStagePiece(stageRoot.transform, "WallLeft", new Vector2(-arenaSize.x * 0.5f, 0f), new Vector2(wallThickness, arenaSize.y), new Color(0.22f, 0.24f, 0.29f), true, 0);
        CreateStagePiece(stageRoot.transform, "WallRight", new Vector2(arenaSize.x * 0.5f, 0f), new Vector2(wallThickness, arenaSize.y), new Color(0.22f, 0.24f, 0.29f), true, 0);
    }

    private float[] GetPlatformHeights()
    {
        return new[]
        {
            -arenaSize.y * 0.33f,
            0f,
            arenaSize.y * 0.24f
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

    private void OnValidate()
    {
        arenaSize.x = Mathf.Max(12f, arenaSize.x);
        arenaSize.y = Mathf.Max(8f, arenaSize.y);
        platformThickness = Mathf.Max(0.25f, platformThickness);
        wallThickness = Mathf.Max(0.25f, wallThickness);
        beatsPerMinute = Mathf.Max(1f, beatsPerMinute);
        perfectWindow = Mathf.Max(0.01f, perfectWindow);
        warningWindow = Mathf.Max(perfectWindow, warningWindow);
        playerMaxHealth = Mathf.Max(1, playerMaxHealth);
        playerMoveSpeed = Mathf.Max(0.1f, playerMoveSpeed);
        playerJumpForce = Mathf.Max(0.1f, playerJumpForce);
        playerDashSpeed = Mathf.Max(playerMoveSpeed, playerDashSpeed);
        playerDashDuration = Mathf.Max(0.01f, playerDashDuration);
        playerDashCooldown = Mathf.Max(0.01f, playerDashCooldown);
        playerAttackRange = Mathf.Max(0.1f, playerAttackRange);
        playerAttackCooldown = Mathf.Max(0.01f, playerAttackCooldown);
        playerBaseDamage = Mathf.Max(1, playerBaseDamage);
        playerPerfectDamageMultiplier = Mathf.Max(1f, playerPerfectDamageMultiplier);
        playerWeakDamageMultiplier = Mathf.Clamp(playerWeakDamageMultiplier, 0.1f, 1f);
        playerAttackPointDistance = Mathf.Max(0.1f, playerAttackPointDistance);
        enemyMaxHealth = Mathf.Max(1, enemyMaxHealth);
        enemyMoveSpeed = Mathf.Max(0.1f, enemyMoveSpeed);
        enemyJumpForce = Mathf.Max(0.1f, enemyJumpForce);
        enemyAttackDistance = Mathf.Max(0.1f, enemyAttackDistance);
        enemyVerticalAttackTolerance = Mathf.Max(0.1f, enemyVerticalAttackTolerance);
        enemyJumpTriggerHeight = Mathf.Max(0.1f, enemyJumpTriggerHeight);
        enemyAttackRange = Mathf.Max(0.1f, enemyAttackRange);
        enemyAttackCooldown = Mathf.Max(0.01f, enemyAttackCooldown);
        enemyBaseDamage = Mathf.Max(1, enemyBaseDamage);
        enemyAttackPointDistance = Mathf.Max(0.1f, enemyAttackPointDistance);
        enemyRepathDelay = Mathf.Max(0.1f, enemyRepathDelay);
        startingEnemyCount = Mathf.Max(1, startingEnemyCount);
        additionalEnemiesPerWave = Mathf.Max(0, additionalEnemiesPerWave);
        timeBetweenWaves = Mathf.Max(0f, timeBetweenWaves);
    }
}
