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

    [Header("XP Icon (orbe/cristal)")]
    [Tooltip("Ícono de XP (orbe/cristal/moneda) que va al lado del número. Hace punch al ganar XP.")]
    [SerializeField] private Image xpIcon;
    [Tooltip("Opcional: imagen de glow detrás del ícono. Late suave de forma continua.")]
    [SerializeField] private Image xpIconGlow;
    [Tooltip("Prefijo del texto. Con el ícono puesto, dejalo vacío para mostrar solo el número.")]
    [SerializeField] private string xpLabelPrefix = "";

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
    [Tooltip("Contenedor dentro de tu Canvas del HUD donde se generan los slots. " +
             "Si lo dejás vacío, se crea un Canvas propio aparte (los slots quedarían por encima de los paneles).")]
    [SerializeField] private RectTransform abilitySlotsParent;
    [Tooltip("Tamaño en píxeles de cada slot de habilidad equipada.")]
    [SerializeField] private Vector2 abilitySlotSize = new Vector2(72f, 72f);
    [SerializeField] private float abilitySlotSpacing = 10f;
    [Tooltip("Posición anclada (desde abajo-centro de la pantalla) de la fila de slots.")]
    [SerializeField] private Vector2 abilitySlotsAnchoredPosition = new Vector2(0f, 24f);

    private const int AbilitySlotCount = 3;
    private static readonly string[] AbilitySlotKeyLabels = { "1", "2", "3" };
    private GameObject abilitySlotsRow;
    private Image[] abilitySlotIcons;
    private Image[] abilitySlotGlows;
    private TextMeshProUGUI[] abilitySlotCooldownTexts;
    private PlayerAbilityController abilityController;
    private static readonly Color ActiveEffectGlowColor = new Color(1f, 0.85f, 0.2f, 0f);

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

    [Header("End Of Demo")]
    [SerializeField] private CanvasGroup endOfDemoGroup;
    [SerializeField] private float endOfDemoFadeDuration = 1.5f;
    [SerializeField] private Button quitFromEndOfDemoButton;

    [Header("Level Complete")]
    [SerializeField] private CanvasGroup levelCompleteGroup;
    [SerializeField] private float levelCompleteFadeDuration = 1f;
    [SerializeField] private Button continueButton;

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
        if (xpIconGlow != null)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 3f) + 1f) * 0.5f;
            Color c = xpIconGlow.color;
            c.a = Mathf.Lerp(0.25f, 0.65f, pulse);
            xpIconGlow.color = c;
        }

        if (abilityController == null)
        {
            return;
        }

        RefreshAbilitySlots(abilityController.LearnedAbilities, abilityController.Cooldowns, abilityController.ActiveEffects);
    }

    private void BuildAbilitySlotsUI()
    {
        Transform slotsParent;

        // Preferido: generar los slots dentro del Canvas del HUD (asignado en el Inspector) para que
        // los paneles de fin (Nivel Completado / Fin de Demo / Victory) puedan taparlos. Si no se
        // asigna, se cae al comportamiento viejo de crear un Canvas propio por encima de todo.
        if (abilitySlotsParent != null)
        {
            slotsParent = abilitySlotsParent;
        }
        else
        {
            var canvasGo = new GameObject("AbilitySlotsCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            slotsParent = canvasGo.transform;
        }

        var rowGo = new GameObject("AbilitySlots");
        rowGo.transform.SetParent(slotsParent, false);
        abilitySlotsRow = rowGo;

        var rowRect = rowGo.AddComponent<RectTransform>();
        if (abilitySlotsParent != null)
        {
            // Con contenedor asignado: la fila se centra dentro de él y vos controlás la posición
            // moviendo el contenedor en el Inspector (ej: llevarlo a la derecha para no tapar la
            // barra de vida del boss). El ContentSizeFitter hace que la fila mida lo justo.
            rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = Vector2.zero;
        }
        else
        {
            // Sin contenedor (Canvas propio): anclado abajo-centro como antes.
            rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0f);
            rowRect.pivot = new Vector2(0.5f, 0f);
            rowRect.anchoredPosition = abilitySlotsAnchoredPosition;
        }

        var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = abilitySlotSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = rowGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        abilitySlotIcons = new Image[AbilitySlotCount];
        abilitySlotGlows = new Image[AbilitySlotCount];
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

            var glowGo = new GameObject("ActiveGlow");
            glowGo.transform.SetParent(slotGo.transform, false);
            var glowRect = glowGo.AddComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-4f, -4f);
            glowRect.offsetMax = new Vector2(4f, 4f);
            var glow = glowGo.AddComponent<Image>();
            glow.color = ActiveEffectGlowColor;
            glow.raycastTarget = false;
            abilitySlotGlows[i] = glow;

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
                RefreshAbilitySlots(abilityController.LearnedAbilities, abilityController.Cooldowns, abilityController.ActiveEffects);
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

        if (quitFromEndOfDemoButton != null)
        {
            quitFromEndOfDemoButton.onClick.RemoveAllListeners();
            quitFromEndOfDemoButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButton(true);
                GameManager.Instance?.QuitToMenu();
            });
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButton(true);
                GameManager.Instance?.ContinueToNextScene();
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

        if (endOfDemoGroup != null)
        {
            endOfDemoGroup.alpha = 0f;
            endOfDemoGroup.gameObject.SetActive(false);
        }

        if (levelCompleteGroup != null)
        {
            levelCompleteGroup.alpha = 0f;
            levelCompleteGroup.gameObject.SetActive(false);
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
        SetAbilitySlotsVisible(false);

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

    public void ShowEndOfDemo()
    {
        HideBossHealthBar();
        SetAbilitySlotsVisible(false);
        AudioManager.Instance?.FadeOutMusic(endOfDemoFadeDuration);

        if (endOfDemoGroup != null)
        {
            StopCoroutine("FadeInEndOfDemo");
            StartCoroutine("FadeInEndOfDemo");
        }
    }

    private IEnumerator FadeInEndOfDemo()
    {
        endOfDemoGroup.alpha = 0f;
        endOfDemoGroup.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < endOfDemoFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            endOfDemoGroup.alpha = Mathf.Clamp01(elapsed / endOfDemoFadeDuration);
            yield return null;
        }

        endOfDemoGroup.alpha = 1f;
    }

    private void SetAbilitySlotsVisible(bool visible)
    {
        if (abilitySlotsRow != null)
        {
            abilitySlotsRow.SetActive(visible);
        }
    }

    public void ShowLevelComplete()
    {
        HideBossHealthBar();
        SetAbilitySlotsVisible(false);
        AudioManager.Instance?.FadeOutMusic(levelCompleteFadeDuration);

        if (levelCompleteGroup != null)
        {
            StopCoroutine("FadeInLevelComplete");
            StartCoroutine("FadeInLevelComplete");
        }
    }

    private IEnumerator FadeInLevelComplete()
    {
        levelCompleteGroup.alpha = 0f;
        levelCompleteGroup.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < levelCompleteFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            levelCompleteGroup.alpha = Mathf.Clamp01(elapsed / levelCompleteFadeDuration);
            yield return null;
        }

        levelCompleteGroup.alpha = 1f;
    }

    public void ShowGameOver()
    {
        SetAbilitySlotsVisible(false);

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

    public void RefreshAbilitySlots(IReadOnlyList<PlayerAbilityDefinition> learnedAbilities, IReadOnlyDictionary<PlayerAbilityDefinition, float> cooldowns, IReadOnlyDictionary<PlayerAbilityDefinition, float> activeEffects = null)
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

        RefreshAbilitySlotIcons(learnedAbilities, cooldowns, activeEffects);
    }

    private void RefreshAbilitySlotIcons(IReadOnlyList<PlayerAbilityDefinition> learnedAbilities, IReadOnlyDictionary<PlayerAbilityDefinition, float> cooldowns, IReadOnlyDictionary<PlayerAbilityDefinition, float> activeEffects)
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

            Image glow = abilitySlotGlows != null ? abilitySlotGlows[i] : null;
            if (glow != null)
            {
                bool isActive = ability != null && activeEffects != null
                    && activeEffects.TryGetValue(ability, out float activeUntil) && Time.time < activeUntil;

                if (isActive)
                {
                    float pulse = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                    Color color = ActiveEffectGlowColor;
                    color.a = Mathf.Lerp(0.35f, 0.7f, pulse);
                    glow.color = color;
                }
                else
                {
                    glow.color = ActiveEffectGlowColor;
                }
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
    private Coroutine xpIconPunchCoroutine;
    private Vector3 xpTextBaseScale = Vector3.one;
    private Vector3 xpIconBaseScale = Vector3.one;

    private void HandleExperienceChanged(int currentExperience, int totalExperience)
    {
        if (experienceText == null) return;

        // Primera vez (bind inicial): seteamos sin animar
        if (!xpInitialized)
        {
            displayedExperience = currentExperience;
            experienceText.text = $"{xpLabelPrefix}{currentExperience}";
            xpTextBaseScale = experienceText.rectTransform.localScale;
            if (xpIcon != null) xpIconBaseScale = xpIcon.rectTransform.localScale;
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

            if (xpIcon != null)
            {
                if (xpIconPunchCoroutine != null) StopCoroutine(xpIconPunchCoroutine);
                xpIconPunchCoroutine = StartCoroutine(PunchIconScale());
            }
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
            experienceText.text = $"{xpLabelPrefix}{value}";
            yield return null;
        }

        experienceText.text = $"{xpLabelPrefix}{to}";
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

    private IEnumerator PunchIconScale()
    {
        Transform t = xpIcon.rectTransform;
        float duration = Mathf.Max(0.01f, xpPunchDuration);
        float half = duration * 0.5f;
        float elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / half);
            float s = Mathf.Lerp(1f, xpPunchScale, k);
            t.localScale = xpIconBaseScale * s;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / half);
            float s = Mathf.Lerp(xpPunchScale, 1f, k);
            t.localScale = xpIconBaseScale * s;
            yield return null;
        }

        t.localScale = xpIconBaseScale;
        xpIconPunchCoroutine = null;
    }
}
