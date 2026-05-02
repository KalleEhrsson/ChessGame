using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChessDevSandboxPanelView : MonoBehaviour
{
    Canvas rootCanvas;
    Button resetBoardButton;
    Button clearBoardButton;
    Button exportFenButton;
    Button sideWhiteButton;
    Button sideBlackButton;
    Toggle aiToggle;
    Button modeOffButton;
    Button modePlaceButton;
    Button modeRemoveButton;
    Button modeMoveButton;
    Button teamCycleButton;
    TMP_Text teamCycleLabel;
    Button pieceCycleButton;
    TMP_Text pieceCycleLabel;
    TMP_Text presetNameLabel;
    Button presetPrevButton;
    Button presetNextButton;
    Button presetLoadButton;
    TMP_InputField fenInput;
    Button importFenButton;

    ChessDevSandboxController controller;

    void Awake()
    {
        controller = ChessDevSandboxController.Instance;
        EnsureEventSystem();
        BuildIfNeeded();
        BindEvents();
        Refresh();
    }

    void Update() => Refresh();

    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject eventSystemObject = new("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    void BuildIfNeeded()
    {
        rootCanvas = GetComponent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = gameObject.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 1000;
            gameObject.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }
        if (transform.childCount > 0) return;

        RectTransform panel = CreateRect("Panel", transform);
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(20f, -20f);
        panel.sizeDelta = new Vector2(520f, 0f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
        var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(12, 12, 12, 12); v.spacing = 8f; v.childControlHeight = false;
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        resetBoardButton = AddButton(panel, "Reset Board", out _);
        clearBoardButton = AddButton(panel, "Clear Board", out _);
        exportFenButton = AddButton(panel, "Export FEN", out _);
        sideWhiteButton = AddButton(panel, "Turn: White", out _);
        sideBlackButton = AddButton(panel, "Turn: Black", out _);
        aiToggle = AddToggle(panel, "AI Enabled (Both Teams)");
        modeOffButton = AddButton(panel, "Mode: Off", out _);
        modePlaceButton = AddButton(panel, "Mode: Place", out _);
        modeRemoveButton = AddButton(panel, "Mode: Remove", out _);
        modeMoveButton = AddButton(panel, "Mode: Move", out _);
        teamCycleButton = AddButton(panel, "Side: White", out teamCycleLabel);
        pieceCycleButton = AddButton(panel, "Piece: Queen", out pieceCycleLabel);

        RectTransform row = CreateRect("PresetRow", panel);
        row.gameObject.AddComponent<HorizontalLayoutGroup>().spacing = 6f;
        presetPrevButton = AddButton(row, "<", out _);
        var presetLabelObj = CreateText("PresetName", row, "Preset");
        presetNameLabel = presetLabelObj;
        presetNextButton = AddButton(row, ">", out _);
        presetLoadButton = AddButton(row, "Load", out _);

        fenInput = AddInput(panel);
        importFenButton = AddButton(panel, "Import FEN", out _);
    }

    void BindEvents()
    {
        resetBoardButton.onClick.AddListener(() => controller.ResetBoard());
        clearBoardButton.onClick.AddListener(() => controller.ClearBoard());
        exportFenButton.onClick.AddListener(() => controller.ExportFen());
        sideWhiteButton.onClick.AddListener(() => controller.SetSideToMove(PieceTeam.White));
        sideBlackButton.onClick.AddListener(() => controller.SetSideToMove(PieceTeam.Black));
        aiToggle.onValueChanged.AddListener(controller.ToggleAi);
        modeOffButton.onClick.AddListener(() => controller.SetMode(ChessDevSandboxController.SandboxMode.None));
        modePlaceButton.onClick.AddListener(() => controller.SetMode(ChessDevSandboxController.SandboxMode.Place));
        modeRemoveButton.onClick.AddListener(() => controller.SetMode(ChessDevSandboxController.SandboxMode.Remove));
        modeMoveButton.onClick.AddListener(() => controller.SetMode(ChessDevSandboxController.SandboxMode.Move));
        teamCycleButton.onClick.AddListener(() => controller.SetSelectedTeam(controller.SelectedTeam == PieceTeam.White ? PieceTeam.Black : PieceTeam.White));
        pieceCycleButton.onClick.AddListener(() => { int n=((int)controller.SelectedPieceType+1)%Enum.GetValues(typeof(PieceType)).Length; controller.SetSelectedPieceType((PieceType)n); });
        presetPrevButton.onClick.AddListener(() => controller.SetPresetIndex(controller.PresetIndex - 1));
        presetNextButton.onClick.AddListener(() => controller.SetPresetIndex(controller.PresetIndex + 1));
        presetLoadButton.onClick.AddListener(() => controller.LoadSelectedPreset());
        fenInput.onValueChanged.AddListener(controller.SetFenBuffer);
        importFenButton.onClick.AddListener(() => controller.ImportFen());
    }

    void Refresh()
    {
        if (controller == null) controller = ChessDevSandboxController.Instance;
        if (controller == null) return;
        rootCanvas.enabled = controller.IsOpen;
        if (!controller.IsOpen) return;
        aiToggle.SetIsOnWithoutNotify(controller.AiEnabled);
        bool placeMode = controller.Mode == ChessDevSandboxController.SandboxMode.Place;
        teamCycleButton.gameObject.SetActive(placeMode);
        pieceCycleButton.gameObject.SetActive(placeMode);
        teamCycleLabel.text = $"Side: {controller.SelectedTeam}";
        pieceCycleLabel.text = $"Piece: {controller.SelectedPieceType}";
        if (fenInput.text != controller.FenBuffer) fenInput.SetTextWithoutNotify(controller.FenBuffer);
        var presets = controller.Presets;
        bool hasPresets = presets.Count > 0;
        presetNameLabel.text = hasPresets ? presets[controller.PresetIndex].Name : "No presets available";
    }

    static RectTransform CreateRect(string name, Transform parent){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return go.GetComponent<RectTransform>();}
    static TMP_Text CreateText(string name, Transform parent, string text){var r=CreateRect(name,parent);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.text=text;t.fontSize=24;return t;}
    static Button AddButton(Transform parent,string label,out TMP_Text text){var r=CreateRect(label.Replace(" ","")+"Button",parent);r.sizeDelta=new Vector2(0,36);var img=r.gameObject.AddComponent<Image>();img.color=new Color(.85f,.85f,.9f,1f);var b=r.gameObject.AddComponent<Button>();text=CreateText("Label",r,label);text.alignment=TextAlignmentOptions.Center;var tr=text.rectTransform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;return b;}
    static Toggle AddToggle(Transform parent,string label){var tGo=CreateRect("AIToggle",parent);var tg=tGo.gameObject.AddComponent<Toggle>();var bg=CreateRect("Background",tGo);bg.sizeDelta=new Vector2(20,20);bg.gameObject.AddComponent<Image>().color=Color.white;var check=CreateRect("Checkmark",bg);check.sizeDelta=new Vector2(14,14);var checkImage=check.gameObject.AddComponent<Image>();checkImage.color=Color.black;tg.graphic=checkImage;tg.targetGraphic=bg.GetComponent<Image>();var txt=CreateText("Label",tGo,label);txt.fontSize=20;txt.rectTransform.anchoredPosition=new Vector2(70,0);return tg;}
    static TMP_InputField AddInput(Transform parent){var r=CreateRect("FenInput",parent);r.sizeDelta=new Vector2(0,100);var img=r.gameObject.AddComponent<Image>();img.color=Color.white;var text=CreateText("Text",r,"");text.fontSize=18;text.color=Color.black;text.rectTransform.offsetMin=new Vector2(8,8);text.rectTransform.offsetMax=new Vector2(-8,-8);var placeholder=CreateText("Placeholder",r,"Enter FEN");placeholder.fontSize=18;placeholder.color=Color.gray;placeholder.rectTransform.offsetMin=new Vector2(8,8);placeholder.rectTransform.offsetMax=new Vector2(-8,-8);var input=r.gameObject.AddComponent<TMP_InputField>();input.textViewport=r;input.textComponent=(TextMeshProUGUI)text;input.placeholder=placeholder;input.lineType=TMP_InputField.LineType.MultiLineNewline;return input;}
}
