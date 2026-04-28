using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BrokenPiecePrefabRegistry))]
public class BrokenPiecePrefabRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8f);
        if (GUILayout.Button("Auto Fill Broken Piece Prefabs"))
        {
            BrokenPiecePrefabRegistry registry = (BrokenPiecePrefabRegistry)target;
            registry.AutoFillBrokenPiecePrefabs();
            EditorUtility.SetDirty(registry);
        }
    }
}
