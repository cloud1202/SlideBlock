#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(AssetReferenceBase<,>), true)]
public class AssetReferenceEditor : Editor
{
    // ── 직렬화 프로퍼티 ──────────────────────────────
    private SerializedProperty _assetDatas;
    private ReorderableList _list;
    private string _referenceName;

    // ── 검색 ────────────────────────────────────────
    private string _searchQuery = "";

    // ── 컬럼 너비 ────────────────────────────────────
    private const float ID_WIDTH = 160f;
    private const float LABEL_WIDTH = 130f;
    private const float ADDR_TAG_WIDTH = 16f;   // Addressable 라벨 뱃지 하나
    private const float ROW_HEIGHT = 20f;
    private const float PAD = 4f;

    // ── 색상 ────────────────────────────────────────
    private static readonly Color ColorUnassigned = new Color(1f, 0.35f, 0.35f, 0.25f);
    private static readonly Color ColorDuplicate = new Color(1f, 0.75f, 0f, 0.25f);
    private static readonly Color ColorNormal = new Color(0f, 0f, 0f, 0f);

    // ── 캐시 ────────────────────────────────────────
    private Dictionary<int, List<string>> _addressableLabelsCache = new();
    private double _lastCacheTime;
    private const double CACHE_INTERVAL = 2.0;

    // ────────────────────────────────────────────────
    protected void OnEnable()
    {
        _assetDatas = serializedObject.FindProperty("assetDatas");
        _referenceName = serializedObject.FindProperty("Name")?.stringValue ?? target.name;

        BuildList();
        RefreshAddressableLabels();
    }

    // ── ReorderableList 구성 ─────────────────────────
    private void BuildList()
    {
        _list = new ReorderableList(serializedObject, _assetDatas,
            draggable: true, displayHeader: true,
            displayAddButton: true, displayRemoveButton: true);

        _list.drawHeaderCallback = DrawHeader;
        _list.drawElementCallback = DrawElement;
        _list.elementHeightCallback = _ => ROW_HEIGHT + PAD;
        _list.onAddCallback = OnAdd;
    }

    // ── 헤더 ────────────────────────────────────────
    private void DrawHeader(Rect rect)
    {
        float dataWidth = rect.width - ID_WIDTH - LABEL_WIDTH - PAD * 2;

        EditorGUI.LabelField(new Rect(rect.x, rect.y, ID_WIDTH, rect.height), "ID", EditorStyles.boldLabel);
        EditorGUI.LabelField(new Rect(rect.x + ID_WIDTH + PAD, rect.y, dataWidth, rect.height), "Data", EditorStyles.boldLabel);
        EditorGUI.LabelField(new Rect(rect.x + ID_WIDTH + dataWidth + PAD * 2, rect.y, LABEL_WIDTH, rect.height), "Label", EditorStyles.boldLabel);
    }

    // ── 행 그리기 ────────────────────────────────────
    private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty element = _assetDatas.GetArrayElementAtIndex(index);
        SerializedProperty idProp = element.FindPropertyRelative("id");
        SerializedProperty dataProp = element.FindPropertyRelative("data");
        SerializedProperty lblProp = element.FindPropertyRelative("containLabel");

        // 검색 필터 — 일치하지 않으면 빈 행으로 스킵
        string idName = GetEnumName(idProp);
        if (!string.IsNullOrEmpty(_searchQuery) &&
            !idName.ToLower().Contains(_searchQuery.ToLower()))
        {
            return;
        }

        float dataWidth = rect.width - ID_WIDTH - LABEL_WIDTH - PAD * 2;
        float y = rect.y + PAD * 0.5f;
        float h = ROW_HEIGHT;

        // ── 행 배경 하이라이트 ──────────────────────
        bool isUnassigned = IsUnassigned(dataProp);
        bool isDuplicate = IsDuplicate(index, idProp);

