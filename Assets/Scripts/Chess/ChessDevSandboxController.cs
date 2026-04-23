using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] KeyCode alternateToggleKey = KeyCode.BackQuote;

    ChessBoard board;
    ChessTurnManager turnManager;
    ChessGameStateController gameStateController;
    ChessTileHoverController tileHoverController;
    ChessBoardStateTools boardTools;

    readonly IReadOnlyList<ChessBoardPreset> presets = ChessBoardPresetLibrary.GetPresets();

    bool isOpen;
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
        host.AddComponent<ChessDevSandboxUI>();
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
        RefreshDependencies();
    }

    void Update()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif
        if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(alternateToggleKey))
        {
            ToggleOpen();
        }

        if (board == null || turnManager == null)
        {
            RefreshDependencies();
        }
    }

    #endregion

    #region State

    public void ToggleOpen()
    {
        isOpen = !isOpen;
        if (!isOpen)
        {
            currentMode = SandboxMode.None;
            moveSourceTile = null;
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

    public bool LoadSelectedPreset()
    {
        RefreshDependencies();
        if (boardTools == null || presets.Count == 0)
        {
            return false;
        }

        bool success = boardTools.LoadPreset(presets[presetIndex]);
        if (success)
        {
            fenBuffer = boardTools.ExportFen();
        }

        return success;
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

    public bool ImportFen()
    {
        RefreshDependencies();
        bool success = boardTools != null && boardTools.ImportFen(fenBuffer);
        if (success)
        {
            fenBuffer = boardTools.ExportFen();
        }

        return success;
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

        return HandleTileClick(tile);
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
            boardTools = new ChessBoardStateTools(board, turnManager, gameStateController);
        }

        aiEnabled = turnManager != null && turnManager.IsAiTeam(PieceTeam.White) && turnManager.IsAiTeam(PieceTeam.Black);
    }

    bool HandleTileClick(ChessTile tile)
    {
        if (boardTools == null)
        {
            return false;
        }

        switch (currentMode)
        {
            case SandboxMode.Place:
                return boardTools.SpawnPiece(selectedTeam, selectedPieceType, tile);
            case SandboxMode.Remove:
                return boardTools.RemovePiece(tile);
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
                return boardTools.MovePiece(fromTile, tile);
            default:
                return false;
        }
    }

    #endregion
}
