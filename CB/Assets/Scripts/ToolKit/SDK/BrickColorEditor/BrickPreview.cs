using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// BrickColorEditorManager 전용 UI 블럭 프리뷰
/// 실제 게임의 Brick.cs와 분리된 에디터 툴킷용 컴포넌트
///
/// Prefab 구조:
/// BrickPreview (RectTransform + Image + BrickPreview + Button)
/// └── Highlight (Image — 선택 테두리, 기본 비활성)
/// </summary>
public class BrickPreview : MonoBehaviour
{
    [SerializeField] private Image _baseImage;
    [SerializeField] private GameObject _highlightImage;

    public Color BrickColor { get; private set; }
    public bool  IsSelected { get; private set; }

    public System.Action<BrickPreview> OnClicked;

    public void Init(Color color)
    {
        BrickColor = color;
        if (_baseImage != null) _baseImage.color = color;
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (_highlightImage != null)
            _highlightImage.SetActive(selected);
    }

    public void OnClick() => OnClicked?.Invoke(this);
}
