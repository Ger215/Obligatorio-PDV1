using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameHudController : SingletonBehaviour<GameHudController>
{
    [Header("HUD")]
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text experienceText;

    [Header("XP Animation")]
    [Tooltip("Duración del count-up del contador de XP.")]
    [SerializeField] private float xpCountUpDuration = 0.35f;
    [Tooltip("Escala pico del punch al recibir XP (1 = sin punch).")]
    [SerializeField] private float xpPunchScale = 1.25f;
    [Tooltip("Duración total del punch (subir + bajar).")]
    [SerializeField] private float xpPunchDuration = 0.18f;
    [SerializeField] private TMP_Text[] abilitySlotTexts;
    [SerializeField] private GameObject abilityShopPanel;
    [SerializeField] private Button[] abilityButtons;
    [SerializeField] private TMP_Text[] abilityButtonTexts;
    [SerializeField] private Button skipShopButton;

    [Header("Game Over")]
    [SerializeField] private CanvasGroup gameOverGroup;
    [SerializeField] private TMP_Text gameOverWaveText;
    [SerializeField] private float gameOverFadeDuration = 1.2f;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitFromGameOverButton;

    [Header("Pause")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitFromPauseButton;

    public void Bind(GameManager gameManager, PlayerController player)
    {
        if (player != null)
        {
            player.HealthSystem.Damaged += HandleHealthChanged;
            player.HealthSystem.Healed += HandleHealthChanged;

            ExperienceSystem experienceSystem = player.GetComponent<ExperienceSystem>();

            if (experienceSystem != null)
            {
                experienceSystem.ExperienceChanged += HandleExperienceChanged;
                HandleExperienceChanged(experienceSystem.CurrentExperience, experienceSystem.TotalExperienceEarned);
            }

            HandleHealthChanged(player.HealthSystem.CurrentHealth, player.HealthSystem.MaxHealth);
        }

        if (skipShopButton != null)
        {
            skipShopButton.onClick.RemoveAllListeners();
            skipShopButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButton(true);
                GameManager.Instance?.SkipAbilityShop();
            });
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButton(true);
                GameManager.Instance?.RestartGame();
            });
        }

        if (quitFromGameOverButton != null)
        {
            quitFromGameOverButton.onClick.RemoveAllListeners();
            quitFromGameOverButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButton(true);
                GameManager.Instance?.QuitToMenu();
            });
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButton(true);
                GameManager.Instance?.ResumeGame();
            });
        }

        if (quitFromPauseButton != null)
        {
            quitFromPauseButton.onClick.RemoveAllListeners();
            quitFromPauseButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButton(true);
                GameManager.Instance?.QuitToMenu();
            });
        }

        if (gameOverGroup != null)
        {
            gameOverGroup.alpha = 0f;
            gameOverGroup.gameObject.SetActive(false);
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    public void ShowGameOver()
    {
        if (gameOverWaveText != null)
        {
            gameOverWaveText.text = "Game Over";
        }

        if (gameOverGroup != null)
        {
            StopCoroutine("FadeInGameOver");
            StartCoroutine("FadeInGameOver");
        }
    }

    private IEnumerator FadeInGameOver()
    {
        gameOverGroup.alpha = 0f;
        gameOverGroup.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < gameOverFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            gameOverGroup.alpha = Mathf.Clamp01(elapsed / gameOverFadeDuration);
            yield return null;
        }

        gameOverGroup.alpha = 1f;
    }

    public void ShowPause()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
    }

    public void HidePause()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    public void RefreshAbilitySlots(IReadOnlyList<PlayerAbilityDefinition> learnedAbilities, IReadOnlyDictionary<PlayerAbilityDefinition, float> cooldowns)
    {
        if (abilitySlotTexts == null)
        {
            return;
        }

        for (int i = 0; i < abilitySlotTexts.Length; i++)
        {
            if (abilitySlotTexts[i] == null)
            {
                continue;
            }

            if (learnedAbilities == null || i >= learnedAbilities.Count)
            {
                abilitySlotTexts[i].text = $"Skill {i + 1}: Empty";
                continue;
            }

            PlayerAbilityDefinition ability = learnedAbilities[i];
            float remainingCooldown = cooldowns != null && cooldowns.TryGetValue(ability, out float readyTime)
                ? Mathf.Max(0f, readyTime - Time.time)
                : 0f;
            abilitySlotTexts[i].text = remainingCooldown > 0f
                ? $"{i + 1}. {ability.displayName} ({remainingCooldown:0.0}s)"
                : $"{i + 1}. {ability.displayName}";
        }
    }

    public void ShowAbilityShop(IReadOnlyList<PlayerAbilityDefinition> abilities, int currentExperience)
    {
        if (abilityShopPanel != null)
        {
            abilityShopPanel.SetActive(true);
        }

        if (abilityButtons == null)
        {
            return;
        }

        for (int i = 0; i < abilityButtons.Length; i++)
        {
            Button button = abilityButtons[i];
            TMP_Text label = i < abilityButtonTexts.Length ? abilityButtonTexts[i] : null;

            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();

            if (abilities == null || i >= abilities.Count)
            {
                button.gameObject.SetActive(false);
                continue;
            }

            int capturedIndex = i;
            PlayerAbilityDefinition ability = abilities[i];
            button.gameObject.SetActive(true);
            button.interactable = currentExperience >= ability.cost;
            button.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButton(true);
                GameManager.Instance?.SelectOfferedAbility(capturedIndex);
            });

            if (label != null)
            {
                label.text = $"{ability.displayName}\nCost: {ability.cost}\n{ability.description}";
            }
        }
    }

    public void HideAbilityShop()
    {
        if (abilityShopPanel != null)
        {
            abilityShopPanel.SetActive(false);
        }
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        if (healthText != null)
        {
            healthText.text = $"HP: {currentHealth}/{maxHealth}";
        }
    }

    private int displayedExperience;
    private bool xpInitialized;
    private Coroutine xpCountCoroutine;
    private Coroutine xpPunchCoroutine;
    private Vector3 xpTextBaseScale = Vector3.one;

    private void HandleExperienceChanged(int currentExperience, int totalExperience)
    {
        if (experienceText == null) return;

        // Primera vez (bind inicial): seteamos sin animar
        if (!xpInitialized)
        {
            displayedExperience = currentExperience;
            experienceText.text = $"XP: {currentExperience}";
            xpTextBaseScale = experienceText.rectTransform.localScale;
            xpInitialized = true;
            return;
        }

        // Count-up animado desde el valor mostrado hasta el nuevo
        if (xpCountCoroutine != null) StopCoroutine(xpCountCoroutine);
        xpCountCoroutine = StartCoroutine(CountUpExperience(displayedExperience, currentExperience));

        // Solo hacemos punch cuando SUBE (ganaste XP), no cuando bajás (gastaste)
        if (currentExperience > displayedExperience)
        {
            if (xpPunchCoroutine != null) StopCoroutine(xpPunchCoroutine);
            xpPunchCoroutine = StartCoroutine(PunchExperienceScale());
        }
    }

    private IEnumerator CountUpExperience(int from, int to)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, xpCountUpDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease-out cuadrático: arranca rápido, frena al final
            float eased = 1f - (1f - t) * (1f - t);
            int value = Mathf.RoundToInt(Mathf.Lerp(from, to, eased));
            experienceText.text = $"XP: {value}";
            yield return null;
        }

        experienceText.text = $"XP: {to}";
        displayedExperience = to;
        xpCountCoroutine = null;
    }

    private IEnumerator PunchExperienceScale()
    {
        Transform t = experienceText.rectTransform;
        float duration = Mathf.Max(0.01f, xpPunchDuration);
        float half = duration * 0.5f;
        float elapsed = 0f;

        // Sube de 1 al pico
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / half);
            float s = Mathf.Lerp(1f, xpPunchScale, k);
            t.localScale = xpTextBaseScale * s;
            yield return null;
        }

        // Baja del pico a 1
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / half);
            float s = Mathf.Lerp(xpPunchScale, 1f, k);
            t.localScale = xpTextBaseScale * s;
            yield return null;
        }

        t.localScale = xpTextBaseScale;
        xpPunchCoroutine = null;
    }
}
