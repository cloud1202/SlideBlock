using Cysharp.Threading.Tasks;
using UnityEngine;

public class MenuUI : BaseUI
{
    [SerializeField] private SlideToggle _bgmToggle;
    [SerializeField] private SlideToggle _sfxToggle;

    public override void Init()
    {
        InputManager.Instance.UseInputHandler = false;

        _bgmToggle.SetValueWithoutNotify(SoundManager.Instance.IsBGMOn);
        _sfxToggle.SetValueWithoutNotify(SoundManager.Instance.IsSFXOn);

        _bgmToggle.OnValueChanged += OnBGMToggleChanged;
        _sfxToggle.OnValueChanged += OnSFXToggleChanged;

        base.Init();
    }

    private void OnDestroy()
    {
        _bgmToggle.OnValueChanged -= OnBGMToggleChanged;
        _sfxToggle.OnValueChanged -= OnSFXToggleChanged;
    }

    private void OnBGMToggleChanged(bool value)
    {
        SoundManager.Instance.IsBGMOn = value;
    }

    private void OnSFXToggleChanged(bool value)
    {
        SoundManager.Instance.IsSFXOn = value;
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
        base.Close();
        InputManager.Instance.UseInputHandler = true;
    }
}
