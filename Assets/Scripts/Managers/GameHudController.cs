using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameHudController : SingletonBehaviour<GameHudController>
{
    [Header("Wave Announcement")]
    [SerializeField] private CanvasGroup waveAnnouncerGroup;
    [SerializeField] private TMP_Text waveAnnouncerText;
    [SerializeField] private float announceFadeInDuration = 0.4f;
    [SerializeField] private float announceHoldDuration = 1.2f;
    [SerializeField] private float announceFadeOutDuration = 0.5f;

    [Header("HUD")]
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text experienceText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text enemiesText;
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

    public void ShowGameOver(int waveReached)
    {
        if (gameOverWaveText != null)
        {
            gameOverWaveText.text = $"Oleada alcanzada: {waveReached}";
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

    public void ShowWaveAnnouncement(int wave)
    {
        if (waveAnnouncerGroup == null || waveAnnouncerText == null)
        {
            return;
        }

        if (waveAnnouncerText != null)
        {
            waveAnnouncerText.text = $"WAVE {wave}";
        }

        StopCoroutine("AnimateWaveAnnouncer");
        StartCoroutine("AnimateWaveAnnouncer");
    }

    private IEnumerator AnimateWaveAnnouncer()
    {
        waveAnnouncerGroup.alpha = 0f;
        waveAnnouncerGroup.gameObject.SetActive(true);
        waveAnnouncerGroup.transform.localScale = Vector3.one * 1.6f;

        float t = 0f;
        while (t < announceFadeInDuration)
        {
            t += Time.deltaTime;
            float progress = t / announceFadeInDuration;
            waveAnnouncerGroup.alpha = progress;
            waveAnnouncerGroup.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, progress);
            yield return null;
        }

        waveAnnouncerGroup.alpha = 1f;
        waveAnnouncerGroup.transform.localScale = Vector3.one;

        yield return new WaitForSeconds(announceHoldDuration);

        t = 0f;
        while (t < announceFadeOutDuration)
        {
            t += Time.deltaTime;
            float progress = t / announceFadeOutDuration;
            waveAnnouncerGroup.alpha = 1f - progress;
            waveAnnouncerGroup.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, progress);
            yield return null;
        }

        waveAnnouncerGroup.alpha = 0f;
        waveAnnouncerGroup.gameObject.SetActive(false);
    }

    public void RefreshWaveState(int currentWave, int enemiesAlive)
    {
        if (waveText != null)
        {
            waveText.text = $"Wave: {currentWave}";
        }

        if (enemiesText != null)
        {
            enemiesText.text = $"Enemies: {enemiesAlive}";
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

    private void HandleExperienceChanged(int currentExperience, int totalExperience)
    {
        if (experienceText != null)
        {
            experienceText.text = $"XP: {currentExperience}";
        }
    }
}
