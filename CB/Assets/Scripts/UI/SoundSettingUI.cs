using Cysharp.Threading.Tasks;
using UnityEngine;

public class SoundSettingUI : MonoBehaviour
{
    [SerializeField] private SlideToggle _bgmToggle;
    [SerializeField] private SlideToggle _sfxToggle;

    private void Awake()
    {
        _bgmToggle.SetValueWithoutNotify(SoundManager.Instance.IsBGMOn);
        _sfxToggle.SetValueWithoutNotify(SoundManager.Instance.IsSFXOn);

        _bgmToggle.OnValueChanged += OnBGMToggleChanged;
        _sfxToggle.OnValueChanged += OnSFXToggleChanged;
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
}
