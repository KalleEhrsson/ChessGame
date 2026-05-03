using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class ChessSceneSetupTool
{
    const string SystemsRootName = "ChessGameSystems";
    static readonly string[] OverlayRoots =
    {
        "GameplayHudRoot", "PauseMenuRoot", "WinScreenRoot", "ResignPopupRoot", "DevMenuRoot", "DebugMenuRoot",
        "BoardPresetsRoot", "StockfishConsoleRoot", "PromotionRoot", "PauseDebugOverlayRoot"
    };

    [MenuItem("ChessGame/Setup/Create Or Update Scene Managers And UI")]
    static void CreateOrUpdateSceneManagersAndUi()
    {
        List<string> log = new();
        Transform systemsRoot = GetOrCreateRoot(SystemsRootName, log);
        EnsureManager<ChessPauseManager>(systemsRoot, "ChessPauseManager", log);
        EnsureManager<ChessGameStateController>(systemsRoot, "ChessGameStateController", log);
        EnsureManager<ChessTurnManager>(systemsRoot, "ChessTurnManager", log);
        EnsureManager<ChessResignUiController>(systemsRoot, "ChessResignUiController", log);
        EnsureManager<ChessWinScreenUI>(systemsRoot, "ChessWinScreenUI", log);
        EnsureManager<PawnPromotionController>(systemsRoot, "PawnPromotionController", log);
        EnsureManager<PromotionSelectionUI>(systemsRoot, "PromotionSelectionUI", log);
        EnsureManager<ChessPauseMenuUI>(systemsRoot, "ChessPauseMenuUI", log);
        EnsureManager<ChessDevSandboxController>(systemsRoot, "ChessDevSandboxController", log);
        EnsureManager<ChessAiRoundConsole>(systemsRoot, "ChessAiRoundConsole", log);
        EnsureManager<StockfishService>(systemsRoot, "StockfishService", log);

        Canvas canvas = EnsureMasterCanvas(log);
        EnsureEventSystem(log);
        for (int i = 0; i < OverlayRoots.Length; i++) EnsureOverlayRoot(canvas.transform, OverlayRoots[i], log);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[ChessSceneSetup] Complete\n- {string.Join("\n- ", log)}");
    }

    [MenuItem("ChessGame/Setup/Validate Scene Managers And UI")]
    static void ValidateSceneManagersAndUi()
    {
        List<string> report = new();
        report.Add(GameObject.Find(SystemsRootName) != null ? "OK: ChessGameSystems present." : "ERROR: ChessGameSystems missing.");

        Canvas[] masterCanvases = FindByName<Canvas>(ChessMasterCanvas.CanvasName);
        report.Add(masterCanvases.Length > 0 ? "OK: ChessMasterCanvas present." : "ERROR: ChessMasterCanvas missing.");
        if (masterCanvases.Length > 1) report.Add($"WARN: Duplicate ChessMasterCanvas found ({masterCanvases.Length}).");

        if (masterCanvases.Length > 0)
        {
            Canvas canvas = masterCanvases[0];
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            report.Add(scaler != null ? "OK: CanvasScaler present." : "ERROR: CanvasScaler missing.");
            if (scaler != null)
            {
                bool valid = scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && scaler.referenceResolution == new Vector2(1920f, 1080f);
                report.Add(valid ? "OK: CanvasScaler configured." : "WARN: CanvasScaler not using 1920x1080 Scale With Screen Size.");
            }

            for (int i = 0; i < OverlayRoots.Length; i++) report.Add(canvas.transform.Find(OverlayRoots[i]) != null ? $"OK: {OverlayRoots[i]} present." : $"WARN: {OverlayRoots[i]} missing.");
        }

        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        report.Add(eventSystems.Length > 0 ? "OK: EventSystem present." : "ERROR: EventSystem missing.");
        if (eventSystems.Length > 1) report.Add($"WARN: Duplicate EventSystems found ({eventSystems.Length}).");
        if (eventSystems.Length > 0 && eventSystems[0].GetComponent<InputSystemUIInputModule>() == null) report.Add("WARN: EventSystem missing InputSystemUIInputModule.");

        ValidateDuplicate<ChessPauseManager>(report);
        ValidateDuplicate<ChessResignUiController>(report);
        ValidateDuplicate<ChessWinScreenUI>(report);
        ValidateDuplicate<StockfishService>(report);

        Debug.Log($"[ChessSceneSetup Validation]\n- {string.Join("\n- ", report)}");
    }

    #region Helpers
    static Transform GetOrCreateRoot(string name, List<string> log)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null) { log.Add($"Reused root: {name}"); return existing.transform; }
        GameObject created = new(name);
        Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
        log.Add($"Created root: {name}");
        return created.transform;
    }

    static T EnsureManager<T>(Transform parent, string objectName, List<string> log) where T : Component
    {
        T[] existing = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existing.Length > 1) log.Add($"Warning duplicate {typeof(T).Name} count={existing.Length}. Keeping first.");
        GameObject target = existing.Length > 0 ? existing[0].gameObject : GameObject.Find(objectName);
        if (target == null)
        {
            target = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(target, $"Create {objectName}");
            target.transform.SetParent(parent, false);
            log.Add($"Created manager object: {objectName}");
        }
        else if (target.transform.parent != parent)
        {
            Undo.SetTransformParent(target.transform, parent, $"Parent {objectName}");
            log.Add($"Reparented manager object: {objectName}");
        }

        if (target.GetComponent<T>() == null) { Undo.AddComponent<T>(target); log.Add($"Added component: {typeof(T).Name}"); }
        else log.Add($"Reused component: {typeof(T).Name}");
        return target.GetComponent<T>();
    }

    static Canvas EnsureMasterCanvas(List<string> log)
    {
        Canvas[] canvases = FindByName<Canvas>(ChessMasterCanvas.CanvasName);
        if (canvases.Length > 1) log.Add($"Warning duplicate ChessMasterCanvas count={canvases.Length}. Keeping first.");
        GameObject go = canvases.Length > 0 ? canvases[0].gameObject : new GameObject(ChessMasterCanvas.CanvasName);
        if (canvases.Length == 0) { Undo.RegisterCreatedObjectUndo(go, "Create ChessMasterCanvas"); log.Add("Created ChessMasterCanvas."); }

        Canvas canvas = go.GetComponent<Canvas>() ?? Undo.AddComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 5000);
        CanvasScaler scaler = go.GetComponent<CanvasScaler>() ?? Undo.AddComponent<CanvasScaler>(go);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        if (go.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(go);
        return canvas;
    }

    static void EnsureOverlayRoot(Transform canvas, string rootName, List<string> log)
    {
        Transform existing = canvas.Find(rootName);
        if (existing == null)
        {
            GameObject root = new(rootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, $"Create {rootName}");
            Undo.SetTransformParent(root.transform, canvas, $"Parent {rootName}");
            existing = root.transform;
            log.Add($"Created overlay root: {rootName}");
        }

        if (existing is RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        if (rootName != "GameplayHudRoot") existing.gameObject.SetActive(false);
    }

    static void EnsureEventSystem(List<string> log)
    {
        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (eventSystems.Length > 1) log.Add($"Warning duplicate EventSystem count={eventSystems.Length}. Keeping first.");
        GameObject go = eventSystems.Length > 0 ? eventSystems[0].gameObject : new GameObject("EventSystem");
        if (eventSystems.Length == 0) { Undo.RegisterCreatedObjectUndo(go, "Create EventSystem"); go.AddComponent<EventSystem>(); log.Add("Created EventSystem."); }
        if (go.GetComponent<InputSystemUIInputModule>() == null) { Undo.AddComponent<InputSystemUIInputModule>(go); log.Add("Added InputSystemUIInputModule."); }
    }

    static T[] FindByName<T>(string name) where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<T> filtered = new();
        for (int i = 0; i < components.Length; i++) if (components[i].name == name) filtered.Add(components[i]);
        return filtered.ToArray();
    }

    static void ValidateDuplicate<T>(List<string> report) where T : Component
    {
        T[] items = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        report.Add(items.Length <= 1 ? $"OK: {typeof(T).Name} count={items.Length}." : $"WARN: Duplicate {typeof(T).Name} count={items.Length}.");
    }
    #endregion
}