        Color bg = isUnassigned ? ColorUnassigned
                 : isDuplicate ? ColorDuplicate
                 : ColorNormal;

        if (bg.a > 0f)
            EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y, rect.width + 4, rect.height), bg);

        // ── ID ─────────────────────────────────────
        Rect idRect = new Rect(rect.x, y, ID_WIDTH, h);
        EditorGUI.PropertyField(idRect, idProp, GUIContent.none);

        // ── Data ───────────────────────────────────
        Rect dataRect = new Rect(rect.x + ID_WIDTH + PAD, y, dataWidth, h);
        EditorGUI.PropertyField(dataRect, dataProp, GUIContent.none);

        // ── Addressable 라벨 뱃지 ──────────────────
        DrawAddressableLabels(rect, dataRect, dataProp, dataWidth, y, h);

        // ── ContainLabel ───────────────────────────
        Rect lblRect = new Rect(rect.x + ID_WIDTH + dataWidth + PAD * 2, y, LABEL_WIDTH, h);
        ContainLabel lblVal = (ContainLabel)lblProp.intValue;
        lblVal = (ContainLabel)EditorGUI.EnumFlagsField(lblRect, lblVal);
        lblProp.intValue = (int)lblVal;
    }

    // ── Addressable 라벨 뱃지 표시 ──────────────────
    private void DrawAddressableLabels(Rect fullRect, Rect dataRect, SerializedProperty dataProp,
                                       float dataWidth, float y, float h)
    {
        string guid = dataProp.FindPropertyRelative("m_AssetGUID")?.stringValue;
        if (string.IsNullOrEmpty(guid)) return;

        if (!_addressableLabelsCache.TryGetValue(guid.GetHashCode(), out var labels)) return;
        if (labels == null || labels.Count == 0) return;

        // 뱃지를 Data 필드 오른쪽 끝에 겹쳐서 표시
        float badgeX = dataRect.x + dataWidth - (ADDR_TAG_WIDTH + 2) * labels.Count - 2;
        foreach (var lbl in labels)
        {
            var badgeRect = new Rect(badgeX, y + 1, ADDR_TAG_WIDTH * 3.5f, h - 2);
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { background = MakeTex(2, 2, new Color(0.2f, 0.6f, 1f, 0.7f)) },
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(badgeRect, lbl, style);
            badgeX += ADDR_TAG_WIDTH * 3.5f + 2;
        }
    }

    // ── 요소 추가 ────────────────────────────────────
    private void OnAdd(ReorderableList list)
    {
        int newIndex = list.serializedProperty.arraySize;
        list.serializedProperty.arraySize++;
        list.index = newIndex;

        SerializedProperty newEl = list.serializedProperty.GetArrayElementAtIndex(newIndex);
        newEl.FindPropertyRelative("id").enumValueIndex = newIndex;
        newEl.FindPropertyRelative("data")
             .FindPropertyRelative("m_AssetGUID").stringValue = "";
    }

    // ── Inspector GUI ────────────────────────────────
    public override void OnInspectorGUI()
    {
        // Addressable 캐시 주기적 갱신
        if (EditorApplication.timeSinceStartup - _lastCacheTime > CACHE_INTERVAL)
            RefreshAddressableLabels();

        serializedObject.Update();
        EditorGUI.BeginChangeCheck();

        // ── 툴바: 검색 + 버튼 ──────────────────────
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("🔍", GUILayout.Width(18));
        _searchQuery = EditorGUILayout.TextField(_searchQuery, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("중복 재할당", EditorStyles.toolbarButton, GUILayout.Width(80)))
            ReassignDuplicateIds();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // ── 리스트 ─────────────────────────────────
        _list.DoLayoutList();

        // ── 경고 메시지 ────────────────────────────
        DrawWarnings();

        if (EditorGUI.EndChangeCheck())
            serializedObject.ApplyModifiedProperties();
    }

    // ── 경고 메시지 ──────────────────────────────────
    private void DrawWarnings()
    {
        var seenIds = new HashSet<int>();
        var dupIds = new HashSet<int>();
        int unassigned = 0;

        for (int i = 0; i < _assetDatas.arraySize; i++)
        {
            var el = _assetDatas.GetArrayElementAtIndex(i);
            int id = el.FindPropertyRelative("id").enumValueIndex;
            var data = el.FindPropertyRelative("data");

            if (!seenIds.Add(id)) dupIds.Add(id);
            if (IsUnassigned(data)) unassigned++;
        }

        if (unassigned > 0)
            EditorGUILayout.HelpBox($"미할당 에셋 {unassigned}개가 있습니다.", MessageType.Warning);

        if (dupIds.Count > 0)
            EditorGUILayout.HelpBox($"중복 ID {dupIds.Count}개 발견. '중복 재할당' 버튼으로 정리하세요.", MessageType.Warning);
    }

    // ── 중복 ID 자동 재할당 ──────────────────────────
    private void ReassignDuplicateIds()
    {
        serializedObject.Update();

        var seenIds = new HashSet<int>();
        int maxEnum = GetMaxEnumValue();

        for (int i = 0; i < _assetDatas.arraySize; i++)
        {
            var idProp = _assetDatas.GetArrayElementAtIndex(i).FindPropertyRelative("id");
            int cur = idProp.enumValueIndex;

            if (!seenIds.Add(cur))
            {
                // 비어있는 다음 인덱스 탐색
                for (int next = 0; next <= maxEnum; next++)
                {
                    if (!seenIds.Contains(next))
                    {
                        idProp.enumValueIndex = next;
                        seenIds.Add(next);
                        break;
                    }
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
        Debug.Log("[AssetReferenceEditor] 중복 ID 재할당 완료");
    }

    // ── Addressable 라벨 캐시 갱신 ──────────────────
    private void RefreshAddressableLabels()
    {
        _lastCacheTime = EditorApplication.timeSinceStartup;
        _addressableLabelsCache.Clear();

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;

        for (int i = 0; i < _assetDatas.arraySize; i++)
        {
            var dataProp = _assetDatas.GetArrayElementAtIndex(i).FindPropertyRelative("data");
            string guid = dataProp.FindPropertyRelative("m_AssetGUID")?.stringValue;
            if (string.IsNullOrEmpty(guid)) continue;

            var entry = settings.FindAssetEntry(guid);
            if (entry == null) continue;

            int key = guid.GetHashCode();
            _addressableLabelsCache[key] = entry.labels.ToList();
        }
    }

    // ── 유틸 ────────────────────────────────────────
    private bool IsUnassigned(SerializedProperty dataProp)
    {
        string guid = dataProp.FindPropertyRelative("m_AssetGUID")?.stringValue;
        return string.IsNullOrEmpty(guid);
    }

    private bool IsDuplicate(int index, SerializedProperty idProp)
    {
        int cur = idProp.enumValueIndex;
        for (int i = 0; i < _assetDatas.arraySize; i++)
        {
            if (i == index) continue;
            if (_assetDatas.GetArrayElementAtIndex(i)
                           .FindPropertyRelative("id").enumValueIndex == cur)
                return true;
        }
        return false;
    }

    private string GetEnumName(SerializedProperty idProp)
    {
        var names = idProp.enumNames;
        int idx = idProp.enumValueIndex;
        return (names != null && idx >= 0 && idx < names.Length) ? names[idx] : idx.ToString();
    }

    private int GetMaxEnumValue()
    {
        var idProp = _assetDatas.arraySize > 0
            ? _assetDatas.GetArrayElementAtIndex(0).FindPropertyRelative("id")
            : null;
        return idProp?.enumNames?.Length - 1 ?? 64;
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var tex = new Texture2D(w, h);
        var pixels = Enumerable.Repeat(col, w * h).ToArray();
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
#endif