using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BrokenPieceCellNormalizer
{
    private const string MenuPath = "Tools/Chess/Normalize Broken Piece Cells";

    #region Menu

    [MenuItem(MenuPath)]
    private static void NormalizeBrokenPieceCells()
    {
        Object[] selectedObjects = Selection.objects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("[BrokenPieceCellNormalizer] No objects selected.");
            return;
        }

        int normalizedCount = 0;

        foreach (Object selectedObject in selectedObjects)
        {
            if (selectedObject is not GameObject selectedGameObject)
            {
                continue;
            }

            bool normalized = EditorUtility.IsPersistent(selectedGameObject) && PrefabUtility.IsPartOfPrefabAsset(selectedGameObject)
                ? NormalizePrefabAsset(selectedGameObject)
                : NormalizeSceneRoot(selectedGameObject);

            if (normalized)
            {
                normalizedCount++;
            }
        }

        Debug.Log($"[BrokenPieceCellNormalizer] Normalized roots: {normalizedCount}.");
    }

    #endregion

    #region Scene

    private static bool NormalizeSceneRoot(GameObject rootObject)
    {
        Transform root = rootObject.transform;
        List<Transform> childCells = GetDirectChildren(root);
        if (childCells.Count == 0)
        {
            Debug.LogWarning($"[BrokenPieceCellNormalizer] No child cells found for root '{root.name}'.");
            return false;
        }

        string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rootObject);
        Vector3 targetRootScale = GetTargetRootScale(rootObject.name, sourcePath);

        Undo.RegisterFullObjectHierarchyUndo(rootObject, "Normalize Broken Piece Cells");
        Vector3 subtractedOffset = NormalizeRoot(root, childCells, targetRootScale);

        if (PrefabUtility.IsPartOfPrefabInstance(rootObject))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            foreach (Transform child in childCells)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(child);
            }

            if (PrefabUtility.GetPrefabInstanceStatus(rootObject) == PrefabInstanceStatus.Connected)
            {
                PrefabUtility.ApplyPrefabInstance(rootObject, InteractionMode.UserAction);
            }
        }

        Debug.Log($"[BrokenPieceCellNormalizer] Root '{root.name}' normalized. Subtracted offset: {subtractedOffset}. Target root scale: {targetRootScale}.");
        return true;
    }

    #endregion

    #region PrefabAsset

    private static bool NormalizePrefabAsset(GameObject prefabAsset)
    {
        string assetPath = AssetDatabase.GetAssetPath(prefabAsset);
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
        try
        {
            List<Transform> childCells = GetDirectChildren(prefabRoot.transform);
            if (childCells.Count == 0)
            {
                Debug.LogWarning($"[BrokenPieceCellNormalizer] No child cells found for prefab '{prefabAsset.name}'.");
                return false;
            }

            Vector3 targetRootScale = GetTargetRootScale(prefabRoot.name, assetPath);
            Vector3 subtractedOffset = NormalizeRoot(prefabRoot.transform, childCells, targetRootScale);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);

            Debug.Log($"[BrokenPieceCellNormalizer] Root '{prefabRoot.name}' normalized. Subtracted offset: {subtractedOffset}. Target root scale: {targetRootScale}.");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    #endregion

    #region Core

    private static Vector3 NormalizeRoot(Transform root, List<Transform> childCells, Vector3 targetRootScale)
    {
        Vector3 centerLocal = CalculateCenterLocal(root, childCells);
        for (int i = 0; i < childCells.Count; i++)
        {
            childCells[i].localPosition -= centerLocal;
        }

        BakeRootScaleIntoChildren(root, childCells, targetRootScale);

        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = targetRootScale;

        return centerLocal;
    }

    private static Vector3 CalculateCenterLocal(Transform root, List<Transform> childCells)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return root.InverseTransformPoint(bounds.center);
        }

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < childCells.Count; i++)
        {
            sum += childCells[i].localPosition;
        }

        return sum / childCells.Count;
    }

    private static void BakeRootScaleIntoChildren(Transform root, List<Transform> childCells, Vector3 targetRootScale)
    {
        Vector3 currentScale = root.localScale;
        if (Approximately(currentScale, targetRootScale))
        {
            return;
        }

        Vector3 bakeScale = new Vector3(
            SafeDivide(currentScale.x, targetRootScale.x),
            SafeDivide(currentScale.y, targetRootScale.y),
            SafeDivide(currentScale.z, targetRootScale.z));

        for (int i = 0; i < childCells.Count; i++)
        {
            Transform child = childCells[i];
            child.localPosition = Vector3.Scale(child.localPosition, bakeScale);
            child.localScale = Vector3.Scale(child.localScale, bakeScale);
        }
    }

    #endregion

    #region CounterpartScale

    private static Vector3 GetTargetRootScale(string rootName, string sourcePath)
    {
        if (TryGetCounterpartPrefabPath(rootName, sourcePath, out string counterpartPath))
        {
            GameObject counterpart = AssetDatabase.LoadAssetAtPath<GameObject>(counterpartPath);
            if (counterpart != null)
            {
                return counterpart.transform.localScale;
            }
        }

        return Vector3.one;
    }

    private static bool TryGetCounterpartPrefabPath(string rootName, string sourcePath, out string counterpartPath)
    {
        counterpartPath = string.Empty;

        if (!string.IsNullOrEmpty(sourcePath) && sourcePath.Contains("/ChessPiecesBroken/"))
        {
            string colorFolder = Path.GetFileName(Path.GetDirectoryName(sourcePath));
            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            if (TryParseBrokenName(fileName, colorFolder, out string pieceType))
            {
                counterpartPath = $"Assets/Prefabs/ChessPieces/{colorFolder}/{colorFolder}_{pieceType}.prefab";
                return true;
            }
        }

        if (TryParseBrokenName(rootName, "White", out string whitePieceType))
        {
            counterpartPath = $"Assets/Prefabs/ChessPieces/White/White_{whitePieceType}.prefab";
            return true;
        }

        if (TryParseBrokenName(rootName, "Black", out string blackPieceType))
        {
            counterpartPath = $"Assets/Prefabs/ChessPieces/Black/Black_{blackPieceType}.prefab";
            return true;
        }

        return false;
    }

    private static bool TryParseBrokenName(string value, string colorPrefix, out string pieceType)
    {
        pieceType = string.Empty;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string normalized = value.Replace("_", string.Empty);
        if (!normalized.StartsWith(colorPrefix) || !normalized.EndsWith("Broken"))
        {
            return false;
        }

        int typeStart = colorPrefix.Length;
        int typeLength = normalized.Length - colorPrefix.Length - "Broken".Length;
        if (typeLength <= 0)
        {
            return false;
        }

        pieceType = normalized.Substring(typeStart, typeLength);
        return true;
    }

    #endregion

    #region Helpers

    private static List<Transform> GetDirectChildren(Transform root)
    {
        var children = new List<Transform>(root.childCount);
        for (int i = 0; i < root.childCount; i++)
        {
            children.Add(root.GetChild(i));
        }

        return children;
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        return Mathf.Approximately(denominator, 0f) ? 1f : numerator / denominator;
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return Mathf.Approximately(a.x, b.x)
            && Mathf.Approximately(a.y, b.y)
            && Mathf.Approximately(a.z, b.z);
    }

    #endregion
}
