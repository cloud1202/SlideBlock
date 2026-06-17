using Cysharp.Threading.Tasks;
using UnityEngine;

public class MenuUI : BaseUI
{
    public override void Init()
    {
        base.Init();
    }

    public void OnClickRetryBtn()
    {
        GameManager.Instance.StartRound().Forget();
        OnClickCloseBtn();
    }

    public void OnClickHomeBtn()
    {
        GameManager.Instance.ExitRound();
        OnClickCloseBtn();
    }

    public void OnClickCloseBtn()
    {
        gameObject.SetActive(false);
    }
}
