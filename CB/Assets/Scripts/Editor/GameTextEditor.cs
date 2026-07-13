using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameTextSO))]
public class GameTextSOEditor :Editor
{
    #region Constants

    // 인스펙터를 닫았다 열어도 마지막으로 보던 언어 인덱스를 기억하기 위한 키
    private const string LANGUAGE_INDEX_PREF_KEY = "GameTextSOEditor_LanguageIndex";

    #endregion

    #region Fields

    private SerializedProperty _textDataProp; 
    private LanguageType _languageType = LanguageType.English;

    #endregion

    #region Unity Callbacks

    private void OnEnable()
    {
        _textDataProp = serializedObject.FindProperty("textData");
        _languageType = EnumConverter.IntToEnum32<LanguageType>(EditorPrefs.GetInt(LANGUAGE_INDEX_PREF_KEY, 0));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawLanguageSelector();
        EditorGUILayout.Space(8);
        DrawTextDataList();
        EditorGUILayout.Space(8);
        DrawAddRemoveButtons();

        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    #region Drawing

    private void DrawLanguageSelector()
    {
        EditorGUILayout.LabelField("로컬라이징 미리보기", EditorStyles.boldLabel);

        // MAX는 개수 카운트용 sentinel이므로 선택지에서 제외
        var names = System.Enum.GetNames(typeof(LanguageType));
        var displayNames = new string[names.Length - 1];
        System.Array.Copy(names, displayNames, displayNames.Length);

        var currentIndex = EnumConverter.Enum32ToInt(_languageType);

        EditorGUI.BeginChangeCheck();
        var newIndex = EditorGUILayout.Popup("국가 인덱스", currentIndex, displayNames);
        if (EditorGUI.EndChangeCheck())
        {
            _languageType = (LanguageType)newIndex;
            EditorPrefs.SetInt(LANGUAGE_INDEX_PREF_KEY, currentIndex);
        }
    }

    private void DrawTextDataList()
    {
        EditorGUILayout.LabelField("텍스트 목록", EditorStyles.boldLabel);

        var currentIndex = EnumConverter.Enum32ToInt(_languageType);
        for (var i = 0; i < _textDataProp.arraySize; i++)
        {
            var elementProp = _textDataProp.GetArrayElementAtIndex(i);
            var idProp = elementProp.FindPropertyRelative("id");
            var textArrayProp = elementProp.FindPropertyRelative("text");

            EnsureArrayLength(textArrayProp, currentIndex + 1);

            var textElementProp = textArrayProp.GetArrayElementAtIndex(currentIndex);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(idProp, GUIContent.none, GUILayout.Width(180));

                    if (GUILayout.Button("삭제", GUILayout.Width(50)))
                    {
                        _textDataProp.DeleteArrayElementAtIndex(i);
                        break; // 리스트가 변경됐으니 이번 프레임 순회는 여기서 중단
                    }
                }

                textElementProp.stringValue = EditorGUILayout.TextArea(
                    textElementProp.stringValue,
                    GUILayout.MinHeight(40));
            }
        }
    }

    private void DrawAddRemoveButtons()
    {
        if (GUILayout.Button("+ 항목 추가"))
        {
            var currentIndex = EnumConverter.Enum32ToInt(_languageType);
            _textDataProp.arraySize++;
            var newElement = _textDataProp.GetArrayElementAtIndex(_textDataProp.arraySize - 1);
            newElement.FindPropertyRelative("text").arraySize = currentIndex + 1;
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// 텍스트 배열이 목표 길이보다 짧으면 빈 문자열로 채워 늘린다.
    /// (아직 해당 언어 슬롯의 텍스트가 채워지지 않은 항목을 대비)
    /// </summary>
    private static void EnsureArrayLength(SerializedProperty arrayProp, int requiredLength)
    {
        if (arrayProp.arraySize >= requiredLength)
        {
            return;
        }

        var previousSize = arrayProp.arraySize;
        arrayProp.arraySize = requiredLength;

        for (var i = previousSize; i < requiredLength; i++)
        {
            arrayProp.GetArrayElementAtIndex(i).stringValue = string.Empty;
        }
    }

    #endregion
}
