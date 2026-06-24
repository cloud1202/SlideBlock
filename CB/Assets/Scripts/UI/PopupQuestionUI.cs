using System;
using UnityEngine;

public class PopupQuestionUI : BaseUI, IPopupQuestion
{
    private Action _onClickYes;
    private Action _onClickNo;
    public void Init(Action onClickYesAction = null, Action onClickNoAction = null)
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
