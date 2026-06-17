using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 블럭 색상 편집 ToolKit
///
/// [UI 구조 예시]
/// Canvas
/// ├── ColorPicker
/// │   ├── ColorWheelImage         RawImage — 색상환 클릭으로 기준 색 선택
/// │   ├── RgbInputRow
/// │   │   ├── InputR              InputField (0~255)
/// │   │   ├── InputG
/// │   │   └── InputB
/// │   ├── PreviewImage            Image — 선택 색 미리보기
/// │   └── GenerateButton          Button — "7색 생성"
/// ├── GeneratedPalette            HorizontalLayoutGroup — 생성된 7개 색상 버튼
/// ├── BrickContainer              GridLayoutGroup — 추가된 블럭 목록
/// └── Controls
///     ├── DeleteButton
///     ├── SaveButton
///     └── StatusText
/// </summary>
public class BrickColorEditorManager : MonoBehaviour
{
    [Header("Color Picker")]
    [SerializeField] private RawImage   _colorWheelImage;
    [SerializeField] private TMP_InputField _inputR;
    [SerializeField] private TMP_InputField _inputG;
    [SerializeField] private TMP_InputField _inputB;
    [SerializeField] private TMP_InputField _inputHexcode;
    [SerializeField] private Image      _previewImage;
    [SerializeField] private Button     _generateButton;

    [Header("Generated Palette")]
    [SerializeField] private Transform  _generatedPaletteContainer;

    [Header("Brick Area")]
    [SerializeField] private GameObject _brickPrefab;       // Brick.prefab (UI용 Image 기반)
    [SerializeField] private Transform  _brickContainer;

    [Header("Controls")]
    [SerializeField] private Button     _deleteButton;
    [SerializeField] private Button     _saveButton;
    [SerializeField] private TextMeshProUGUI       _statusText;

    private const string COLORS_CS_PATH = "Assets/Scripts/Utility/Colors.cs";
    private const int    COLOR_COUNT     = 7;

    // ── 런타임 상태 ──────────────────────────────────
    private Color              _pickedColor     = Color.red;
    private Color[]            _generatedColors = new Color[COLOR_COUNT];
    private List<BrickPreview> _bricks          = new();
    private BrickPreview       _selectedBrick   = null;

    // ────────────────────────────────────────────────
    private void Start()
    {
        BuildColorWheel();
        SetPickedColor(Color.red);

        _inputR.onEndEdit.AddListener(_ => OnRgbInputChanged());
        _inputG.onEndEdit.AddListener(_ => OnRgbInputChanged());
        _inputB.onEndEdit.AddListener(_ => OnRgbInputChanged());
        _inputHexcode.onEndEdit.AddListener(_ => OnHexcodeInputChanged());

        _generateButton.onClick.AddListener(GenerateAndShowPalette);
        _deleteButton.onClick.AddListener(DeleteSelectedBrick);
        _saveButton.onClick.AddListener(SaveColorSet);

        GenerateAndShowPalette();
        RefreshDeleteButton();
        SetStatus("색상환에서 기준 색을 선택하면 7가지 색이 자동 생성됩니다.");
    }

    // ── 색상환 텍스처 생성 ───────────────────────────
    private void BuildColorWheel()
    {
        if (_colorWheelImage == null) return;

        const int SIZE = 256;
        var tex    = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
        var center = new Vector2(SIZE * 0.5f, SIZE * 0.5f);
        float radius = SIZE * 0.5f;

        for (int y = 0; y < SIZE; y++)
        for (int x = 0; x < SIZE; x++)
        {
            var delta = new Vector2(x - center.x, y - center.y);
            float dist = delta.magnitude;

            if (dist > radius) { tex.SetPixel(x, y, Color.clear); continue; }

            float hue = Mathf.Atan2(delta.y, delta.x) / (Mathf.PI * 2f);
            if (hue < 0f) hue += 1f;

            tex.SetPixel(x, y, Color.HSVToRGB(hue, dist / radius, 1f));
        }

        tex.Apply();
        _colorWheelImage.texture = tex;

        // 클릭 이벤트
        var trigger = _colorWheelImage.gameObject.GetComponent<EventTrigger>()
                   ?? _colorWheelImage.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener(e => OnColorWheelClicked((PointerEventData)e));
        trigger.triggers.Add(entry);
    }

