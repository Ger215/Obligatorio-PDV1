using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameHudController : SingletonBehaviour<GameHudController>
{
    [Header("HUD")]
    [Tooltip("Opcional. Texto numérico de vida. Dejalo vacío si usás solo la barra.")]
    [SerializeField] private TMP_Text healthText;
    [Tooltip("Relleno rojo de la barra de vida (Image con Image Type = Filled). Mismo patrón que la barra del boss.")]
    [SerializeField] private Image healthBarFill;
    [Tooltip("Duración del deslizamiento de la barra de vida hacia el nuevo valor. 0 = instantáneo.")]
    [SerializeField] private float healthBarLerpDuration = 0.3f;
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

    [Header("Ability Slots (icon UI, generado en runtime)")]
    [Tooltip("Tamaño en píxeles de cada slot de habilidad equipada.")]
    [SerializeField] private Vector2 abilitySlotSize = new Vector2(72f, 72f);
    [SerializeField] private float abilitySlotSpacing = 10f;
    [Tooltip("Posición anclada (desde abajo-centro de la pantalla) de la fila de slots.")]
    [SerializeField] private Vector2 abilitySlotsAnchoredPosition = new Vector2(0f, 24f);

    private const int AbilitySlotCount = 3;
    private static readonly string[] AbilitySlotKeyLabels = { "1", "2", "3" };
    private Image[] abilitySlotIcons;
    private TextMeshProUGUI[] abilitySlotCooldownTexts;
    private PlayerAbilityController abilityController;

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

    [Header("Boss Health Bar")]
    [SerializeField] private GameObject bossHealthBarPanel;
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private Image bossHealthBarFill;

    [Header("Victory")]
    [SerializeField] private CanvasGroup victoryGroup;
    [SerializeField] private float victoryFadeDuration = 1.2f;
    [SerializeField] private Button restartFromVictoryButton;
    [SerializeField] private Button quitFromVictoryButton;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
        {
            return;
        }

        BuildAbilitySlotsUI();
    }

    private void Update()
    {
        if (abilityController == null)
        {
            return;
        }

        RefreshAbilitySlots(abilityController.LearnedAbilities, abilityController.Cooldowns);
    }

    private void BuildAbilitySlotsUI()
    {
        var canvasGo = new GameObject("AbilitySlotsCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var rowGo = new GameObject("AbilitySlots");
        rowGo.transform.SetParent(canvasGo.transform, false);

        var rowRect = rowGo.AddComponent<RectTransform>();
        rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0f);
        rowRect.pivot = new Vector2(0.5f, 0f);
        rowRect.anchoredPosition = abilitySlotsAnchoredPosition;

        var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = abilitySlotSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = rowGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        abilitySlotIcons = new Image[AbilitySlotCount];
        abilitySlotCooldownTexts = new TextMeshProUGUI[AbilitySlotCount];

        for (int i = 0; i < AbilitySlotCount; i++)
        {
            string keyLabel = AbilitySlotKeyLabels[Mathf.Min(i, AbilitySlotKeyLabels.Length - 1)];
            var slotGo = new GameObject($"AbilitySlot{i + 1}");
            slotGo.transform.SetParent(rowGo.transform, false);

            slotGo.AddComponent<RectTransform>();

            var slotBg = slotGo.AddComponent<Image>();
            slotBg.color = new Color(0f, 0f, 0f, 0.55f);

            var layoutElement = slotGo.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = abilitySlotSize.x;
            layoutElement.preferredHeight = abilitySlotSize.y;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(6f, 6f);
            iconRect.offsetMax = new Vector2(-6f, -6f);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.enabled = false;
            abilitySlotIcons[i] = icon;

            var cooldownGo = new GameObject("Cooldown");
            cooldownGo.transform.SetParent(slotGo.transform, false);
            var cooldownRect = cooldownGo.AddComponent<RectTransform>();
            cooldownRect.anchorMin = Vector2.zero;
            cooldownRect.anchorMax = Vector2.one;
            cooldownRect.offsetMin = Vector2.zero;
            cooldownRect.offsetMax = Vector2.zero;
            var cooldownText = cooldownGo.AddComponent<TextMeshProUGUI>();
            cooldownText.alignment = TextAlignmentOptions.Center;
            cooldownText.fontStyle = FontStyles.Bold;
            cooldownText.fontSize = 22f;
            cooldownText.color = Color.white;
            cooldownText.text = string.Empty;
            cooldownText.raycastTarget = false;
            abilitySlotCooldownTexts[i] = cooldownText;

            var keyGo = new GameObject("Key");
            keyGo.transform.SetParent(slotGo.transform, false);
            var keyRect = keyGo.AddComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(0f, 1f);
            keyRect.anchorMax = new Vector2(0f, 1f);
            keyRect.pivot = new Vector2(0f, 1f);
            keyRect.anchoredPosition = new Vector2(2f, -2f);
            keyRect.sizeDelta = new Vector2(20f, 20f);
            var keyText = keyGo.AddComponent<TextMeshProUGUI>();
            keyText.alignment = TextAlignmentOptions.Center;
            keyText.fontStyle = FontStyles.Bold;
            keyText.fontSize = 16f;
            keyText.color = new Color(1f, 0.85f, 0.2f);
            keyText.text = keyLabel;
            keyText.raycastTarget = false;
        }
    }

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

            abilityController = player.GetComponent<PlayerAbilityController>();
            if (abilityController != null)
            {
                RefreshAbilitySlots(abilityController.LearnedAbilities, abilityController.Cooldowns);
            }
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

        if (restartFromVictoryButton != null)
        {
            restartFromVictoryButton.onClick.RemoveAllListeners();
            restartFromVictoryButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButton(true);
                GameManager.Instance?.RestartGame();
            });
        }

        if (quitFromVictoryButton != null)
        {
            quitFromVictoryButton.onClick.RemoveAllListeners();
            quitFromVictoryButton.onClick.AddListener(() =>
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

        if (victoryGroup != null)
        {
            victoryGroup.alpha = 0f;
            victoryGroup.gameObject.SetActive(false);
        }

        if (bossHealthBarPanel != null)
        {
            bossHealthBarPanel.SetActive(false);
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    public void ShowBossHealthBar(string bossName, int current, int max)
    {
        if (bossHealthBarPanel != null)
            bossHealthBarPanel.SetActive(true);

        if (bossNameText != null)
            bossNameText.text = bossName;

        UpdateBossHealthBar(current, max);
    }

    public void UpdateBossHealthBar(int current, int max)
    {
        if (bossHealthBarFill != null && max > 0)
            bossHealthBarFill.fillAmount = (float)current / max;
    }

    public void HideBossHealthBar()
    {
        if (bossHealthBarPanel != null)
            bossHealthBarPanel.SetActive(false);
    }

    public void ShowVictory()
    {
        HideBossHealthBar();

        if (victoryGroup != null)
        {
            StopCoroutine("FadeInVictory");
            StartCoroutine("FadeInVictory");
        }
    }

    private IEnumerator FadeInVictory()
    {
        victoryGroup.alpha = 0f;
        victoryGroup.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < victoryFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            victoryGroup.alpha = Mathf.Clamp01(elapsed / victoryFadeDuration);
            yield return null;
        }

        victoryGroup.alpha = 1f;
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
        for (int i = 0; abilitySlotTexts != null && i < abilitySlotTexts.Length; i++)
        {
            if (abilitySlotTexts[i] == null)
            {
                continue;
            }

            if (learnedAbilities == null || i >= learnedAbilities.Count || learnedAbilities[i] == null)
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

        RefreshAbilitySlotIcons(learnedAbilities, cooldowns);
    }

    private void RefreshAbilitySlotIcons(IReadOnlyList<PlayerAbilityDefinition> learnedAbilities, IReadOnlyDictionary<PlayerAbilityDefinition, float> cooldowns)
    {
        if (abilitySlotIcons == null)
        {
            return;
        }

        for (int i = 0; i < abilitySlotIcons.Length; i++)
        {
            PlayerAbilityDefinition ability = learnedAbilities != null && i < learnedAbilities.Count ? learnedAbilities[i] : null;

            Image icon = abilitySlotIcons[i];
            if (icon != null)
            {
                icon.sprite = ability != null ? ability.icon : null;
                icon.enabled = icon.sprite != null;
            }

            TextMeshProUGUI cooldownText = abilitySlotCooldownTexts[i];
            if (cooldownText == null)
            {
                continue;
            }

            float remainingCooldown = ability != null && cooldowns != null && cooldowns.TryGetValue(ability, out float readyTime)
                ? Mathf.Max(0f, readyTime - Time.time)
                : 0f;
            cooldownText.text = remainingCooldown > 0f ? $"{remainingCooldown:0.0}" : string.Empty;
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

    private Coroutine healthBarCoroutine;

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        if (healthText != null)
        {
            healthText.text = $"HP: {currentHealth}/{maxHealth}";
        }

        if (healthBarFill != null && maxHealth > 0)
        {
            float target = (float)currentHealth / maxHealth;

            if (healthBarLerpDuration <= 0f)
            {
                healthBarFill.fillAmount = target;
            }
            else
            {
                if (healthBarCoroutine != null) StopCoroutine(healthBarCoroutine);
                healthBarCoroutine = StartCoroutine(LerpHealthBar(target));
            }
        }
    }

    private IEnumerator LerpHealthBar(float target)
    {
        float from = healthBarFill.fillAmount;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, healthBarLerpDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease-out: arranca rápido y frena al final
            float eased = 1f - (1f - t) * (1f - t);
            healthBarFill.fillAmount = Mathf.Lerp(from, target, eased);
            yield return null;
        }

        healthBarFill.fillAmount = target;
        healthBarCoroutine = null;
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
