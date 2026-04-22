using System;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class ChessTurnManager : MonoBehaviour
{
    #region Singleton

    public static ChessTurnManager Instance { get; private set; }

    public static ChessTurnManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessTurnManager existing = FindFirstObjectByType<ChessTurnManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject host = new("ChessTurnManager");
        Instance = host.AddComponent<ChessTurnManager>();
        return Instance;
    }

    #endregion

    #region Variables

    [SerializeField] PieceTeam currentTurn = PieceTeam.White;
    [SerializeField] bool whiteControlledByAi;
    [SerializeField] bool blackControlledByAi = true;
    [SerializeField, Range(1, 25)] int aiSearchDepth = 10;
    [SerializeField, Range(0.5f, 1.5f)] float aiMoveDelayMin = 0.5f;
    [SerializeField, Range(0.5f, 1.5f)] float aiMoveDelayMax = 1.5f;

    ChessBoard board;
    ChessGameStateController gameStateController;
    StockfishService stockfishService;
    bool aiTurnInProgress;

    #endregion

    #region Events

    public event Action<PieceTeam> TurnChanged;

    #endregion

    #region Unity

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        ResolveSystems();
        HandleTurnStarted();
    }

    void OnValidate()
    {
        if (aiMoveDelayMax < aiMoveDelayMin)
        {
            aiMoveDelayMax = aiMoveDelayMin;
        }
    }

    #endregion

    #region API

    public PieceTeam GetCurrentTurn()
    {
        return currentTurn;
    }

    public bool IsAiTeam(PieceTeam team)
    {
        return team == PieceTeam.White ? whiteControlledByAi : blackControlledByAi;
    }

    public bool IsAiTurn()
    {
        return IsAiTeam(currentTurn);
    }

    public bool IsHumanTurn(PieceTeam team)
    {
        return !IsAiTeam(team);
    }

    public bool IsHumanTurn()
    {
        return !IsAiTurn();
    }

    public void SwitchTurn()
    {
        currentTurn = currentTurn == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        TurnChanged?.Invoke(currentTurn);
        HandleTurnStarted();
    }

    public void SetTurn(PieceTeam team)
    {
        currentTurn = team;
        TurnChanged?.Invoke(currentTurn);
        HandleTurnStarted();
    }

    #endregion

    #region AI Turn

    void ResolveSystems()
    {
        board ??= ChessBoard.Instance != null ? ChessBoard.Instance : FindFirstObjectByType<ChessBoard>();
        gameStateController ??= ChessGameStateController.GetOrCreate();
        stockfishService ??= StockfishService.GetOrCreate();
    }

    void HandleTurnStarted()
    {
        if (aiTurnInProgress)
        {
            return;
        }

        ResolveSystems();
        if (!IsAiTurn())
        {
            return;
        }

        if (gameStateController != null && !gameStateController.IsGameplayActive())
        {
            return;
        }

        _ = ExecuteAiTurnAsync();
    }

    async Task ExecuteAiTurnAsync()
    {
        aiTurnInProgress = true;

        try
        {
            ResolveSystems();
            if (board == null || stockfishService == null)
            {
                UnityEngine.Debug.LogError("[ChessTurnManager] Missing board or StockfishService for AI turn.");
                return;
            }

            string fen = ChessFenBuilder.BuildFen(board, currentTurn);
            if (string.IsNullOrWhiteSpace(fen))
            {
                UnityEngine.Debug.LogError("[ChessTurnManager] Failed to build FEN for AI turn.");
                return;
            }

            string bestMoveRaw = await stockfishService.RequestBestMoveAsync(fen, aiSearchDepth);
            if (string.IsNullOrWhiteSpace(bestMoveRaw))
            {
                UnityEngine.Debug.LogError("[ChessTurnManager] No bestmove received from Stockfish.");
                return;
            }

            if (!stockfishService.TryParseBestMove(bestMoveRaw, out string fromName, out string toName))
            {
                UnityEngine.Debug.LogError($"[ChessTurnManager] Invalid bestmove format: {bestMoveRaw}");
                return;
            }

            ChessTile fromTile = board.GetTile(fromName);
            ChessTile toTile = board.GetTile(toName);
            if (fromTile == null || toTile == null)
            {
                UnityEngine.Debug.LogError($"[ChessTurnManager] Could not resolve tiles for move {fromName}->{toName}.");
                return;
            }

            float delaySeconds = UnityEngine.Random.Range(aiMoveDelayMin, aiMoveDelayMax);
            int delayMs = Mathf.RoundToInt(delaySeconds * 1000f);
            await Task.Delay(delayMs);

            bool moved = board.MovePiece(fromTile, toTile);
            if (!moved)
            {
                UnityEngine.Debug.LogError($"[ChessTurnManager] AI move rejected by move system: {fromName}->{toName}.");
            }
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError($"[ChessTurnManager] AI turn failed: {exception.Message}");
        }
        finally
        {
            aiTurnInProgress = false;
        }
    }

    #endregion
}
