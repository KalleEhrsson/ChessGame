using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class ChessMasterCanvas
{
    public const string CanvasName = "ChessMasterCanvas";

    static Canvas cachedCanvas;

    public static Canvas GetOrCreateCanvas()
    {
        if (cachedCanvas != null)
        {
            return cachedCanvas;
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Canvas firstValid = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] == null)
            {
                continue;
            }

            if (canvases[i].name == CanvasName)
            {
                if (firstValid == null)
                {
                    firstValid = canvases[i];
                }
                else
                {
                    Debug.LogWarning($"[ChessRuntimeBootstrap] Found duplicate: {CanvasName} on {canvases[i].gameObject.scene.name}/{canvases[i].name}");
                }
            }
        }

        if (firstValid == null)
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].isRootCanvas)
                {
                    firstValid = canvases[i];
                    firstValid.gameObject.name = CanvasName;
                    break;
                }
            }
        }

        if (firstValid == null)
        {
            GameObject canvasObject = new(CanvasName);
            firstValid = canvasObject.AddComponent<Canvas>();
        }

        firstValid.renderMode = RenderMode.ScreenSpaceOverlay;
        firstValid.sortingOrder = Mathf.Max(firstValid.sortingOrder, 5000);
        EnsureCanvasComponents(firstValid);
        EnsureEventSystem();
        cachedCanvas = firstValid;
        return cachedCanvas;
    }

    public static Transform GetOrCreateOverlayRoot(string rootName)
    {
        Canvas canvas = GetOrCreateCanvas();
        Transform existing = canvas.transform.Find(rootName);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new(rootName, typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return root.transform;
    }

    public static bool HasRoot(string rootName)
    {
        return GetOrCreateCanvas().transform.Find(rootName) != null;
    }

    static void EnsureCanvasComponents(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    static void EnsureEventSystem()
    {
        EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (systems.Length == 0)
        {
            GameObject eventSystemObject = new("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[ChessRuntimeBootstrap] Created fallback instance: EventSystem");
            return;
        }
        if (systems.Length > 1)
        {
            Debug.LogWarning($"[ChessRuntimeBootstrap] Found duplicate: EventSystem count={systems.Length}");
        }
    }
}
