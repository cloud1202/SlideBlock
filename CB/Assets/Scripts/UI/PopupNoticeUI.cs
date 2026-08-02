using TMPro;
using UnityEngine;
using VContainer;

public class PopupNoticeUI : CloseBaseUI, IPopupNotice
{
    [SerializeField] private TextMeshProUGUI _content;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Vector2 _minSize;
    [SerializeField] private Vector2 _maxSize;
    [SerializeField] private Vector2 _padding;

    protected TextDataManager m_textDataManager;
    [Inject]
    public void Construct(TextDataManager textDataManager)
    {
        m_textDataManager = textDataManager;
    }

    public void SetNoticeContent(GameTextData content)
    {
        _content.text = m_textDataManager.GetGameText(content);
        _panel.sizeDelta = Utility.UpdateLayoutSize(_content, _minSize, _maxSize, _padding);
    }
}
