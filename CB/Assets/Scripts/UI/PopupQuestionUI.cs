using System;
using TMPro;
using UnityEngine;
using VContainer;

public class PopupQuestionUI : CloseBaseUI, IPopupQuestion
{
    [SerializeField] private TextMeshProUGUI _content;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Vector2 _minSize;
    [SerializeField] private Vector2 _maxSize;
    [SerializeField] private Vector2 _padding;
    private Action _onClickYes;
    private Action _onClickNo;

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

    public void RegistQuestionAction(Action onClickYesAction = null, Action onClickNoAction = null)
    {
        _onClickYes = onClickYesAction;
        _onClickNo = onClickNoAction;
        Init();
    }

    public override void Close()
    {
        _onClickNo?.Invoke();
        base.Close();
        Destroy(this.gameObject);
    }

    public void OnClickCloseBtn()
    {
        Close();
    }

    public void OnClickYesBtn()
    {
        _onClickYes?.Invoke();
        _onClickNo = null;
        Close();
    }

    public void OnClickNoBtn()
    {
        Close();
    }
}
