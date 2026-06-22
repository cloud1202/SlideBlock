#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ToolKit.Editor
{
    /// <summary>
    /// 에디터 메뉴에서 PlayerPrefs를 초기화(삭제)할 수 있는 유틸리티.
    /// 메뉴 경로: Tools/PlayerPrefs/...
    /// </summary>
    public static class PlayerPrefsEditor
    {
        private const string MenuRoot = "Tools/PlayerPrefs/";

        [MenuItem(MenuRoot + "Clear All PlayerPrefs", priority = 1)]
        private static void ClearAllPlayerPrefs()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "PlayerPrefs 초기화",
                "모든 PlayerPrefs 데이터를 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
                "삭제",
                "취소");

            if (!confirmed)
                return;

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            Debug.Log("[PlayerPrefsEditorUtility] 모든 PlayerPrefs가 초기화되었습니다.");
        }

        [MenuItem(MenuRoot + "Clear Specific Key...", priority = 2)]
        private static void ClearSpecificKey()
        {
            PlayerPrefsKeyDeleteWindow.Open();
        }

        [MenuItem(MenuRoot + "Log All Keys (Editor Registry)", priority = 21)]
        private static void LogAllKeysHint()
        {
            // PlayerPrefs는 전체 키 목록을 가져오는 공식 API가 없어 Windows 레지스트리 /
            // macOS plist 파일을 직접 열어 확인해야 함을 안내.
            Debug.Log(
                "[PlayerPrefsEditorUtility] Unity의 PlayerPrefs API는 전체 키 목록 조회를 지원하지 않습니다.\n" +
                "- Windows: 레지스트리 편집기에서 HKCU\\Software\\Unity\\UnityEditor\\<CompanyName>\\<ProductName> 경로 확인\n" +
                "- macOS: ~/Library/Preferences/unity.<CompanyName>.<ProductName>.plist 파일 확인\n" +
                "프로젝트 내에서 사용 중인 키를 직접 관리하고 싶다면 별도의 키 상수 클래스를 만들어 추적하는 것을 권장합니다.");
        }
    }

    /// <summary>
    /// 특정 PlayerPrefs 키 하나만 삭제할 수 있는 간단한 에디터 윈도우.
    /// </summary>
    public class PlayerPrefsKeyDeleteWindow : EditorWindow
    {
        private string _key = string.Empty;

        public static void Open()
        {
            var window = GetWindow<PlayerPrefsKeyDeleteWindow>(true, "Delete PlayerPrefs Key", true);
            window.minSize = new Vector2(320, 100);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("삭제할 PlayerPrefs 키를 입력하세요.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(4);

            _key = EditorGUILayout.TextField("Key", _key);

            EditorGUILayout.Space(8);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_key)))
            {
                if (GUILayout.Button("Delete Key"))
                {
                    if (PlayerPrefs.HasKey(_key))
                    {
                        PlayerPrefs.DeleteKey(_key);
                        PlayerPrefs.Save();
                        Debug.Log($"[PlayerPrefsEditorUtility] 키 '{_key}' 가 삭제되었습니다.");
                        Close();
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayerPrefsEditorUtility] 키 '{_key}' 를 찾을 수 없습니다.");
                    }
                }
            }
        }
    }
}
#endif