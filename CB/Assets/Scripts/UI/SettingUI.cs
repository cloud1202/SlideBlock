using UnityEngine;

public class SettingUI : BaseUI
{
    public override void Init()
    {
        base.Init();
    }

    public void OnClickBackBtn()
    {
        OnClickCloseBtn();
    }

    public void OnClickCloseBtn()
    {
        Destroy(this.gameObject);
    }
}
