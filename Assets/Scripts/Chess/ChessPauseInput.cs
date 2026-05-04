using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ChessPauseInput : MonoBehaviour
{
    static ChessPauseInput activeInstance;

    [SerializeField] bool enablePauseDebugLogs;

    ChessPauseManager pauseManager;

    void Awake()
    {
        if (activeInstance != null && activeInstance != this)
        {
            Debug.LogWarning($"[ChessPauseInput] Duplicate input disabled: {name} ({GetInstanceID()}) existing={activeInstance.name} ({activeInstance.GetInstanceID()})", this);
            enabled = false;
            return;
        }

        activeInstance = this;
        pauseManager = ChessPauseManager.GetOrCreate();
    }

    void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.pKey.wasPressedThisFrame)
        {
            return;
        }

        pauseManager ??= ChessPauseManager.GetOrCreate();
        pauseManager.TogglePauseRequest();

        if (enablePauseDebugLogs)
        {
            Debug.Log($"[ChessPauseInput] {name} ({GetInstanceID()}) handled P -> manager {pauseManager.name} ({pauseManager.GetInstanceID()})", this);
        }
    }
}
