using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Banquito de la Shop que vende UNA habilidad. Muestra el ícono y el precio flotando arriba del
/// banquito y, cuando el Player está cerca, se compra apretando E (gasta XP via
/// <see cref="ExperienceSystem.TrySpend"/>). Convive con la ruleta: son dos formas distintas de
/// conseguir habilidades.
///
/// - Si el Player tiene un slot libre, la habilidad se equipa directo y el banquito se marca como
///   vendido.
/// - Si ya tiene los 3 slots llenos, el cartel pasa a "Apretá 1, 2 o 3 para reemplazar"; la XP
///   recién se gasta cuando se elige el slot (si cancela o se aleja, no pierde XP).
///
/// Uso: poné este componente en cada banquito, agregale un Collider2D en modo trigger (zona de
/// compra), asigná la <see cref="ability"/>, el <see cref="price"/> y la capa del Player en
/// <see cref="targetLayers"/>. El ícono/precio flotante se arma solo en runtime.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AbilityShopStand : MonoBehaviour
{
    [Header("Mercadería")]
    [Tooltip("Habilidad que vende este banquito.")]
    [SerializeField] private PlayerAbilityDefinition ability;
    [Tooltip("Precio en XP. Si es negativo, usa el 'cost' definido en la propia habilidad.")]
    [SerializeField] private int price = -1;

    [Header("Detección del Player")]
    [Tooltip("Capas que pueden comprar (seleccioná la capa del Player).")]
    [SerializeField] private LayerMask targetLayers;

    [Header("Cartel flotante")]
    [Tooltip("Desplazamiento del ícono/precio respecto del banquito.")]
    [SerializeField] private Vector2 displayOffset = new Vector2(0f, 1.6f);
    [Tooltip("Tamaño en unidades de mundo del ícono de la habilidad.")]
    [SerializeField] private float iconWorldSize = 1f;
    [Tooltip("Orden de dibujo del ícono (subilo si queda tapado por el escenario).")]
    [SerializeField] private int sortingOrder = 50;

    [Header("Audio")]
    [SerializeField] private AudioClip purchaseSound;

    private bool sold;
    private bool awaitingSlot;
    private PlayerAbilityController abilityController;
    private ExperienceSystem experience;
    private SpriteRenderer iconRenderer;

    private int Price => price >= 0 ? price : (ability != null ? ability.cost : 0);

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        BuildDisplay();
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleEnter(other.gameObject);
    private void OnTriggerExit2D(Collider2D other) => HandleExit(other.gameObject);

    private void Update()
    {
        if (sold || abilityController == null || Keyboard.current == null) return;

        if (awaitingSlot)
        {
            int slot = ReadSlotKey();
            if (slot >= 0) TryReplaceSlot(slot);
            // E de nuevo cancela la elección de slot.
            else if (Keyboard.current.eKey.wasPressedThisFrame) CancelSlotChoice();
            return;
        }

        bool buyPressed = Keyboard.current.eKey.wasPressedThisFrame
                       || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
        if (buyPressed) TryBuy();
    }

    private void TryBuy()
    {
        if (ability == null) return;

        if (System.Array.IndexOf(GetLearnedArray(), ability) >= 0)
        {
            SignUI.Instance?.Show($"Ya tenés {ability.displayName}.");
            return;
        }

        if (experience == null || experience.CurrentExperience < Price)
        {
            SignUI.Instance?.Show($"Te faltan {Price - (experience != null ? experience.CurrentExperience : 0)} XP para {ability.displayName}.");
            return;
        }

        if (!abilityController.IsAbilityCapacityReached)
        {
            if (experience.TrySpend(Price) && abilityController.LearnAbility(ability))
            {
                Complete();
            }
            return;
        }

        // Slots llenos: pasamos a elegir cuál reemplazar (la XP se gasta al confirmar).
        awaitingSlot = true;
        SignUI.Instance?.Show($"Apretá 1, 2 o 3 para reemplazar y comprar {ability.displayName} ({Price} XP). E para cancelar.");
    }

    private void TryReplaceSlot(int slotIndex)
    {
        if (experience == null || !experience.TrySpend(Price))
        {
            SignUI.Instance?.Show($"Te faltan XP para {ability.displayName}.");
            return;
        }

        if (abilityController.LearnAbilityInSlot(ability, slotIndex))
        {
            Complete();
        }
    }

    private void CancelSlotChoice()
    {
        awaitingSlot = false;
        ShowBuyPrompt();
    }

    private void Complete()
    {
        sold = true;
        awaitingSlot = false;
        if (purchaseSound != null) AudioSource.PlayClipAtPoint(purchaseSound, transform.position);
        SignUI.Instance?.Hide();
        if (iconRenderer != null) iconRenderer.transform.parent.gameObject.SetActive(false);
    }

    private void HandleEnter(GameObject other)
    {
        if (sold || abilityController != null) return;
        if (((1 << other.layer) & targetLayers.value) == 0) return;

        PlayerAbilityController controller = other.GetComponentInParent<PlayerAbilityController>();
        if (controller == null) return;

        abilityController = controller;
        experience = controller.GetComponent<ExperienceSystem>();
        ShowBuyPrompt();
    }

    private void HandleExit(GameObject other)
    {
        if (((1 << other.layer) & targetLayers.value) == 0) return;
        if (other.GetComponentInParent<PlayerAbilityController>() != abilityController) return;

        abilityController = null;
        experience = null;
        awaitingSlot = false;
        SignUI.Instance?.Hide();
    }

    private void ShowBuyPrompt()
    {
        if (ability == null) return;
        SignUI.Instance?.Show($"Apretá E para comprar {ability.displayName} ({Price} XP).");
    }

    private int ReadSlotKey()
    {
        Keyboard k = Keyboard.current;
        if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) return 0;
        if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) return 1;
        if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) return 2;
        return -1;
    }

    private PlayerAbilityDefinition[] GetLearnedArray()
    {
        var learned = abilityController.LearnedAbilities;
        var array = new PlayerAbilityDefinition[learned.Count];
        for (int i = 0; i < learned.Count; i++) array[i] = learned[i];
        return array;
    }

    private void BuildDisplay()
    {
        if (ability == null || ability.icon == null) return;

        var displayGo = new GameObject("Display");
        displayGo.transform.SetParent(transform, false);
        displayGo.transform.localPosition = displayOffset;

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(displayGo.transform, false);
        iconRenderer = iconGo.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = ability.icon;
        iconRenderer.sortingOrder = sortingOrder;

        float spriteSize = Mathf.Max(ability.icon.bounds.size.x, ability.icon.bounds.size.y);
        if (spriteSize > 0.0001f)
        {
            iconGo.transform.localScale = Vector3.one * (iconWorldSize / spriteSize);
        }
    }
}
