using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class PieceCellGrouper
{
    #region Menu

    [MenuItem("Tools/Chess/Group Piece Cells")]
    public static void GroupPieceCells()
    {
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        Dictionary<string, List<Transform>> groups = new();

        foreach (Transform t in allTransforms)
        {
            string groupName = GetGroupName(t.name);

            if (string.IsNullOrEmpty(groupName))
                continue;

            // Ensure object is editable (unpack if needed)
            if (PrefabUtility.IsPartOfPrefabInstance(t))
            {
                GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(t);
                if (root != null)
                {
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
            }

            if (!groups.ContainsKey(groupName))
                groups[groupName] = new List<Transform>();

            groups[groupName].Add(t);
        }

        foreach (var pair in groups)
        {
            string groupName = pair.Key;
            List<Transform> children = pair.Value;

            GameObject parent = GameObject.Find(groupName);
            if (parent == null)
            {
                parent = new GameObject(groupName);
                Undo.RegisterCreatedObjectUndo(parent, $"Create {groupName}");
            }

            foreach (Transform child in children)
            {
                if (child == parent.transform)
                    continue;

                Undo.SetTransformParent(child, parent.transform, $"Parent {child.name}");
            }
        }

        Debug.Log($"Grouped {groups.Count} cell groups.");
    }

    #endregion

    #region Helpers

    static string GetGroupName(string name)
    {
        Match match = Regex.Match(name, @"^(?<group>.+)_cell(?:\.\d+)?$");
        return match.Success ? match.Groups["group"].Value : string.Empty;
    }

    #endregion
}