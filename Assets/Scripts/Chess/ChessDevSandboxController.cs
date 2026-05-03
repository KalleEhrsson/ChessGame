using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ChessDevSandboxController : MonoBehaviour
{
    public enum SandboxMode
    {
        None,
        Place,
        Remove,
        Move
    }

    public static ChessDevSandboxController Instance { get; private set; }

    [SerializeField] KeyCode toggleKey = KeyCode.F1;

    ChessBoard board;
    ChessTurnManager turnManager;
    ChessGameStateController gameStateController;
    ChessTileHoverController tileHoverController;
    ChessBoardStateTools boardTools;
    StockfishService stockfishService;
    InputAction toggleSandboxAction;
    KeyCode actionBoundToggleKey = KeyCode.None;

    readonly IReadOnlyList<ChessBoardPreset> presets = ChessBoardPresetLibrary.GetPresets();

    bool isOpen;
    CursorLockMode previousCursorLockMode;
    bool previousCursorVisible;
    bool hasStoredCursorState;
    SandboxMode currentMode;
    PieceTeam selectedTeam = PieceTeam.White;
    PieceType selectedPieceType = PieceType.Queen;
    bool aiEnabled = true;
    string fenBuffer = string.Empty;
    int presetIndex;
    ChessTile moveSourceTile;

    #region Properties

    public bool IsOpen => isOpen;
    public SandboxMode Mode => currentMode;
    public PieceTeam SelectedTeam => selectedTeam;
    public PieceType SelectedPieceType => selectedPieceType;
    public IReadOnlyList<ChessBoardPreset> Presets => presets;
    public int PresetIndex => presetIndex;
    public string FenBuffer => fenBuffer;
    public bool AiEnabled => aiEnabled;

    #endregion

    #region Bootstrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRuntimeInstance()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (FindFirstObjectByType<ChessDevSandboxController>() != null)
        {
            return;
        }

        GameObject host = new("ChessDevSandbox");
        host.AddComponent<ChessDevSandboxController>();
#endif
    }

    void Awake()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        enabled = false;
        return;
