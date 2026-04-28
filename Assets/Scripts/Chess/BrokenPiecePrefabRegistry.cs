using System;
using UnityEngine;

[DisallowMultipleComponent]
public class BrokenPiecePrefabRegistry : MonoBehaviour
{
    [Serializable]
    struct BrokenPiecePrefabEntry
    {
        public PieceTeam team;
        public PieceType type;
        public GameObject prefab;
    }

    [SerializeField] BrokenPiecePrefabEntry[] entries = Array.Empty<BrokenPiecePrefabEntry>();

    public GameObject GetBrokenPrefab(PieceTeam team, PieceType type)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            BrokenPiecePrefabEntry entry = entries[i];
            if (entry.team == team && entry.type == type)
            {
                return entry.prefab;
            }
        }

        return null;
    }

    public static string GetExpectedBrokenPrefabAssetPath(PieceTeam team, PieceType type)
    {
        string color = team == PieceTeam.White ? "White" : "Black";
        return $"Assets/Prefabs/ChessPiecesBroken/{color}/{color}{type}Broken.prefab";
    }

#if UNITY_EDITOR
    [ContextMenu("Auto Fill Broken Piece Prefabs")]
    void AutoFillBrokenPiecePrefabs()
    {
        PieceTeam[] teams = { PieceTeam.White, PieceTeam.Black };
        PieceType[] pieceTypes =
        {
            PieceType.Pawn,
            PieceType.Rook,
            PieceType.Knight,
            PieceType.Bishop,
            PieceType.Queen,
            PieceType.King
        };

        entries = new BrokenPiecePrefabEntry[teams.Length * pieceTypes.Length];
        int index = 0;

        for (int teamIndex = 0; teamIndex < teams.Length; teamIndex++)
        {
            for (int typeIndex = 0; typeIndex < pieceTypes.Length; typeIndex++)
            {
                PieceTeam team = teams[teamIndex];
                PieceType type = pieceTypes[typeIndex];
                string path = GetExpectedBrokenPrefabAssetPath(team, type);
                Debug.Log($"[BrokenPiecePrefabRegistry] Checking: {path}");

                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[BrokenPiecePrefabRegistry] Missing prefab at path: {path}");
                }

                entries[index] = new BrokenPiecePrefabEntry
                {
                    team = team,
                    type = type,
                    prefab = prefab
                };
                index++;
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
