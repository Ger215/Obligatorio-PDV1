using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : SingletonBehaviour<GameManager>
{
    [Header("References")]
    [SerializeField] private PlayerAbilityCatalog abilityCatalog;
    [SerializeField] private GameHudController hudController;
    [SerializeField] private AudioManager audioManager;

    [Header("Shop")]
    [Tooltip("Cantidad de habilidades ofrecidas en cada apertura de la shop.")]
    [SerializeField] private int offeredAbilitiesCount = 3;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

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
        }

        hudController?.Bind(this, PlayerController.Instance);
        audioManager?.PlayBackgroundMusic();
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
        Time.timeScale = 0f;
        hudController?.ShowVictory();
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
}
