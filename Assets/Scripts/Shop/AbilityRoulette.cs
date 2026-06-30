using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Ruleta de habilidades (estilo gacha) para la Shop. El jugador paga XP por girar; sale una
/// habilidad al azar PONDERADA por <see cref="PlayerAbilityDefinition.rouletteWeight"/> entre las
/// que todavía NO tiene, y se equipa al toque en un slot libre del <see cref="PlayerAbilityController"/>.
/// Si los tres slots están ocupados, se muestra un panel para elegir cuál reemplazar.
///
/// Opera sobre el Player de la escena (el mismo que usan los pickups). La XP y las habilidades ya
/// vienen restauradas del nivel anterior por <see cref="PlayerStateRestorer"/>.
/// </summary>
public class AbilityRoulette : MonoBehaviour
{
    [Header("Datos")]
    [Tooltip("Catálogo con todas las habilidades posibles (el pool de la ruleta).")]
    [SerializeField] private PlayerAbilityCatalog catalog;
    [Tooltip("XP que cuesta cada giro.")]
    [SerializeField] private int spinCost = 15;

    [Header("Player (auto si vacío)")]
    [SerializeField] private PlayerController player;

    [Header("UI")]
    [SerializeField] private Button spinButton;
    [SerializeField] private TMP_Text xpText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text messageText;
    [Tooltip("Ícono donde se ve la habilidad (gira durante el sorteo y queda en la que tocó).")]
    [SerializeField] private Image resultIcon;
    [SerializeField] private TMP_Text resultNameText;

    [Header("Animación")]
    [SerializeField] private float spinDuration = 1.2f;
    [Tooltip("Cuántos íconos pasan durante el giro antes de frenar.")]
    [SerializeField] private int spinTicks = 16;
    [SerializeField] private AudioClip spinClip;
    [SerializeField] private AudioClip winClip;

    [Header("Reemplazo de slot (cuando los 3 están llenos)")]
    [SerializeField] private GameObject slotPickerPanel;
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotButtonLabels;

    [Header("Colores por rareza")]
    [SerializeField] private Color commonColor = new Color(0.8f, 0.85f, 0.9f);
    [SerializeField] private Color rareColor = new Color(0.3f, 0.6f, 1f);
    [SerializeField] private Color epicColor = new Color(0.75f, 0.35f, 1f);

    [Header("Abrir/cerrar")]
    [Tooltip("Panel raíz que se muestra/oculta con la tecla. Si está vacío, la ruleta queda siempre visible.")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("Tecla para abrir/cerrar la ruleta.")]
    [SerializeField] private Key toggleKey = Key.R;
    [Tooltip("Tecla para girar (no depende del click, que con el New Input System no anda en UI).")]
    [SerializeField] private Key spinKey = Key.Space;
    [Tooltip("Si está activo, arranca oculta y se abre recién al apretar la tecla.")]
    [SerializeField] private bool startHidden = true;

    private ExperienceSystem experience;
    private PlayerAbilityController abilities;
    private bool spinning;
    private PlayerAbilityDefinition pendingAbility;

    public Key ToggleKey => toggleKey;
    public Key SpinKey => spinKey;
    public int SpinCost => spinCost;

