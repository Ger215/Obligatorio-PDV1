using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Generador de la UI de la ruleta de habilidades, con estética pixel-art (fuente PressStart2P,
/// paneles planos con borde, sin sprites suavizados). Crea todo el Canvas ya cableado al componente
/// <see cref="AbilityRoulette"/> para no tener que armarlo a mano en el inspector.
///
/// Uso: abrí la escena Shop y andá a  Tools ▸ Shop ▸ Crear UI Ruleta (Pixel Art).
/// Es seguro: usa Undo (Ctrl+Z) y no pisa una UI ya creada (avisa y aborta si ya existe).
/// </summary>
public static class RouletteUIBuilder
{
    private const string CanvasName = "RouletteCanvas";
    private const string FontPath = "Assets/Fonts/PressStart2P-Regular SDF.asset";

    // Paleta pixel-art.
    private static readonly Color BgColor      = new Color32(0x14, 0x12, 0x2B, 0xFF); // morado oscuro
    private static readonly Color BorderColor  = new Color32(0xE0, 0xC5, 0x6E, 0xFF); // dorado
    private static readonly Color PanelColor   = new Color32(0x22, 0x1E, 0x40, 0xFF);
    private static readonly Color ButtonColor  = new Color32(0x3A, 0x2E, 0x5C, 0xFF);
    private static readonly Color FrameColor   = new Color32(0x0E, 0x0C, 0x1C, 0xFF);
    private static readonly Color TextColor    = new Color32(0xF2, 0xEC, 0xD8, 0xFF);
    private static readonly Color AccentColor  = new Color32(0xE0, 0xC5, 0x6E, 0xFF);

    [MenuItem("Tools/Shop/Crear UI Ruleta (Pixel Art)")]
    public static void Build()
    {
        if (Object.FindFirstObjectByType<AbilityRoulette>() != null
            || GameObject.Find(CanvasName) != null)
        {
            EditorUtility.DisplayDialog("Ruleta",
                "Ya hay una UI de ruleta (o un AbilityRoulette) en la escena. Borrala antes de regenerarla.",
                "Ok");
            return;
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogWarning($"RouletteUIBuilder: no encontré la fuente en {FontPath}. Uso la default de TMP.");
        }

        // --- Canvas ---
        var canvasGo = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Crear UI Ruleta");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640, 360);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f; // priorizar alto: pixel-art se ve consistente

        EnsureEventSystem();

        // --- Panel principal (modal centrado) ---
        RectTransform panel = CreateFramed("RoulettePanel", canvasGo.transform, new Vector2(460, 300),
            PanelColor, BorderColor, 4f);
        // CreateFramed devuelve el Fill interno; el root que se muestra/oculta es el marco externo.
        GameObject panelRoot = panel.parent.gameObject;

        CreateText("Title", panel, "RULETA DE HABILIDADES", font, 18, AccentColor,
            new Vector2(0, 120), new Vector2(440, 30));

        // Ventana de resultado (marco) + ícono adentro.
        RectTransform frame = CreateFramed("ResultFrame", panel, new Vector2(120, 120),
            FrameColor, BorderColor, 3f);
        SetAnchoredCenter(frame, new Vector2(0, 36));
        Image resultIcon = CreateImage("ResultIcon", frame, new Vector2(96, 96), Vector2.zero, Color.white);
        resultIcon.preserveAspect = true;
        resultIcon.enabled = false;

        TMP_Text resultName = CreateText("ResultName", panel, "?", font, 16, TextColor,
            new Vector2(0, -36), new Vector2(440, 24));

        TMP_Text xpText = CreateText("XpText", panel, "XP: 0", font, 12, TextColor,
            new Vector2(-110, -70), new Vector2(200, 20));
        xpText.alignment = TextAlignmentOptions.Left;
        TMP_Text costText = CreateText("CostText", panel, "0 XP", font, 12, AccentColor,
            new Vector2(110, -70), new Vector2(200, 20));
        costText.alignment = TextAlignmentOptions.Right;

        TMP_Text messageText = CreateText("MessageText", panel, "", font, 10, TextColor,
            new Vector2(0, -98), new Vector2(440, 30));

        // Botón girar.
        Button spinButton = CreateButton("SpinButton", panel, "GIRAR", font, 16,
            new Vector2(0, -128), new Vector2(200, 40), out _);

        // --- Panel de reemplazo de slot (oculto) ---
        RectTransform slotPanel = CreateFramed("SlotPickerPanel", panel, new Vector2(460, 300),
            BgColor, BorderColor, 4f);
        SetAnchoredCenter(slotPanel, Vector2.zero);
        // CreateFramed devuelve el Fill interno; para mostrar/ocultar el panel hay que tocar el
        // marco externo (su padre), si no el borde dorado queda prendido tapando todo.
        GameObject slotPanelRoot = slotPanel.parent.gameObject;
        CreateText("SlotTitle", slotPanel, "¿QUÉ HABILIDAD REEMPLAZÁS?", font, 12, AccentColor,
            new Vector2(0, 110), new Vector2(440, 24));

        var slotButtons = new Button[3];
        var slotLabels = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            slotButtons[i] = CreateButton($"SlotButton{i}", slotPanel, $"Slot {i + 1}", font, 12,
                new Vector2(0, 50 - i * 55), new Vector2(360, 44), out slotLabels[i]);
        }
        slotPanelRoot.SetActive(false);

        // --- Componente + cableado ---
        var roulette = canvasGo.AddComponent<AbilityRoulette>();
        var so = new SerializedObject(roulette);
        AssignCatalog(so);
        so.FindProperty("spinButton").objectReferenceValue = spinButton;
        so.FindProperty("xpText").objectReferenceValue = xpText;
        so.FindProperty("costText").objectReferenceValue = costText;
        so.FindProperty("messageText").objectReferenceValue = messageText;
        so.FindProperty("resultIcon").objectReferenceValue = resultIcon;
        so.FindProperty("resultNameText").objectReferenceValue = resultName;
        so.FindProperty("slotPickerPanel").objectReferenceValue = slotPanelRoot;
        so.FindProperty("panelRoot").objectReferenceValue = panelRoot;
        SetArray(so, "slotButtons", slotButtons);
        SetArray(so, "slotButtonLabels", slotLabels);
        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = canvasGo;
        Debug.Log("RouletteUIBuilder: UI de la ruleta creada y cableada. Revisá la posición y el spinCost en el inspector.");
    }

    private static void AssignCatalog(SerializedObject so)
    {
        string[] guids = AssetDatabase.FindAssets("t:PlayerAbilityCatalog");
        if (guids.Length == 0) return;
        var catalog = AssetDatabase.LoadAssetAtPath<PlayerAbilityCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        so.FindProperty("catalog").objectReferenceValue = catalog;
    }

    private static void SetArray(SerializedObject so, string propName, Object[] values)
    {
        SerializedProperty prop = so.FindProperty(propName);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(es, "Crear EventSystem");
    }

    // Crea un panel con borde: imagen externa (borde) + imagen interna (relleno). Devuelve el RectTransform
    // del relleno, que sirve de contenedor del contenido.
    private static RectTransform CreateFramed(string name, Transform parent, Vector2 size,
        Color fill, Color border, float thickness)
    {
        var outerGo = new GameObject(name, typeof(Image));
        outerGo.transform.SetParent(parent, false);
        var outer = (RectTransform)outerGo.transform;
        outer.sizeDelta = size;
        outer.anchorMin = outer.anchorMax = outer.pivot = new Vector2(0.5f, 0.5f);
        outer.anchoredPosition = Vector2.zero;
        var outerImg = outerGo.GetComponent<Image>();
        outerImg.color = border;

        var innerGo = new GameObject("Fill", typeof(Image));
        innerGo.transform.SetParent(outerGo.transform, false);
        var inner = (RectTransform)innerGo.transform;
        inner.anchorMin = Vector2.zero;
        inner.anchorMax = Vector2.one;
        inner.offsetMin = new Vector2(thickness, thickness);
        inner.offsetMax = new Vector2(-thickness, -thickness);
        innerGo.GetComponent<Image>().color = fill;

        return inner;
    }

    private static void SetAnchoredCenter(RectTransform rt, Vector2 pos)
    {
        // El RectTransform devuelto por CreateFramed es el "Fill" (anclado en stretch al padre).
        // Para reposicionar el panel hay que mover su padre (el marco externo).
        RectTransform target = rt.parent as RectTransform;
        target.anchorMin = target.anchorMax = target.pivot = new Vector2(0.5f, 0.5f);
        target.anchoredPosition = pos;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, TMP_FontAsset font,
        float size, Color color, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateButton(string name, Transform parent, string label, TMP_FontAsset font,
        float size, Vector2 pos, Vector2 sizeDelta, out TMP_Text labelText)
    {
        RectTransform fill = CreateFramed(name, parent, sizeDelta, ButtonColor, BorderColor, 2f);
        RectTransform frame = fill.parent as RectTransform;
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = pos;

        var button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = frame.GetComponent<Image>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        labelText = CreateText("Label", fill, label, font, size, TextColor, Vector2.zero, sizeDelta);
        labelText.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static Image CreateImage(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
