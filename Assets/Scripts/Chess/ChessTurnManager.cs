using System;
using System.Threading;
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
    ChessAiRoundConsole aiRoundConsole;
    bool aiTurnInProgress;
    int aiTurnSequence;
    CancellationTokenSource aiTurnCancellation;
    bool boardEventsBound;
    bool stockfishEventsBound;

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

    void OnDestroy()
    {
        CancelAiTurn("turn manager destroyed");

        if (boardEventsBound && board != null)
        {
            board.PieceMoved -= OnBoardPieceMoved;
            boardEventsBound = false;
        }

        if (stockfishEventsBound && stockfishService != null)
        {
            stockfishService.EngineLineReceived -= OnStockfishEngineLine;
            stockfishEventsBound = false;
        }
    }

    void OnDisable()
    {
        CancelAiTurn("turn manager disabled");
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
        CancelAiTurn("turn switched");
        currentTurn = currentTurn == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        TurnChanged?.Invoke(currentTurn);
        HandleTurnStarted();
    }

    public void SetTurn(PieceTeam team)
    {
        CancelAiTurn("turn set");
        currentTurn = team;
        TurnChanged?.Invoke(currentTurn);
        HandleTurnStarted();
    }

    public void SetAiControl(PieceTeam team, bool enabled)
    {
        if (team == PieceTeam.White)
        {
            whiteControlledByAi = enabled;
        }
        else
        {
            blackControlledByAi = enabled;
        }

        if (IsAiTurn())
        {
            HandleTurnStarted();
            return;
        }

        CancelAiTurn("active turn changed to human");
    }

    public void SetAiEnabledForBothTeams(bool enabled)
    {
        whiteControlledByAi = enabled;
        blackControlledByAi = enabled;

        if (IsAiTurn())
        {
            HandleTurnStarted();
            return;
        }

        CancelAiTurn("ai disabled for active turn");
    }

    #endregion

    #region AI Turn

    void ResolveSystems()
    {
        board ??= ChessBoard.Instance != null ? ChessBoard.Instance : FindFirstObjectByType<ChessBoard>();
        gameStateController ??= ChessGameStateController.GetOrCreate();
        stockfishService ??= StockfishService.GetOrCreate();
        aiRoundConsole ??= ChessAiRoundConsole.GetOrCreate();

        if (!boardEventsBound && board != null)
        {
            board.PieceMoved += OnBoardPieceMoved;
            boardEventsBound = true;
        }

        if (!stockfishEventsBound && stockfishService != null)
        {
            stockfishService.EngineLineReceived += OnStockfishEngineLine;
            stockfishEventsBound = true;
        }
    }

    void HandleTurnStarted()
    {
        if (aiTurnInProgress)
        {
            CancelAiTurn("restarting ai turn");
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

        int turnId = ++aiTurnSequence;
        aiTurnCancellation?.Cancel();
        aiTurnCancellation?.Dispose();
        aiTurnCancellation = new CancellationTokenSource();
        _ = ExecuteAiTurnAsync(turnId, aiTurnCancellation.Token);
    }

    async Task ExecuteAiTurnAsync(int turnId, CancellationToken cancellationToken)
    {
        aiTurnInProgress = true;

        try
        {
            await WaitForPieceMotionAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

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

            aiRoundConsole?.SetFen(fen);
            aiRoundConsole?.SetThinking(true);

            string bestMoveRaw = await stockfishService.RequestBestMoveAsync(fen, cancellationToken, aiSearchDepth);
            if (cancellationToken.IsCancellationRequested || turnId != aiTurnSequence)
            {
                UnityEngine.Debug.Log("[ChessTurnManager] Stale AI response ignored.");
                return;
            }

            if (string.IsNullOrWhiteSpace(bestMoveRaw))
            {
                aiRoundConsole?.SetThinking(false);
                UnityEngine.Debug.LogError("[ChessTurnManager] No bestmove received from Stockfish.");
                return;
            }

            aiRoundConsole?.SetStockfishMove(bestMoveRaw);
            aiRoundConsole?.SetThinking(false);

            if (!stockfishService.TryParseBestMove(bestMoveRaw, out string fromName, out string toName, out PieceType? promotionPiece))
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
            await Task.Delay(delayMs, cancellationToken);
            await WaitForPieceMotionAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (turnId != aiTurnSequence)
            {
                UnityEngine.Debug.Log("[ChessTurnManager] Stale AI move ignored.");
                return;
            }

            bool moved = board.MovePiece(fromTile, toTile, promotionPiece);
            if (!moved)
            {
                UnityEngine.Debug.LogError($"[ChessTurnManager] AI move rejected by move system: {fromName}->{toName}.");
            }
        }
        catch (OperationCanceledException)
        {
            aiRoundConsole?.SetThinking(false);
            UnityEngine.Debug.Log("[ChessTurnManager] AI turn cancelled.");
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

    void OnBoardPieceMoved(ChessPiece movedPiece, ChessTile fromTile, ChessTile toTile)
    {
        if (movedPiece == null || fromTile == null || toTile == null)
        {
            return;
        }

        ResolveSystems();
        string readableMove = BuildReadableMove(movedPiece, fromTile, toTile);
        if (IsAiTeam(movedPiece.Team))
        {
            aiRoundConsole?.SetAiMove(readableMove);
            return;
        }

        aiRoundConsole?.StartNewRound(readableMove);
    }

    void OnStockfishEngineLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        ResolveSystems();

        if (line.StartsWith("info ", StringComparison.OrdinalIgnoreCase))
        {
            aiRoundConsole?.SetStockfishInfo(line);
            return;
        }

        if (line.StartsWith("bestmove ", StringComparison.OrdinalIgnoreCase))
        {
            aiRoundConsole?.SetStockfishMove(line);
            aiRoundConsole?.SetThinking(false);
        }
    }

    static string BuildReadableMove(ChessPiece piece, ChessTile fromTile, ChessTile toTile)
    {
        string team = piece.Team == PieceTeam.White ? "White" : "Black";
        string pieceType = piece.Type.ToString();
        return $"{team} {pieceType}: {fromTile.TileName.ToLowerInvariant()} -> {toTile.TileName.ToLowerInvariant()}";
    }

    static async Task WaitForPieceMotionAsync()
    {
        while (ChessPieceMotion.IsAnyAnimating)
        {
            await Task.Yield();
        }
    }

    static async Task WaitForPieceMotionAsync(CancellationToken cancellationToken)
    {
        while (ChessPieceMotion.IsAnyAnimating)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    void CancelAiTurn(string reason)
    {
        if (aiTurnCancellation == null)
        {
            return;
        }

        if (!aiTurnCancellation.IsCancellationRequested)
        {
            UnityEngine.Debug.Log($"[ChessTurnManager] AI turn cancelled: {reason}.");
            aiTurnCancellation.Cancel();
        }

        aiTurnCancellation.Dispose();
        aiTurnCancellation = null;
    }

    #endregion
}
