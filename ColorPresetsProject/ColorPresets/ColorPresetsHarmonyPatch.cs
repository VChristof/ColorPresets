using SpaceWarp.API.Mods;
using HarmonyLib;
using KSP.UI.Binding;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using KSP.OAB;
using Newtonsoft.Json;
using BepInEx;
using SpaceWarp;

namespace ColorPresets;

[BepInPlugin(ModGuid, ModName, ModVer)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class ColorPresetsMainMod : BaseSpaceWarpPlugin
{
    public const string ModGuid = "com.vchristof.color_presets";
    public const string ModName = "color_presets";
    public const string ModVer = "0.1.1";
    private static ColorPresetsMainMod Instance { get; set; }

    public override void OnInitialized()
    {
        base.OnInitialized();
        Instance = this;
        Harmony.CreateAndPatchAll(typeof(ColorPresetsMainMod).Assembly, ModGuid);
        Logger.LogInfo("Color chooser mod is initialized");
    }
}

[HarmonyPatch(typeof(ObjectAssemblyColorPicker), "Start")]
class ColorPresetsHarmonyPatch
{
    private const string extraColors = "Extra Colors Buttons";
    private const string extraColor = "Extra Color-";
    private const string agencyColorsButtonsName = "Agency Colors Buttons";
    private const string grpAgencyColorsName = "GRP-Agency Colors";
    private const string grpSelectedColorsName = "GRP-Selected Colors";
    private const string BTNRestoreAgencyColorsName = "BTN-RestoreAgencyColors";
    private const string BTNSetAgencyColorsName = "BTN-SetAgencyColors";
    private const string BaseColorDefaultButtonName = "BaseColorDefaultButton";
    private const string DetailColorDefaultButtonName = "DetailColorDefaultButton";
    private const string PartsManagerELEPropertyNameName = "PartsManager-ELE-Property Name";
    private const string GRPColorPreviewHorName = "GRP-ColorPreview-Hor";
    private const string GRPBaseColorVertName = "GRP-BaseColor-Vert";
    private const string BaseColorPreviewButtonName = "BaseColorPreviewButton";
    private const string GRPDetailColorVertName = "GRP-DetailColor-Vert";
    private const string DetailColorPreviewButtonName = "DetailColorPreviewButton";
    private const string BaseColorDefaultImageName = "BaseColorDefaultImage";
    private const string DetailColorDefaultImageName = "DetailColorDefaultImage";

    [Serializable]
    public class ColorList
    {
        [SerializeField]
        public List<ColorPair> colorPresets = new();
    }

    [Serializable]
    public class ColorPair
    {
        [SerializeField]
        public MyColor baseColor, accentColor;
        public ColorPair(Color baseColor, Color accentColor)
        {
            this.baseColor = new MyColor(baseColor);
            this.accentColor = new MyColor(accentColor);
        }
    }
    [Serializable]
    public class MyColor
    {
        public float r, g, b, a;
        public MyColor()
        {
        }

        public MyColor(Color color)
        {
            this.r = color.r;
            this.g = color.g;
            this.b = color.b;
            this.a = color.a;
        }

        public Color GetColor()
        {
            return new Color(r, g, b, a);
        }
    }

    private static readonly Dictionary<Guid, ColorPair> colorPresets = new();

    private static Transform GetGrpBody(ObjectAssemblyColorPicker instance)
    {
        return instance.transform
                    .FindChildEx("UIPanel")
                    .FindChildEx("GRP-Body");
    }

    [HarmonyPostfix]
    public static void Postfix(ObjectAssemblyColorPicker __instance)
    {
        try
        {
            var body = GetGrpBody(__instance);

            SquishActiveColors(body);
            SquishAgencyButtons(body);
            AddNewOptions(__instance);
            __instance.GetComponent<RectTransform>().sizeDelta = __instance.GetComponent<RectTransform>().sizeDelta + new Vector2(0, 60);

            _ = DelayLoadForUIToBuild(__instance);
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }

    private static async Task DelayLoadForUIToBuild(ObjectAssemblyColorPicker __instance)
    {
        await Task.Delay(1000);
        try
        {
            LoadData(__instance);
        } catch(Exception e)
        {
            Debug.LogError(e.Message);
        }
    }

    private static void AddNewOptions(ObjectAssemblyColorPicker instance)
    {
        Transform body = GetGrpBody(instance);
        Transform agencyColorsButtons = body.FindChildEx(agencyColorsButtonsName);
        GameObject newOption = GameObject.Instantiate(agencyColorsButtons.gameObject, body);
        MoveBy(newOption.transform, new Vector2(0, -70));
        Object.Destroy(newOption.transform.FindChildEx("Div").gameObject);

        Transform optionsName = newOption.transform.FindChildEx(PartsManagerELEPropertyNameName);
        optionsName.GetComponent<TextMeshProUGUI>().SetText("Extra Colors");

        Transform baseColor = newOption.transform.FindChildEx(BaseColorDefaultButtonName);
        Transform.Destroy(baseColor.gameObject);

        Transform accentColor = newOption.transform.FindChildEx(DetailColorDefaultButtonName);
        Transform.Destroy(accentColor.gameObject);

        Transform grpAgencyColors = newOption.transform.FindChildEx(grpAgencyColorsName);
        LayoutElement grpAgencyColorsLayoutElement = grpAgencyColors.GetComponent<LayoutElement>();
        grpAgencyColorsLayoutElement.preferredHeight += 10;
        grpAgencyColorsLayoutElement.minHeight += 10;


        Transform useButton = grpAgencyColors.FindChildEx(BTNRestoreAgencyColorsName);
        GameObject.Destroy(useButton.gameObject);

        Transform setButton = grpAgencyColors.FindChildEx(BTNSetAgencyColorsName);
        setButton.GetComponentInChildren<TextMeshProUGUI>().SetText("+");
        ButtonExtended setButtonExtended = setButton.GetComponent<ButtonExtended>();
        setButtonExtended.onClick.RemoveAllListeners();
        setButtonExtended.onClick.AddListener(() => SaveNewColor(instance));
        newOption.name = extraColors;

        GameObject extraColorHolder = new GameObject("Extra Color View");
        extraColorHolder.transform.SetParent(newOption.transform);
        RectTransform extraColorHolderRectTransfrom = extraColorHolder.AddComponent<RectTransform>();
        extraColorHolderRectTransfrom.anchorMin = Vector2.zero;
        extraColorHolderRectTransfrom.anchorMax = Vector2.one;
        extraColorHolderRectTransfrom.sizeDelta = Vector2.zero;
        extraColorHolderRectTransfrom.anchoredPosition = Vector2.zero;
        ScrollRect scrollRect = extraColorHolder.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.inertia = false;

        LayoutElement layoutElement = extraColorHolder.AddComponent<LayoutElement>();
        layoutElement.minHeight = 150;
        layoutElement.preferredHeight = 150;
        layoutElement.flexibleHeight = 1;
        layoutElement.flexibleWidth = 1;

        GameObject extraColorHolderViewport = new GameObject("Viewport");
        extraColorHolderViewport.transform.SetParent(extraColorHolder.transform);
        RectTransform extraColorHolderViewportRectTransfrom = extraColorHolderViewport.AddComponent<RectTransform>();
        extraColorHolderViewportRectTransfrom.anchorMin = Vector2.zero;
        extraColorHolderViewportRectTransfrom.anchorMax = Vector2.one;
        extraColorHolderViewportRectTransfrom.sizeDelta = Vector2.zero;
        extraColorHolderViewportRectTransfrom.anchoredPosition = Vector2.zero;
        extraColorHolderViewport.AddComponent<Image>();
        Mask mask = extraColorHolderViewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        scrollRect.viewport = (RectTransform)extraColorHolderViewport.transform;

        GameObject extraColorHolderContent = new GameObject("Content");
        extraColorHolderContent.transform.SetParent(extraColorHolderViewport.transform);
        RectTransform extraColorHolderContentRectTransfrom = extraColorHolderContent.AddComponent<RectTransform>();
        extraColorHolderContentRectTransfrom.anchorMin = Vector2.zero;
        extraColorHolderContentRectTransfrom.anchorMax = Vector2.one;
        extraColorHolderContentRectTransfrom.sizeDelta = Vector2.zero;
        extraColorHolderContentRectTransfrom.anchoredPosition = Vector2.zero;
        extraColorHolderContentRectTransfrom.pivot = Vector2.one;
        VerticalLayoutGroup verticalLayoutGroup = extraColorHolderContent.AddComponent<VerticalLayoutGroup>();
        verticalLayoutGroup.childControlHeight = false;
        verticalLayoutGroup.childControlWidth = false;
        verticalLayoutGroup.childForceExpandHeight = false;
        verticalLayoutGroup.childForceExpandWidth = false;
        verticalLayoutGroup.childScaleHeight = false;
        verticalLayoutGroup.childScaleWidth = false;
        verticalLayoutGroup.childAlignment = TextAnchor.UpperCenter;
        verticalLayoutGroup.spacing = 5.0f;
        ContentSizeFitter extraColorHolderContentSizeFitter = extraColorHolderContent.AddComponent<ContentSizeFitter>();
        extraColorHolderContentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        extraColorHolderContentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = (RectTransform)extraColorHolderContent.transform;
    }

    private static Transform GetExtraColorViewContent(ObjectAssemblyColorPicker instance)
    {
        Transform body = GetGrpBody(instance);
        Transform otherColorsButtons = body.FindChildEx(extraColors);
        return otherColorsButtons
            .FindChildEx("Extra Color View")
            .FindChildEx("Viewport")
            .FindChildEx("Content");
    }

    private static void SaveNewColor(ObjectAssemblyColorPicker instance)
    {
        Transform body = GetGrpBody(instance);
        Transform baseColorPreviewButton = body.FindChildEx(grpSelectedColorsName)
            .FindChildEx(GRPColorPreviewHorName)
            .FindChildEx(GRPBaseColorVertName)
            .FindChildEx(BaseColorPreviewButtonName);
        UIValue_ReadColor_ImageTint baseImage = baseColorPreviewButton.GetComponentInChildren<UIValue_ReadColor_ImageTint>();
        Transform detailColorPreviewButton = body.FindChildEx(grpSelectedColorsName)
            .FindChildEx(GRPColorPreviewHorName)
            .FindChildEx(GRPDetailColorVertName)
            .FindChildEx(DetailColorPreviewButtonName);
        UIValue_ReadColor_ImageTint accentImage = detailColorPreviewButton.GetComponentInChildren<UIValue_ReadColor_ImageTint>();
        SaveNewColor(instance, baseImage.GetValue(), accentImage.GetValue(), true);
    }


    private static void SaveNewColor(ObjectAssemblyColorPicker instance, Color baseColorToAdd, Color accentColorToAdd, bool saveData)
    {
        Transform body = GetGrpBody(instance);
        Transform agencyColorsButtons = body.FindChildEx(agencyColorsButtonsName);
        Transform grpAgencyColors = agencyColorsButtons.FindChildEx(grpAgencyColorsName);
        Transform extraColorViewContent = GetExtraColorViewContent(instance);
        GameObject newGrpAgencyColor = GameObject.Instantiate(grpAgencyColors.gameObject, extraColorViewContent);
        MoveBy(newGrpAgencyColor.transform, new Vector2(0, -70 * (1 + colorPresets.Count)));

        Transform baseColor = newGrpAgencyColor.transform.FindChildEx(BaseColorDefaultButtonName);
        ButtonExtended baseButtonExtended = baseColor.GetComponent<ButtonExtended>();
        baseButtonExtended.onClick.RemoveAllListeners();
        Transform baseColorDefaultImage = baseColor.FindChildEx(BaseColorDefaultImageName);
        GameObject.Destroy(baseColorDefaultImage.GetComponent<UIValue_ReadColor_ImageTint>());
        Image myBaseImage = baseColorDefaultImage.GetComponent<Image>();
        myBaseImage.color = baseColorToAdd;
        myBaseImage.SetAllDirty();
        baseColorDefaultImage.gameObject.SetActive(false);
        baseColorDefaultImage.gameObject.SetActive(true);

        Transform accentColor = newGrpAgencyColor.transform.FindChildEx(DetailColorDefaultButtonName);
        ButtonExtended accentButtonExtended = accentColor.GetComponent<ButtonExtended>();
        accentButtonExtended.onClick.RemoveAllListeners();
        Transform detailColorDefaultImage = accentColor.FindChildEx(DetailColorDefaultImageName);
        GameObject.Destroy(detailColorDefaultImage.GetComponent<UIValue_ReadColor_ImageTint>());
        Image myAccentImage = detailColorDefaultImage.GetComponent<Image>();
        myAccentImage.color = accentColorToAdd;
        detailColorDefaultImage.gameObject.SetActive(false);
        detailColorDefaultImage.gameObject.SetActive(true);

        Guid key = Guid.NewGuid();
        colorPresets.Add(key, new ColorPair(baseColorToAdd, accentColorToAdd));

        newGrpAgencyColor.name = extraColor + key.ToString();

        var useButton = newGrpAgencyColor.transform.FindChildEx(BTNRestoreAgencyColorsName);
        useButton.GetComponentInChildren<TextMeshProUGUI>().SetText("USE");
        ButtonExtended useButtonExtended = useButton.GetComponent<ButtonExtended>();
        useButtonExtended.onClick.RemoveAllListeners();
        useButtonExtended.onClick.AddListener(() => UseColor(instance, key));

        var setButton = newGrpAgencyColor.transform.FindChildEx(BTNSetAgencyColorsName);
        setButton.GetComponentInChildren<TextMeshProUGUI>().SetText("DEL");
        ButtonExtended setButtonExtended = setButton.GetComponent<ButtonExtended>();
        setButtonExtended.onClick.RemoveAllListeners();
        setButtonExtended.onClick.AddListener(() => DeleteColor(instance, key));

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)extraColorViewContent.transform);
        if (saveData)
        {
            SaveData();
        }
    }

    private static void UseColor(ObjectAssemblyColorPicker instance, Guid key)
    {
        Transform body = GetGrpBody(instance);
        instance.BaseColor.color = colorPresets[key].baseColor.GetColor();
        instance.AccentColor.color = colorPresets[key].accentColor.GetColor();

        Transform baseColorPreviewButton = body.FindChildEx(grpSelectedColorsName)
            .FindChildEx(GRPColorPreviewHorName)
            .FindChildEx(GRPBaseColorVertName)
            .FindChildEx(BaseColorPreviewButtonName);
        UIValue_ReadColor_ImageTint baseImage = baseColorPreviewButton.GetComponentInChildren<UIValue_ReadColor_ImageTint>();
        baseImage.SetValue(instance.BaseColor.color);
        baseImage.RedrawValue(true);

        Transform detailColorPreviewButton = body.FindChildEx(grpSelectedColorsName)
            .FindChildEx(GRPColorPreviewHorName)
            .FindChildEx(GRPDetailColorVertName)
            .FindChildEx(DetailColorPreviewButtonName);
        UIValue_ReadColor_ImageTint accentImage = detailColorPreviewButton.GetComponentInChildren<UIValue_ReadColor_ImageTint>();
        accentImage.SetValue(instance.AccentColor.color);
        accentImage.RedrawValue(true);
        instance.RedrawValue(true, true);
    }

    private static void DeleteColor(ObjectAssemblyColorPicker instance, Guid key)
    {
        Transform body = GetGrpBody(instance);
        Transform extraColorViewContent = GetExtraColorViewContent(instance);
        Transform toDelete = extraColorViewContent.FindChildEx(extraColor + key.ToString());
        GameObject.Destroy(toDelete.gameObject);
        colorPresets.Remove(key);
        SaveData();
    }

    private static void SaveData()
    {
        ColorList colorList = new ();
        colorList.colorPresets = colorPresets.Values.ToList();
        string presetsString = JsonConvert.SerializeObject(colorList, Formatting.Indented);
        File.WriteAllText(Application.persistentDataPath + "/ColorPresets.json", presetsString);
    }

    private static void LoadData(ObjectAssemblyColorPicker instance)
    {
        string filePath = Path.Combine(Application.persistentDataPath, "ColorPresets.json");
        if(File.Exists(filePath))
        {
            string dataString = File.ReadAllText(filePath);
            ColorList colorList = JsonConvert.DeserializeObject<ColorList>(dataString);
            foreach (ColorPair pair in colorList.colorPresets)
            {
                SaveNewColor(instance, pair.baseColor.GetColor(), pair.accentColor.GetColor(), false);
            }
        }
    }

    private static void SquishActiveColors(Transform body)
    {
        var squishBy = new Vector2(0, 80);
        var activeColorContainer = body
            .FindChildEx(grpSelectedColorsName)
            .FindChildEx(GRPColorPreviewHorName);
        ((RectTransform)activeColorContainer).sizeDelta -= squishBy;

        MoveBy(body.FindChildEx("GRP-HueSlider"), squishBy);
        MoveBy(body.FindChildEx("GRP-ColorPicker"), squishBy);
        MoveBy(body.FindChildEx(agencyColorsButtonsName), squishBy);
    }

    private static void SquishAgencyButtons(Transform body)
    {
        var agencyColorContainer = body.FindChildEx(agencyColorsButtonsName);

        // delete extra spacing
        Object.Destroy(agencyColorContainer.FindChildEx("Div").gameObject);

        var horizontalContainer = agencyColorContainer.FindChildEx(grpAgencyColorsName);

        // move and rename restore button (use)
        var useButton = agencyColorContainer.FindChildEx(BTNRestoreAgencyColorsName);
        useButton.SetParent(horizontalContainer);
        useButton.GetComponentInChildren<TextMeshProUGUI>().SetText("USE");


        // move and rename set button
        var setButton = agencyColorContainer.FindChildEx(BTNSetAgencyColorsName);
        setButton.SetParent(horizontalContainer);
        setButton.GetComponentInChildren<TextMeshProUGUI>().SetText("SET");
    }

    private static void MoveBy(Transform rect, Vector2 delta)
    {
        ((RectTransform)rect).anchoredPosition += delta;
    }
}