using Cysharp.Threading.Tasks;
using UnityEngine;

public class MenuUI : BaseUI
{
    [SerializeField] private SlideToggle _bgmToggle;
    [SerializeField] private SlideToggle _sfxToggle;
    [SerializeField] private SlideToggle _vibToggle;

    private bool _initBGM;
    private bool _initSFX;
    private bool _initVIB;

    private bool _isDirty;
    public override void Init()
    {
        InputManager.Instance.UseInputHandler = false;

        _initBGM = SoundManager.Instance.IsBGMOn;
        _initSFX = SoundManager.Instance.IsSFXOn;
        _initVIB = SoundManager.Instance.IsVIBOn;

        _bgmToggle.SetValueWithoutNotify(_initBGM);
        _sfxToggle.SetValueWithoutNotify(_initSFX);
        _vibToggle.SetValueWithoutNotify(_initVIB);

        _bgmToggle.OnValueChanged += OnBGMToggleChanged;
        _sfxToggle.OnValueChanged += OnSFXToggleChanged;
        _vibToggle.OnValueChanged += OnVIBToggleChanged;

        base.Init();
    }

    private void SaveField()
    {
        _isDirty |= _initBGM != _bgmToggle.IsOn;
        _isDirty |= _initSFX != _sfxToggle.IsOn;
        _isDirty |= _initVIB != _vibToggle.IsOn;

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
                _vibToggle.IsOn.GetHashCode(),
            });
        PlayerPrefs.Save();
    }
    private void OnDestroy()
    {
        _bgmToggle.OnValueChanged -= OnBGMToggleChanged;
        _sfxToggle.OnValueChanged -= OnSFXToggleChanged;
        _vibToggle.OnValueChanged -= OnVIBToggleChanged;
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

    private void OnVIBToggleChanged(bool value)
    {
        SoundManager.Instance.IsVIBOn = value;
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
