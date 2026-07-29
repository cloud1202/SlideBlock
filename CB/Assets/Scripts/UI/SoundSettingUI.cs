using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public class SoundSettingUI : MonoBehaviour
{
    [SerializeField] private SlideToggle _bgmToggle;
    [SerializeField] private SlideToggle _sfxToggle;

    private GameManager m_gameManager;
    private PrefabManager m_prefabManager;
    private SoundManager m_soundManager;

    [Inject]
    public void Construct(GameManager gameManager, PrefabManager prefabManager, SoundManager soundManager)
    {
        m_gameManager = gameManager;
        m_prefabManager = prefabManager;
        m_soundManager = soundManager;
    }
    private void Awake()
    {
        _bgmToggle.SetValueWithoutNotify(m_soundManager.IsBGMOn);
        _sfxToggle.SetValueWithoutNotify(m_soundManager.IsSFXOn);

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
        m_soundManager.IsBGMOn = value;
    }

    private void OnSFXToggleChanged(bool value)
    {
        m_soundManager.IsSFXOn = value;
    }
}
