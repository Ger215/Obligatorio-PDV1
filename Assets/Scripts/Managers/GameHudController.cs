using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameHudController : SingletonBehaviour<GameHudController>
{
    [SerializeField] private Text healthText;
    [SerializeField] private Text experienceText;
    [SerializeField] private Text waveText;
    [SerializeField] private Text enemiesText;
    [SerializeField] private Text[] abilitySlotTexts;
    [SerializeField] private GameObject abilityShopPanel;
    [SerializeField] private Button[] abilityButtons;
    [SerializeField] private Text[] abilityButtonTexts;
    [SerializeField] private Button skipShopButton;

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
            Text label = i < abilityButtonTexts.Length ? abilityButtonTexts[i] : null;

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
            experienceText.text = $"XP: {currentExperience} (Earned: {totalExperience})";
        }
    }
}
