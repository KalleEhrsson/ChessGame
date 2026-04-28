using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BrokenPieceCellNormalizer
{
    #region Menu

    [MenuItem("Tools/Chess/Normalize Broken Piece Cells")]
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

            if (EditorUtility.IsPersistent(selectedGameObject) && PrefabUtility.IsPartOfPrefabAsset(selectedGameObject))
            {
                if (NormalizePrefabAsset(selectedGameObject))
                {
                    normalizedCount++;
                }

                continue;
            }

            if (NormalizeSceneRoot(selectedGameObject))
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

        Undo.RegisterFullObjectHierarchyUndo(rootObject, "Normalize Broken Piece Cells");
        Vector3 subtractedOffset = NormalizeRoot(root, childCells);

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

        Debug.Log($"[BrokenPieceCellNormalizer] Root '{root.name}' normalized. Subtracted offset: {subtractedOffset}.");
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

            Vector3 subtractedOffset = NormalizeRoot(prefabRoot.transform, childCells);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
            Debug.Log($"[BrokenPieceCellNormalizer] Root '{prefabRoot.name}' normalized. Subtracted offset: {subtractedOffset}.");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    #endregion

    #region Core

    private static Vector3 NormalizeRoot(Transform root, List<Transform> childCells)
    {
        Vector3 centerLocal = CalculateCenterLocal(root, childCells);

        foreach (Transform child in childCells)
        {
            child.localPosition -= centerLocal;
        }

        BakeRootScaleIntoChildren(root, childCells);

        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

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
        foreach (Transform child in childCells)
        {
            sum += child.localPosition;
        }

        return sum / childCells.Count;
    }

    private static void BakeRootScaleIntoChildren(Transform root, List<Transform> childCells)
    {
        Vector3 rootScale = root.localScale;
        if (ApproximatelyOne(rootScale))
        {
            return;
        }

        foreach (Transform child in childCells)
        {
            child.localPosition = Vector3.Scale(child.localPosition, rootScale);
            child.localScale = Vector3.Scale(child.localScale, rootScale);
        }
    }

    private static List<Transform> GetDirectChildren(Transform root)
    {
        var children = new List<Transform>(root.childCount);
        for (int i = 0; i < root.childCount; i++)
        {
            children.Add(root.GetChild(i));
        }

        return children;
    }

    private static bool ApproximatelyOne(Vector3 value)
    {
        return Mathf.Approximately(value.x, 1f) && Mathf.Approximately(value.y, 1f) && Mathf.Approximately(value.z, 1f);
    }

    #endregion
}
