using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using static SoundManager;

[RequireComponent(typeof(AudioSource))]
public class SoundEmitter : MonoBehaviour
{
    [SerializeField] private SoundData _soundType;
    private AudioSource _audioSource;
    private float _initVolum;
    private SoundManager m_soundManager;
    [Inject]
    public void Construct(SoundManager soundManager)
    {
        m_soundManager = soundManager;
        SetAudioClip().Forget();
        m_soundManager.SubscribeToSoundHandler(UpdateVolum);
    }
    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _initVolum = _audioSource.volume;
    }

    async private UniTask SetAudioClip()
    {
        _audioSource.clip = await m_soundManager.LoadAsset<AudioClip>(EnumConverter.Enum32ToInt(_soundType));
    }

    private void OnDestroy()
    {
        m_soundManager.UnsubscribeToSoundHandler(UpdateVolum);
    }

    private void UpdateVolum(float volumPer)
    {
        _audioSource.volume = _initVolum * volumPer;
    }

    public void PlaySound()
    {
        _audioSource.Play();
    }

    public void FadeSound(float value, float duration)
    {
        m_soundManager.FadeSound(_audioSource, value, duration);
    }
}