    private void Awake()
    {
        if (slotPickerPanel != null) slotPickerPanel.SetActive(false);

        if (spinButton != null)
        {
            spinButton.onClick.RemoveAllListeners();
            spinButton.onClick.AddListener(Spin);
        }

        if (slotButtons != null)
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                if (slotButtons[i] == null) continue;
                int slot = i;
                slotButtons[i].onClick.RemoveAllListeners();
                slotButtons[i].onClick.AddListener(() => ChooseSlot(slot));
            }
        }
    }

    private void Start()
    {
        ResolvePlayer();
        SetMessage(string.Empty);
        RefreshUi();

        if (startHidden && panelRoot != null) panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (panelRoot != null && Keyboard.current[toggleKey].wasPressedThisFrame) Toggle();

        bool open = panelRoot == null || panelRoot.activeSelf;
        if (open && Keyboard.current[spinKey].wasPressedThisFrame) Spin();
    }

    private void Toggle()
    {
        bool open = !panelRoot.activeSelf;
        panelRoot.SetActive(open);
        if (open)
        {
            ResolvePlayer();
            SetMessage(string.Empty);
            RefreshUi();
        }
    }

    private void ResolvePlayer()
    {
        if (player == null) player = PlayerController.Instance;
        if (player == null) return;

        if (experience == null) experience = player.GetComponent<ExperienceSystem>();
        if (abilities == null) abilities = player.GetComponent<PlayerAbilityController>();
    }

    public void Spin()
    {
        if (spinning || pendingAbility != null) return;

        ResolvePlayer();
        if (catalog == null || experience == null || abilities == null)
        {
            SetMessage("Falta config: revisá el catálogo o que haya un Player en la escena.");
            return;
        }

        List<PlayerAbilityDefinition> pool = catalog.GetUnlearnedAbilities(abilities.LearnedAbilities);
        if (pool.Count == 0)
        {
            SetMessage("¡Ya tenés todas las habilidades!");
            return;
        }

        if (experience.CurrentExperience < spinCost)
        {
            SetMessage("XP insuficiente para girar.");
            return;
        }

        if (!experience.TrySpend(spinCost))
        {
            return;
        }

        PlayerAbilityDefinition rolled = WeightedRandom(pool);
        StartCoroutine(SpinRoutine(rolled, pool));
    }

    private static PlayerAbilityDefinition WeightedRandom(List<PlayerAbilityDefinition> pool)
    {
        float total = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            total += Mathf.Max(0.01f, pool[i].rouletteWeight);
        }

        float pick = Random.value * total;
        for (int i = 0; i < pool.Count; i++)
        {
            pick -= Mathf.Max(0.01f, pool[i].rouletteWeight);
            if (pick <= 0f)
            {
                return pool[i];
            }
        }

        return pool[pool.Count - 1];
    }

    private IEnumerator SpinRoutine(PlayerAbilityDefinition rolled, List<PlayerAbilityDefinition> pool)
    {
        spinning = true;
        if (spinButton != null) spinButton.interactable = false;
        SetMessage("Girando...");
        AudioManager.Instance?.PlaySfx(spinClip);

        int ticks = Mathf.Max(1, spinTicks);
        for (int i = 0; i < ticks; i++)
        {
            PlayerAbilityDefinition preview = pool[Random.Range(0, pool.Count)];
            ShowAbility(preview, "?");
            // Arranca rápido y va frenando (delay creciente).
            float t = (i + 1) / (float)ticks;
            yield return new WaitForSeconds(Mathf.Lerp(0.03f, 0.18f, t * t) * (spinDuration / 1.2f));
        }

        ShowAbility(rolled, rolled.displayName);
        AudioManager.Instance?.PlaySfx(winClip);

        Grant(rolled);

        spinning = false;
        RefreshUi();
    }

    private void Grant(PlayerAbilityDefinition ability)
    {
        if (abilities.LearnAbility(ability))
        {
            SetMessage($"¡Obtuviste {ability.displayName}!");
            return;
        }

        // No quedaban slots libres: el jugador elige cuál reemplazar.
        pendingAbility = ability;
        SetMessage($"Elegí qué habilidad reemplazar por {ability.displayName}.");
        ShowSlotPicker();
    }

    private void ShowSlotPicker()
    {
        if (slotPickerPanel != null) slotPickerPanel.SetActive(true);

        IReadOnlyList<PlayerAbilityDefinition> learned = abilities.LearnedAbilities;
        if (slotButtons == null) return;

        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtonLabels != null && i < slotButtonLabels.Length && slotButtonLabels[i] != null)
            {
                PlayerAbilityDefinition current = i < learned.Count ? learned[i] : null;
                slotButtonLabels[i].text = current != null ? current.displayName : "(vacío)";
            }
        }
    }

    private void ChooseSlot(int slotIndex)
    {
        if (pendingAbility == null) return;

        abilities.LearnAbilityInSlot(pendingAbility, slotIndex);
        SetMessage($"¡Equipaste {pendingAbility.displayName}!");
        pendingAbility = null;

        if (slotPickerPanel != null) slotPickerPanel.SetActive(false);
        RefreshUi();
    }

    private void ShowAbility(PlayerAbilityDefinition ability, string nameOverride)
    {
        if (resultIcon != null)
        {
            resultIcon.sprite = ability.icon;
            resultIcon.enabled = ability.icon != null;
            resultIcon.color = RarityColor(ability.rarity);
        }

        if (resultNameText != null)
        {
            resultNameText.text = nameOverride;
            resultNameText.color = RarityColor(ability.rarity);
        }
    }

    private Color RarityColor(AbilityRarity rarity)
    {
        switch (rarity)
        {
            case AbilityRarity.Rare: return rareColor;
            case AbilityRarity.Epic: return epicColor;
            default: return commonColor;
        }
    }

    private void RefreshUi()
    {
        if (costText != null) costText.text = $"{spinCost} XP";
        if (xpText != null && experience != null) xpText.text = $"XP: {experience.CurrentExperience}";

        if (spinButton != null)
        {
            bool poolHasAbilities = catalog != null && abilities != null
                && catalog.GetUnlearnedAbilities(abilities.LearnedAbilities).Count > 0;
            bool canAfford = experience != null && experience.CurrentExperience >= spinCost;
            spinButton.interactable = !spinning && pendingAbility == null && poolHasAbilities && canAfford;
        }
    }

    private void SetMessage(string message)
    {
        if (messageText != null) messageText.text = message;
    }
}