    // ── 색상환 클릭 → 색 추출 ────────────────────────
    private void OnColorWheelClicked(PointerEventData e)
    {
        RectTransform rt = _colorWheelImage.rectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rt, e.position, e.pressEventCamera, out Vector2 local);

        float u = (local.x / rt.rect.width)  + 0.5f;
        float v = (local.y / rt.rect.height) + 0.5f;

        var tex = _colorWheelImage.texture as Texture2D;
        if (tex == null) return;

        int px = Mathf.Clamp(Mathf.RoundToInt(u * tex.width),  0, tex.width  - 1);
        int py = Mathf.Clamp(Mathf.RoundToInt(v * tex.height), 0, tex.height - 1);

        Color sampled = tex.GetPixel(px, py);
        if (sampled.a < 0.01f) return;

        SetPickedColor(sampled);
    }

    // ── RGB 입력 변경 ────────────────────────────────
    private void OnHexcodeInputChanged()
    {
        ColorUtility.TryParseHtmlString(_inputHexcode.text, out Color color);
        _inputR.SetTextWithoutNotify(((int)color.r).ToString());
        _inputG.SetTextWithoutNotify(((int)color.g).ToString());
        _inputB.SetTextWithoutNotify(((int)color.b).ToString());
        SetPickedColor(color);
    }

    private void OnRgbInputChanged()
    {
        float r = ParseInput(_inputR);
        float g = ParseInput(_inputG);
        float b = ParseInput(_inputB);
        SetPickedColor(new Color(r, g, b));
    }

    private float ParseInput(TMP_InputField field)
    {
        return float.TryParse(field.text, out float v) ? Mathf.Clamp01(v / 255f) : 0f;
    }

    // ── 선택 색 설정 ─────────────────────────────────
    private void SetPickedColor(Color color)
    {
        _pickedColor = color;
        if (_previewImage != null) _previewImage.color = color;
        if (_inputR != null) _inputR.text = Mathf.RoundToInt(color.r * 255f).ToString();
        if (_inputG != null) _inputG.text = Mathf.RoundToInt(color.g * 255f).ToString();
        if (_inputB != null) _inputB.text = Mathf.RoundToInt(color.b * 255f).ToString();
    }

    private static readonly float[] RainbowHues =
{
    0f,       // 빨
    0.14f,   // 주
    0.28f,   // 노
    0.42f,   // 초
    0.56f,   // 파
    0.7f,   // 남
    0.84f,   // 보
};

    private void GenerateAndShowPalette()
    {
        Color.RGBToHSV(_pickedColor, out float h, out float s, out float v);
        //if (s < 0.3f) s = 0.85f;
        //if (v < 0.3f) v = 0.90f;

        // 선택한 Hue와 가장 가까운 무지개 인덱스 찾기
        int startIndex = 0;
        float minDist = float.MaxValue;
        for (int i = 0; i < RainbowHues.Length; i++)
        {
            float dist = Mathf.Abs(RainbowHues[i] - h);
            // 색상환은 원형이라 반대쪽 거리도 체크
            dist = Mathf.Min(dist, 1f - dist);
            if (dist < minDist)
            {
                minDist = dist;
                startIndex = i;
            }
        }

        // 가장 가까운 색부터 무지개 순서로 7색 생성
        for (int i = 0; i < COLOR_COUNT; i++)
        {
            int idx = (startIndex + i) % COLOR_COUNT;
            _generatedColors[i] = Color.HSVToRGB(RainbowHues[idx] + minDist, s, v);
        }

        BuildGeneratedPaletteButtons();
        SetStatus("7색 생성 완료. 색상 버튼을 눌러 블럭을 추가하세요.");
    }

    // ── 생성된 팔레트 버튼 표시 ──────────────────────
    private void BuildGeneratedPaletteButtons()
    {
        foreach (Transform child in _generatedPaletteContainer)
            Destroy(child.gameObject);

        for (int i = 0; i < COLOR_COUNT; i++)
        {
            var color = _generatedColors[i];
            var btnGO = new GameObject($"GenBtn_{i}");
            btnGO.transform.SetParent(_generatedPaletteContainer, false);

            btnGO.AddComponent<RectTransform>().sizeDelta = new Vector2(60f, 60f);
            btnGO.AddComponent<Image>().color = color;

            var btn = btnGO.AddComponent<Button>();
            var captured = color;
            btn.onClick.AddListener(() => SpawnBrick(captured));
        }
    }

    // ── 블럭 생성 ────────────────────────────────────
    private void SpawnBrick(Color color)
    {
        var go      = Instantiate(_brickPrefab, _brickContainer);
        var preview = go.GetComponent<BrickPreview>();

        if (preview == null)
        {
            LLogger.Log("[BrickColorEditorManager] BrickPreview 컴포넌트 없음", LLogger.LogLevel.Error);
            Destroy(go);
            return;
        }

        preview.Init(color);
        preview.OnClicked += OnBrickClicked;
        _bricks.Add(preview);
        SetStatus($"블럭 추가 (총 {_bricks.Count}개)");
    }

    // ── 블럭 클릭 ────────────────────────────────────
    private void OnBrickClicked(BrickPreview brick)
    {
        if (_selectedBrick == brick)
        {
            _selectedBrick.SetSelected(false);
            _selectedBrick = null;
        }
        else
        {
            _selectedBrick?.SetSelected(false);
            _selectedBrick = brick;
            _selectedBrick.SetSelected(true);
        }

        RefreshDeleteButton();
        SetStatus(_selectedBrick != null ? "블럭 선택됨." : "선택 해제됨.");
    }

    // ── 삭제 ─────────────────────────────────────────
    private void DeleteSelectedBrick()
    {
        if (_selectedBrick == null) return;
        _bricks.Remove(_selectedBrick);
        Destroy(_selectedBrick.gameObject);
        _selectedBrick = null;
        RefreshDeleteButton();
        SetStatus($"삭제됨 (남은 {_bricks.Count}개)");
    }

    // ── Colors.cs 저장 ───────────────────────────────
    private void SaveColorSet()
    {
        if (_bricks.Count == 0) { SetStatus("저장할 블럭이 없습니다."); return; }

        string fullPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../", COLORS_CS_PATH));

        var sb = new StringBuilder();
        sb.AppendLine("        new Color[]");
        sb.AppendLine("        {");
        foreach (var b in _bricks)
        {
            var c = b.BrickColor;
            sb.AppendLine($"            new Color({c.r:F2}f, {c.g:F2}f, {c.b:F2}f), // {ColorUtility.ToHtmlStringRGBA(c)}");
        }
        sb.Append("        },");

        string original = File.ReadAllText(fullPath);
        const string MARKER = "    };";
        int insertIdx = original.LastIndexOf(MARKER);

        if (insertIdx < 0) { SetStatus("Colors.cs MARKER 없음. 저장 실패."); return; }

        File.WriteAllText(fullPath, original.Insert(insertIdx, sb + "\n"));
        SetStatus($"Colors.cs 저장 완료 ({_bricks.Count}색)");

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    // ── 유틸 ─────────────────────────────────────────
    private void RefreshDeleteButton()
    {
        if (_deleteButton != null)
            _deleteButton.interactable = _selectedBrick != null;
    }

    private void SetStatus(string msg)
    {
        if (_statusText != null) _statusText.text = msg;
        LLogger.Log($"[BrickColorEditor] {msg}");
    }
}
