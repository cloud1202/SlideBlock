using UnityEditor;
using UnityEngine;

public class UIPrefabPostprocessor : AssetPostprocessor
{
    // UI Prefab이 저장되는 경로
    private const string UI_PATH = "Assets/AddressableAssets/Prefabs/UI/";

    static void OnPostprocessAllAssets(
        string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (var path in importedAssets)
        {
            if (!path.StartsWith(UI_PATH) || !path.EndsWith(".prefab"))
                continue;

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null || !go.activeSelf) continue;

            // Prefab 열어서 루트 비활성화 후 저장
            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            scope.prefabContentsRoot.SetActive(false);

            LLogger.Log($"[UIPrefabPostprocessor] Auto-deactivated: {path}");
        }
    }
}
