using System;
using UnityEngine;

public interface IPopupQuestion : IBaseUI
{
    public void Init(Action onClickYesAction = null, Action onClickNoAction = null);
}
