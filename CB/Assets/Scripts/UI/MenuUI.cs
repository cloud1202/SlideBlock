using Cysharp.Threading.Tasks;
using UnityEngine;

public class MenuUI : BaseUI
{
    [SerializeField] private SlideToggle _bgmToggle;
    [SerializeField] private SlideToggle _sfxToggle;

    private bool _initBGM;
    private bool _initSFX;

    private bool _isDirty;
    public override void Init()
    {
        InputManager.Instance.UseInputHandler = false;

        _initBGM = SoundManager.Instance.IsBGMOn;
        _initSFX = SoundManager.Instance.IsSFXOn;

        _bgmToggle.SetValueWithoutNotify(_initBGM);
        _sfxToggle.SetValueWithoutNotify(_initSFX);

        _bgmToggle.OnValueChanged += OnBGMToggleChanged;
        _sfxToggle.OnValueChanged += OnSFXToggleChanged;

        base.Init();
    }

    private void SaveField()
    {
        _isDirty |= _initBGM != _bgmToggle.IsOn;
        _isDirty |= _initSFX != _sfxToggle.IsOn;

        if (!_isDirty)
            return;

        FirebaseManager.Instance.SaveField(new SaveFieldType[] {
                SaveFieldType.IsBGMOn,
                SaveFieldType.IsSFXOn,
                SaveFieldType.IsVibOn
            },
            new int[]{
                _bgmToggle.IsOn.GetHashCode(),
                _sfxToggle.IsOn.GetHashCode(),
            });
        PlayerPrefs.Save();
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
        PlayerPrefs.Save();
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
        SaveField();
        base.Close();
        InputManager.Instance.UseInputHandler = true;
    }
}
