using System.IO;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TestPlayHudRuntime : MonoBehaviour
{
    [Header("References")]
    public TestPlayController controller;
    public Font font;

    [Header("Layout")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public Vector2 statusPanelSize = new Vector2(340f, 90f);
    public Vector2 gaugePanelSize = new Vector2(620f, 100f);
    public Vector2 screenMargin = new Vector2(24f, 24f);

    [Header("Colors")]
    public Color panelColor = new Color(0.015f, 0.025f, 0.045f, 0.84f);
    public Color gaugeBackgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.9f);
    public Color generatorColor = new Color(0.18f, 0.9f, 0.42f, 0.96f);
    public Color energyColor = new Color(0.1f, 0.68f, 1f, 0.96f);
    public Color hpColor = new Color(1f, 0.38f, 0.34f, 1f);

    GameObject canvasObject;
    RectTransform generatorFill;
    RectTransform energyFill;
    Text mechaNameText;
    Text hpText;
    Text generatorValueText;
    Text energyValueText;
    bool hudVisible;
    float displayedHP = float.NaN;
    float displayedGenerator = float.NaN;
    float displayedMaximumGenerator = float.NaN;
    float displayedEnergy = float.NaN;
    float displayedMaximumEnergy = float.NaN;

    public void Bind(TestPlayController source)
    {
        controller = source;
    }

    public void ShowHud()
    {
        EnsureVisuals();
        hudVisible = true;
        canvasObject.SetActive(true);
        mechaNameText.text = ResolveMechaName(controller);
        InvalidateDisplayedValues();
        RefreshHud();
    }

    public void HideHud()
    {
        hudVisible = false;
        if (canvasObject != null)
            canvasObject.SetActive(false);
    }

    public void RefreshHud()
    {
        if (controller == null || canvasObject == null)
            return;

        if (!Mathf.Approximately(displayedHP, controller.currentHP))
        {
            displayedHP = controller.currentHP;
            hpText.text = "HP  " + Mathf.CeilToInt(Mathf.Max(0f, controller.currentHP));
        }
        UpdateGauge(
            generatorFill,
            generatorValueText,
            controller.currentEnergy,
            controller.maximumEnergy,
            ref displayedGenerator,
            ref displayedMaximumGenerator);
        UpdateGauge(
            energyFill,
            energyValueText,
            controller.currentAuxiliaryEnergy,
            controller.maximumAuxiliaryEnergy,
            ref displayedEnergy,
            ref displayedMaximumEnergy);
    }

    void InvalidateDisplayedValues()
    {
        displayedHP = float.NaN;
        displayedGenerator = float.NaN;
        displayedMaximumGenerator = float.NaN;
        displayedEnergy = float.NaN;
        displayedMaximumEnergy = float.NaN;
    }

    void LateUpdate()
    {
        if (!hudVisible || controller == null)
            return;

        if (!controller.playModeActive)
        {
            HideHud();
            return;
        }

        RefreshHud();
    }

    void OnDestroy()
    {
        if (canvasObject == null)
            return;

        if (Application.isPlaying)
            Destroy(canvasObject);
        else
            DestroyImmediate(canvasObject);
    }

    public static float CalculateFillAmount(float current, float maximum)
    {
        if (maximum <= 0f)
            return 0f;

        return Mathf.Clamp01(current / maximum);
    }

    public static string ResolveMechaName(TestPlayController source)
    {
        if (source == null)
            return "機体名不明";

        SptRuntimeData data = source.sptSource != null ? source.sptSource.LastSptData : null;
        string displayName = data != null ? CleanDisplayName(data.Name) : "";
        if (!string.IsNullOrEmpty(displayName))
            return displayName;

        displayName = data != null ? CleanDisplayName(data.NameEng) : "";
        if (!string.IsNullOrEmpty(displayName))
            return displayName;

        if (source.robo != null && !string.IsNullOrEmpty(source.robo.folder))
        {
            string folder = source.robo.folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            displayName = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(displayName))
                return displayName;
        }

        if (source.robo != null && source.robo.root != null)
            return source.robo.root.name;

        return "機体名不明";
    }

    static string CleanDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim().Trim('"', '\'');
    }

    void EnsureVisuals()
    {
        if (canvasObject != null)
            return;

        int uiLayer = LayerMask.NameToLayer("UI");
        canvasObject = new GameObject(
            "TestPlayHUDCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.layer = uiLayer >= 0 ? uiLayer : gameObject.layer;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Font resolvedFont = ResolveFont();
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        BuildStatusPanel(canvasRect, resolvedFont);
        BuildGaugePanel(canvasRect, resolvedFont);
    }

    Font ResolveFont()
    {
        if (font != null)
            return font;

        Text[] existingTexts = FindObjectsByType<Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < existingTexts.Length; i++)
        {
            Font candidate = existingTexts[i].font;
            if (candidate != null && candidate.name == "LegacyRuntime")
            {
                font = candidate;
                return font;
            }
        }

        for (int i = 0; i < existingTexts.Length; i++)
        {
            if (existingTexts[i].font != null)
            {
                font = existingTexts[i].font;
                return font;
            }
        }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }

    void BuildStatusPanel(RectTransform canvasRect, Font resolvedFont)
    {
        RectTransform panel = CreateImage("StatusPanel", canvasRect, panelColor).rectTransform;
        SetAnchoredRect(
            panel,
            Vector2.zero,
            Vector2.zero,
            screenMargin,
            statusPanelSize);

        mechaNameText = CreateText(
            "MechaName",
            panel,
            resolvedFont,
            28,
            Color.white,
            TextAnchor.MiddleLeft);
        SetStretchRect(
            mechaNameText.rectTransform,
            new Vector2(0f, 0.48f),
            Vector2.one,
            new Vector2(14f, 2f),
            new Vector2(-14f, -4f));

        hpText = CreateText(
            "RemainingHP",
            panel,
            resolvedFont,
            28,
            hpColor,
            TextAnchor.MiddleLeft);
        SetStretchRect(
            hpText.rectTransform,
            Vector2.zero,
            new Vector2(1f, 0.48f),
            new Vector2(14f, 4f),
            new Vector2(-14f, -2f));
    }

    void BuildGaugePanel(RectTransform canvasRect, Font resolvedFont)
    {
        RectTransform panel = CreateImage("GaugePanel", canvasRect, panelColor).rectTransform;
        SetAnchoredRect(
            panel,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, screenMargin.y),
            gaugePanelSize);

        CreateGaugeRow(
            panel,
            "Generator",
            new Vector2(0f, 0.5f),
            Vector2.one,
            generatorColor,
            resolvedFont,
            out generatorFill,
            out generatorValueText);
        CreateGaugeRow(
            panel,
            "Energy",
            Vector2.zero,
            new Vector2(1f, 0.5f),
            energyColor,
            resolvedFont,
            out energyFill,
            out energyValueText);
    }

    void CreateGaugeRow(
        RectTransform parent,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color fillColor,
        Font resolvedFont,
        out RectTransform fill,
        out Text valueText)
    {
        RectTransform row = CreateRect(label + "Row", parent);
        SetStretchRect(row, anchorMin, anchorMax, new Vector2(14f, 5f), new Vector2(-14f, -5f));

        Text labelText = CreateText(
            label + "Label",
            row,
            resolvedFont,
            23,
            Color.white,
            TextAnchor.MiddleLeft);
        labelText.text = label;
        SetStretchRect(
            labelText.rectTransform,
            Vector2.zero,
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(112f, 0f));

        RectTransform bar = CreateImage(label + "Bar", row, gaugeBackgroundColor).rectTransform;
        SetStretchRect(bar, Vector2.zero, Vector2.one, new Vector2(116f, 5f), new Vector2(0f, -5f));

        fill = CreateImage(label + "Fill", bar, fillColor).rectTransform;
        SetStretchRect(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        valueText = CreateText(
            label + "Value",
            bar,
            resolvedFont,
            20,
            Color.white,
            TextAnchor.MiddleCenter);
        SetStretchRect(valueText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static Image CreateImage(string objectName, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Text CreateText(
        string objectName,
        Transform parent,
        Font resolvedFont,
        int fontSize,
        Color color,
        TextAnchor alignment)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = resolvedFont;
        text.fontSize = fontSize;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(fontSize * 0.65f));
        text.resizeTextMaxSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(0, 0, 0, 220);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
        return text;
    }

    static void SetAnchoredRect(
        RectTransform rect,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    static void SetStretchRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    static void UpdateGauge(
        RectTransform fill,
        Text valueText,
        float current,
        float maximum,
        ref float displayedCurrent,
        ref float displayedMaximum)
    {
        if (Mathf.Approximately(displayedCurrent, current) &&
            Mathf.Approximately(displayedMaximum, maximum))
            return;

        displayedCurrent = current;
        displayedMaximum = maximum;
        float amount = CalculateFillAmount(current, maximum);
        fill.anchorMax = new Vector2(amount, 1f);
        fill.offsetMax = Vector2.zero;
        valueText.text = Mathf.CeilToInt(Mathf.Max(0f, current)) +
            " / " + Mathf.CeilToInt(Mathf.Max(0f, maximum));
    }
}
