using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class PopupNoticeUI : BaseUI, IPopupNotice
{
    [SerializeField] private TextMeshProUGUI _content;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Vector2 _minSize;
    [SerializeField] private Vector2 _maxSize;
    [SerializeField] private Vector2 _padding;

    public void SetNoticeContent(GameTextData content)
    {
        _content.text = TextDataManager.Instance.GetGameText(content);
        _panel.sizeDelta = Utility.UpdateLayoutSize(_content, _minSize, _maxSize, _padding);
    }
}