#endif
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureToggleInputAction();
        RefreshDependencies();
        EnsurePanelView();
    }

    void OnEnable()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif
        EnsureToggleInputAction();
        toggleSandboxAction?.Enable();
    }

    void OnDisable()
    {
        toggleSandboxAction?.Disable();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        toggleSandboxAction?.Dispose();
        toggleSandboxAction = null;
        actionBoundToggleKey = KeyCode.None;
    }

    void Update()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif
        EnsureToggleInputAction();

        if (toggleSandboxAction != null && toggleSandboxAction.WasPressedThisFrame())
        {
            ToggleOpen();
        }

        if (board == null || turnManager == null)
        {
            RefreshDependencies();
        }
    }

    void EnsureToggleInputAction()
    {
        if (toggleSandboxAction != null && actionBoundToggleKey == toggleKey)
        {
            return;
        }

        toggleSandboxAction?.Disable();
        toggleSandboxAction?.Dispose();

        string keyPath = $"<Keyboard>/{toggleKey.ToString().ToLowerInvariant()}";
        toggleSandboxAction = new InputAction("ToggleSandbox", InputActionType.Button, keyPath);
        actionBoundToggleKey = toggleKey;

        if (isActiveAndEnabled)
        {
            toggleSandboxAction.Enable();
        }
    }

    void EnsurePanelView()
    {
        ChessDevSandboxPanelView existingView = FindFirstObjectByType<ChessDevSandboxPanelView>();
        if (existingView != null)
        {
            return;
        }

        GameObject panelHost = new("ChessDevPanel");
        DontDestroyOnLoad(panelHost);
        panelHost.AddComponent<ChessDevSandboxPanelView>();
    }

    #endregion

    #region State

    public void ToggleOpen()
    {
        isOpen = !isOpen;

        if (isOpen)
        {
            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            hasStoredCursorState = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EnsurePanelView();
        }
        else
        {
            currentMode = SandboxMode.None;
            moveSourceTile = null;

            if (hasStoredCursorState)
            {
                Cursor.lockState = previousCursorLockMode;
                Cursor.visible = previousCursorVisible;
                hasStoredCursorState = false;
            }
        }
    }

    public void SetMode(SandboxMode mode)
    {
        currentMode = mode;
        if (mode != SandboxMode.Move)
        {
            moveSourceTile = null;
        }
    }

    public void SetSelectedTeam(PieceTeam team)
    {
        selectedTeam = team;
    }

    public void SetSelectedPieceType(PieceType pieceType)
    {
        selectedPieceType = pieceType;
    }

    public void SetPresetIndex(int index)
    {
        if (presets.Count == 0)
        {
            presetIndex = 0;
            return;
        }

        presetIndex = Mathf.Clamp(index, 0, presets.Count - 1);
    }

    public void SetFenBuffer(string fen)
    {
        fenBuffer = fen ?? string.Empty;
    }

    #endregion

    #region Actions

    public void ResetBoard()
    {
        RefreshDependencies();
        boardTools?.ResetBoard();
    }

    public void ClearBoard()
    {
        RefreshDependencies();
        boardTools?.ClearBoard();
    }

    public async void LoadSelectedPreset()
    {
        RefreshDependencies();
        if (boardTools == null || presets.Count == 0)
        {
            return;
        }

        bool success = await boardTools.LoadPresetAsync(presets[presetIndex]);
        if (success)
        {
            fenBuffer = boardTools.ExportFen();
        }

    }

    public void SetSideToMove(PieceTeam team)
    {
        RefreshDependencies();
        boardTools?.SetSideToMove(team);
    }

    public void ToggleAi(bool enabled)
    {
        RefreshDependencies();
        aiEnabled = enabled;
        boardTools?.SetAiEnabled(enabled);
    }

    public string ExportFen()
    {
        RefreshDependencies();
        fenBuffer = boardTools?.ExportFen() ?? string.Empty;
        return fenBuffer;
    }

    public async void ImportFen()
    {
        RefreshDependencies();
        bool success = boardTools != null && await boardTools.ImportFenAsync(fenBuffer);
        if (success)
        {
            fenBuffer = boardTools.ExportFen();
        }

    }

    public bool TryHandleSandboxTileClick()
    {
        if (!isOpen || currentMode == SandboxMode.None)
        {
            return false;
        }

        RefreshDependencies();
        if (tileHoverController == null)
        {
            return false;
        }

        ChessTile tile = tileHoverController.GetTileUnderCursor();
        if (tile == null)
        {
            return true;
        }

        _ = HandleTileClickAsync(tile);
        return true;
    }

    #endregion

    #region Internals

    void RefreshDependencies()
    {
        board = ChessBoard.Instance != null ? ChessBoard.Instance : FindFirstObjectByType<ChessBoard>();
        turnManager = ChessTurnManager.GetOrCreate();
        gameStateController = ChessGameStateController.GetOrCreate();
        tileHoverController = ChessTileHoverController.GetOrCreate();

        if (board != null && turnManager != null)
        {
            stockfishService ??= StockfishService.GetOrCreate();
            boardTools = new ChessBoardStateTools(board, turnManager, gameStateController, stockfishService);
        }

        aiEnabled = turnManager != null && turnManager.IsAiTeam(PieceTeam.White) && turnManager.IsAiTeam(PieceTeam.Black);
    }

    async System.Threading.Tasks.Task<bool> HandleTileClickAsync(ChessTile tile)
    {
        if (boardTools == null)
        {
            return false;
        }

        switch (currentMode)
        {
            case SandboxMode.Place:
                return await boardTools.SpawnPieceAsync(selectedTeam, selectedPieceType, tile);
            case SandboxMode.Remove:
                return await boardTools.RemovePieceAsync(tile);
            case SandboxMode.Move:
                if (moveSourceTile == null)
                {
                    if (tile.CurrentPiece == null)
                    {
                        Debug.LogWarning("[ChessDevSandbox] Move mode source tile is empty.");
                        return true;
                    }

                    moveSourceTile = tile;
                    return true;
                }

                ChessTile fromTile = moveSourceTile;
                moveSourceTile = null;
                return await boardTools.MovePieceAsync(fromTile, tile);
            default:
                return false;
        }
    }

    #endregion
}
