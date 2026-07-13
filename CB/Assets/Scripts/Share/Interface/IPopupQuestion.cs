using System;
using UnityEngine;

public interface IPopupQuestion : IBaseUI, IPopupNotice
{
    public void RegistQuestionAction(Action onClickYesAction = null, Action onClickNoAction = null);
}
