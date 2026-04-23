using System;
using System.Collections.Generic;

public readonly struct ChessBoardPreset
{
    public string Name { get; }
    public string Fen { get; }
    public PieceTeam ActiveTurn { get; }

    public ChessBoardPreset(string name, string fen, PieceTeam activeTurn)
    {
        Name = name;
        Fen = fen;
        ActiveTurn = activeTurn;
    }
}

public static class ChessBoardPresetLibrary
{
    static readonly ChessBoardPreset[] Presets =
    {
        new("Standard Start", "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", PieceTeam.White),
        new("Empty Board", "8/8/8/8/8/8/8/8 w - - 0 1", PieceTeam.White),
        new("Castling Ready", "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", PieceTeam.White),
        new("Castling Blocked", "r3k2r/8/8/8/8/8/8/R2QK2R w KQkq - 0 1", PieceTeam.White),
        new("En Passant Ready", "4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", PieceTeam.White),
        new("En Passant Expired", "4k3/8/8/3pP3/8/8/8/4K3 w - - 1 1", PieceTeam.White),
        new("Promotion Ready", "4k3/6P1/8/8/8/8/8/4K3 w - - 0 1", PieceTeam.White),
        new("Check", "4k3/8/8/8/8/8/4Q3/4K3 b - - 0 1", PieceTeam.Black),
        new("Checkmate", "7k/6Q1/6K1/8/8/8/8/8 b - - 0 1", PieceTeam.Black),
        new("Stalemate", "7k/5Q2/6K1/8/8/8/8/8 b - - 0 1", PieceTeam.Black),
        new("Sparse Sandbox", "4k3/8/8/3q4/8/8/4P3/4K3 w - - 0 1", PieceTeam.White)
    };

    #region API

    public static IReadOnlyList<ChessBoardPreset> GetPresets() => Presets;

    public static bool TryGetPreset(string name, out ChessBoardPreset preset)
    {
        for (int i = 0; i < Presets.Length; i++)
        {
            if (string.Equals(Presets[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                preset = Presets[i];
                return true;
            }
        }

        preset = default;
        return false;
    }

    #endregion
}
