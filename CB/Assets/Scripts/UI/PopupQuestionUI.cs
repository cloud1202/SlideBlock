using System;
using TMPro;
using UnityEngine;

public class PopupQuestionUI : BaseUI, IPopupQuestion
{
    [SerializeField] private TextMeshProUGUI _content;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Vector2 _minSize;
    [SerializeField] private Vector2 _maxSize;
    [SerializeField] private Vector2 _padding;
    private Action _onClickYes;
    private Action _onClickNo;

    public void SetNoticeContent(GameTextData content)
    {
        _content.text = TextDataManager.Instance.GetGameText(content);
        _panel.sizeDelta = Utility.UpdateLayoutSize(_content, _minSize, _maxSize, _padding);
    }

    public void RegistQuestionAction(Action onClickYesAction = null, Action onClickNoAction = null)
    {
        _onClickYes = onClickYesAction;
        _onClickNo = onClickNoAction;
        base.Init();
    }

    public void OnClickCloseBtn()
    {
        base.Close();
    }
    public void OnClickYesBtn()
    {
        _onClickYes?.Invoke();
        OnClickCloseBtn();
    }
    public void OnClickNoBtn()
    {
        _onClickNo?.Invoke();
        OnClickCloseBtn();
    }
}
