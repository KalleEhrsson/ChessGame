using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChessPauseMenuUI : MonoBehaviour
{
    Canvas canvas;
    GameObject overlay;
    TMP_Text statusText;
    Button resumeButton;
    Button aiConsoleButton;
    Button sandboxButton;
    Button restartButton;
    Button resignButton;

    ChessPauseManager pauseManager;
    ChessAiRoundConsole aiConsole;
    ChessDevSandboxController sandbox;
    ChessResignUiController resignUi;

    public bool IsVisible => overlay != null && overlay.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRuntimeInstance()
    {
        if (FindFirstObjectByType<ChessPauseMenuUI>() != null)
        {
            return;
        }

        GameObject host = new("ChessPauseMenuUI");
        DontDestroyOnLoad(host);
        host.AddComponent<ChessPauseMenuUI>();
    }

    void Awake()
    {
        pauseManager = ChessPauseManager.GetOrCreate();
        aiConsole = ChessAiRoundConsole.GetOrCreate();
        sandbox = ChessDevSandboxController.Instance;
        resignUi = ChessResignUiController.GetOrCreate();
        EnsureUi();
    }

    void Update()
    {
        Refresh();
    }

    void EnsureUi()
    {
        if (EventSystem.current == null)
        {
            GameObject es = new("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        Canvas existing = FindFirstObjectByType<Canvas>();
        if (existing == null)
        {
            GameObject canvasObject = new("ChessRuntimeCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }
        else
        {
            canvas = existing;
        }

        overlay = new GameObject("PauseOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        RectTransform oRect = overlay.GetComponent<RectTransform>();
        oRect.anchorMin = Vector2.zero;
        oRect.anchorMax = Vector2.one;
        oRect.offsetMin = Vector2.zero;
        oRect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        GameObject panel = new("PausePanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(overlay.transform, false);
        RectTransform pRect = panel.GetComponent<RectTransform>();
        pRect.anchorMin = pRect.anchorMax = new Vector2(0.5f, 0.5f);
        pRect.sizeDelta = new Vector2(420f, 0f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        VerticalLayoutGroup v = panel.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(20, 20, 20, 20);
        v.spacing = 8;
        v.childControlHeight = false;
        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateText(panel.transform, "Paused", 42);
        statusText = CreateText(panel.transform, string.Empty, 24);
        resumeButton = CreateButton(panel.transform, "Resume", () => pauseManager.Resume());
        aiConsoleButton = CreateButton(panel.transform, "AI / Stockfish Console", () => aiConsole.SetVisible(true));
        sandboxButton = CreateButton(panel.transform, "Sandbox Tools", () => { if (sandbox != null) sandbox.SetOpenFromPauseMenu(true); });
        restartButton = CreateButton(panel.transform, "Restart / New Game", () => { ChessBoard.Instance?.RestartMatch(); pauseManager.ResetPauseState(); });
        resignButton = CreateButton(panel.transform, "Resign", () => resignUi.OpenConfirmFromPauseMenu());

        overlay.SetActive(false);
    }

    void Refresh()
    {
        overlay.SetActive(pauseManager.IsPauseRequested);
        if (!pauseManager.IsPauseRequested)
        {
            aiConsole?.SetVisible(false);
            sandbox?.SetOpenFromPauseMenu(false);
            return;
        }

        bool fullyPaused = pauseManager.IsPaused;
        statusText.gameObject.SetActive(!fullyPaused);
        statusText.text = fullyPaused ? string.Empty : "Finishing current move...";
        resumeButton.GetComponentInChildren<TMP_Text>().text = fullyPaused ? "Resume" : "Cancel Pause";

        aiConsoleButton.interactable = fullyPaused;
        sandboxButton.interactable = fullyPaused;
        restartButton.interactable = fullyPaused;
        resignButton.interactable = fullyPaused;
    }

    static TMP_Text CreateText(Transform parent, string value, float size)
    {
        GameObject go = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 42f);
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        return text;
    }

    Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new(label.Replace(" ", string.Empty), typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 44f);
        go.GetComponent<Image>().color = new Color(0.86f, 0.86f, 0.9f, 1f);
        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);
        TMP_Text labelText = CreateText(go.transform, label, 24f);
        labelText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        return button;
    }
}
