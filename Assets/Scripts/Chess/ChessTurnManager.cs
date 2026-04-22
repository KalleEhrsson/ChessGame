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

    #endregion

    #region API

    public PieceTeam GetCurrentTurn()
    {
        return currentTurn;
    }

    public void SwitchTurn()
    {
        currentTurn = currentTurn == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
    }

    public void SetTurn(PieceTeam team)
    {
        currentTurn = team;
    }

    #endregion
}
