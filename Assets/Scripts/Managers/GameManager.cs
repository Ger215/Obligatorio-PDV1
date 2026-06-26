using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum VictoryAction
{
    ShowVictoryScreen,
    LoadNextScene,
    ShowEndOfDemo
}

public class GameManager : SingletonBehaviour<GameManager>
{
    [Header("References")]
    [SerializeField] private PlayerAbilityCatalog abilityCatalog;
    [SerializeField] private GameHudController hudController;
    [SerializeField] private AudioManager audioManager;
    [Tooltip("Opcional: fundido a negro al derrotar al boss antes de cargar la pantalla de carga. " +
             "Si está vacío, pasa directo sin fundido.")]
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private float victoryFadeDuration = 0.6f;

    [Header("Shop")]
    [Tooltip("Cantidad de habilidades ofrecidas en cada apertura de la shop.")]
    [SerializeField] private int offeredAbilitiesCount = 3;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Escena intermedia de carga (personaje corriendo + 'Cargando Nivel...'). " +
             "Si está vacía, se carga la próxima escena de forma directa, sin pantalla de carga.")]
    [SerializeField] private string loadingSceneName = "Loading";

    [Header("Progression")]
    [Tooltip("Qué hace este nivel al derrotar a su boss. " +
             "LoadNextScene: pasa al próximo nivel llevando el estado del Player. " +
             "ShowEndOfDemo: muestra la pantalla de fin de demo. " +
             "ShowVictoryScreen: pantalla de victoria clásica.")]
    [SerializeField] private VictoryAction victoryAction = VictoryAction.ShowVictoryScreen;
    [Tooltip("Nombre de la próxima escena a cargar cuando victoryAction = LoadNextScene.")]
    [SerializeField] private string nextSceneName = "Level 2";

    [Header("Death FX")]
    [SerializeField] private GameObject enemyDeathFXPrefab;
    [SerializeField] private GameObject floatingTextPrefab;
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private Color xpFloatingTextColor = new Color(0.3f, 1f, 0.4f);

    private readonly List<PlayerAbilityDefinition> offeredAbilities = new List<PlayerAbilityDefinition>();

    private bool shopOpen;
    private bool isPaused;
    private bool isGameOver;
    private bool isVictory;
    private HealthSystem playerHealth;
    private ExperienceSystem playerExperience;
    private PlayerAbilityController playerAbilities;

    public IReadOnlyList<PlayerAbilityDefinition> OfferedAbilities => offeredAbilities;
    public bool ShopOpen => shopOpen;
    public bool IsPaused => isPaused;
    public bool IsGameOver => isGameOver;
    public bool IsVictory => isVictory;

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

            RestorePlayerStateIfAny();
        }

        hudController?.Bind(this, PlayerController.Instance);
        StartCoroutine(PlayFallbackMusic());
    }

    // El backgroundMusic es solo un fallback: esperamos un frame para que el RoomEnvironment del
    // room inicial pida su tema (vía CameraController.Start → RoomChanged). Si el room ya puso
    // música, no la pisamos; si no, recién ahí ponemos el tema de fondo global.
    private IEnumerator PlayFallbackMusic()
    {
        yield return null;
        if (audioManager != null && !audioManager.IsMusicPlaying)
            audioManager.PlayBackgroundMusic();
    }

    private void Update()
    {
        if (isGameOver || isVictory)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    protected override void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.Died -= HandlePlayerDeath;
        }
    }

    /// <summary>
    /// Llamado por EnemyXpReward cuando un enemigo muere. Otorga XP, spawnea floating text y FX.
    /// </summary>
    public void AwardEnemyKill(Vector3 worldPosition, int xpReward)
    {
        if (enemyDeathFXPrefab != null)
        {
            Vector3 spawnPos = worldPosition + Vector3.up * 0.5f;
            GameObject fx = Instantiate(enemyDeathFXPrefab, spawnPos, Quaternion.identity);
            float scale = Random.Range(2.5f, 3.5f);
            fx.transform.localScale = new Vector3(scale, scale, 1f);
            Animator fxAnimator = fx.GetComponent<Animator>();
            if (fxAnimator != null) fxAnimator.speed = 1.5f;
            Destroy(fx, 0.35f);
        }

        if (playerExperience != null && xpReward > 0)
        {
            playerExperience.AddExperience(xpReward);

            if (floatingTextPrefab != null && hudCanvas != null && Camera.main != null)
            {
                GameObject instance = Instantiate(floatingTextPrefab, hudCanvas.transform);
                Vector2 screenPoint = Camera.main.WorldToScreenPoint(worldPosition);
                Camera uiCamera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)hudCanvas.transform, screenPoint, uiCamera, out Vector2 canvasPos);
                ((RectTransform)instance.transform).anchoredPosition = canvasPos;
                FloatingText floatingText = instance.GetComponent<FloatingText>();
                floatingText?.Play($"+{xpReward} XP", xpFloatingTextColor);
            }
        }
    }

    /// <summary>
    /// Abre la shop de habilidades. Llamalo desde un trigger en el mundo, un NPC, post-boss, etc.
    /// </summary>
    public void OpenAbilityShop()
    {
        if (shopOpen || isGameOver || isPaused) return;
        TryOpenAbilityShop();
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
        CloseShop();
    }

    public void SkipAbilityShop()
    {
        if (!shopOpen) return;
        CloseShop();
    }

    private void CloseShop()
    {
        shopOpen = false;
        offeredAbilities.Clear();
        hudController?.HideAbilityShop();
        Time.timeScale = 1f;
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

        while (offeredAbilities.Count < offeredAbilitiesCount && availableAbilities.Count > 0)
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

    private void HandlePlayerDeath(HealthSystem _)
    {
        TriggerGameOver();
    }

    private void TriggerGameOver()
    {
        if (isGameOver) return;

        isGameOver = true;
        isPaused = false;
        Time.timeScale = 0f;
        hudController?.ShowGameOver();
    }

    public void TriggerVictory()
    {
        if (isGameOver || isVictory) return;

        isVictory = true;

        switch (victoryAction)
        {
            case VictoryAction.LoadNextScene:
                CapturePlayerState();
                LoadNextSceneWithLoadingScreen();
                break;

            case VictoryAction.ShowEndOfDemo:
                Time.timeScale = 0f;
                hudController?.ShowEndOfDemo();
                break;

            default:
                Time.timeScale = 0f;
                hudController?.ShowVictory();
                break;
        }
    }

    // Pasa a la escena de carga ("Loading"), que muestra al personaje corriendo + "Cargando Nivel..."
    // y se encarga de cargar la próxima escena en segundo plano. Si no hay escena de carga configurada,
    // carga la próxima escena directo. El estado del Player ya viaja por PlayerStateStore.
    private void LoadNextSceneWithLoadingScreen()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            return;
        }

        StartCoroutine(LoadNextSceneRoutine());
    }

    private IEnumerator LoadNextSceneRoutine()
    {
        Time.timeScale = 1f;

        // Fundido a negro en el nivel actual antes de saltar, para que no corte de golpe.
        if (screenFader != null)
        {
            yield return screenFader.FadeToBlack(victoryFadeDuration);
        }

        string sceneToLoad = !string.IsNullOrEmpty(loadingSceneName) ? loadingSceneName : nextSceneName;

        // Si vamos por la pantalla de carga, le pasamos el destino real.
        if (!string.IsNullOrEmpty(loadingSceneName))
        {
            LevelLoadingScreen.TargetScene = nextSceneName;
        }

        SceneManager.LoadScene(sceneToLoad);
    }

    // Guarda el estado del Player (vida, XP y habilidades aprendidas) para volcarlo en el próximo nivel.
    private void CapturePlayerState()
    {
        if (playerHealth == null || playerExperience == null || playerAbilities == null)
        {
            return;
        }

        PlayerStateStore.Capture(
            playerHealth.CurrentHealth,
            playerExperience.CurrentExperience,
            playerExperience.TotalExperienceEarned,
            playerAbilities.LearnedAbilities);
    }

    // Vuelca el estado capturado en el nivel anterior sobre el Player de esta escena (y lo consume).
    private void RestorePlayerStateIfAny()
    {
        if (!PlayerStateStore.HasState)
        {
            return;
        }

        playerHealth?.RestoreHealth(PlayerStateStore.CurrentHealth);
        playerExperience?.RestoreState(PlayerStateStore.CurrentExperience, PlayerStateStore.TotalExperienceEarned);

        if (playerAbilities != null)
        {
            foreach (PlayerAbilityDefinition ability in PlayerStateStore.LearnedAbilities)
            {
                if (ability != null)
                {
                    playerAbilities.LearnAbility(ability);
                }
            }
        }

        PlayerStateStore.Clear();
    }

    public void TogglePause()
    {
        if (shopOpen || isVictory) return;

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
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = 1f;
        hudController?.HidePause();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        PlayerStateStore.Clear();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        PlayerStateStore.Clear();

        if (!string.IsNullOrEmpty(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            Application.Quit();
        }
    }
}
