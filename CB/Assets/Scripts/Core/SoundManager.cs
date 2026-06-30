using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

[ManagerOrder(6)]
public class SoundManager : ReferenceManager<SoundManager>, IManager
{
    private const float DEFAULT_SOUND_VOLUM = 0.5f;

    private UnityAction<float> _updateSoundVolumEvent = null;
    private UnityAction<float> _updateBGMVolumEvent = null;
    private UnityAction<float> _updateSFXVolumEvent = null;

    [SerializeField] private AudioSource _bgmAudio;
    [SerializeField] private AudioSource _sfxAudio;

    private float _soundVolumPer;
    public float SoundVolumPer
    {
        get { return _soundVolumPer; }
        set
        {
            if (_soundVolumPer == value)
                return;

            _soundVolumPer = value;
            _updateSoundVolumEvent?.Invoke(value);
        }
    }

    private float _bgmVolumPer;
    public float BGMVolumPer
    {
        get { return _bgmVolumPer; }
        set
        {
            if (_bgmVolumPer == value)
                return;

            _bgmVolumPer = value;
            _updateBGMVolumEvent?.Invoke(value);
        }
    }

    private float _sfxVolumPer;
    public float SFXVolumPer
    {
        get { return _sfxVolumPer; }
        set
        {
            if (_sfxVolumPer == value)
                return;

            _sfxVolumPer = value;
            _updateSFXVolumEvent?.Invoke(value);
        }
    }

    public bool IsBGMOn
    {
        get { return FirebaseManager.Instance.IsBGMOn; }
        set
        {
            if (FirebaseManager.Instance.IsBGMOn == value)
                return;

            FirebaseManager.Instance.IsBGMOn = value;
            _bgmAudio.mute = !value;
        }
    }

    public bool IsSFXOn
    {
        get { return FirebaseManager.Instance.IsSFXOn; }
        set
        {
            if (FirebaseManager.Instance.IsSFXOn == value)
                return;

            FirebaseManager.Instance.IsSFXOn = value;
            _sfxAudio.mute = !value;
        }
    }

    public override void Init()
    {
        base.Init();
        CreateBGMAudio();
        CreateSFXAudio();

        LoadSaveFieldData().Forget();
        //_updateSoundVolumEvent += UpdateVolum;
        //_updateBGMVolumEvent += UpdateBGMVolum;
        //_updateSFXVolumEvent += UpdateSFXVolum;
        SoundVolumPer = 0.5f;
    }

    async private UniTask LoadSaveFieldData()
    {
        await UniTask.WaitUntil(() => FirebaseManager.Instance.IsLoadData);
        _bgmAudio.mute = !FirebaseManager.Instance.IsBGMOn;
        _sfxAudio.mute = !FirebaseManager.Instance.IsSFXOn;
    }

    async public override UniTask LoadAssetReference()
    {
        var assets = await AddressableManager.Instance.LoadResourceData<SoundAssetReference>(nameof(SoundAssetReference));
        _assetDatas = assets.assetDatas;
        await base.LoadAssetReference();
    }

    private void CreateBGMAudio()
    {
        var go = new GameObject("BGM");
        _bgmAudio = go.AddComponent<AudioSource>();
        _bgmAudio.playOnAwake = false;
        _bgmAudio.loop = true;
        _bgmAudio.volume = 1.0f;
        go.transform.SetParent(this.transform);
    }

    private void CreateSFXAudio()
    {
        var go = new GameObject("SFX");
        _sfxAudio = go.AddComponent<AudioSource>();
        _sfxAudio.playOnAwake = false;
        _sfxAudio.volume = 1.0f;
        go.transform.SetParent(this.transform);
    }

    private void UpdateVolum(float volumPer)
    {
        _bgmAudio.volume = DEFAULT_SOUND_VOLUM * _bgmVolumPer * volumPer;
        _sfxAudio.volume = DEFAULT_SOUND_VOLUM * _sfxVolumPer * volumPer;
    }

    private void UpdateBGMVolum(float volumPer)
    {
        _bgmAudio.volume = DEFAULT_SOUND_VOLUM  * volumPer;
    }

    private void UpdateSFXVolum(float volumPer)
    {
        _sfxAudio.volume = DEFAULT_SOUND_VOLUM  * volumPer;
    }

    async public UniTask PlayBGM(SoundData sound = SoundData.None)
    {
        if(sound == SoundData.None)
        {
            _bgmAudio.clip = null;
        }
        else
        {
            var clip = await LoadAsset<AudioClip>(EnumConverter.Enum32ToInt(sound));
            await FadeBGM(true);
            _bgmAudio.clip = clip;
        }
        _bgmAudio.Play();
        FadeBGM(false).Forget();
    }

    async public UniTask PlaySFX(SoundData sound, CancellationToken ct = new CancellationToken())
    {
        var clip = await LoadAsset<AudioClip>(EnumConverter.Enum32ToInt(sound), ct);

        _sfxAudio.PlayOneShot(clip);
    }

    async private UniTask FadeBGM(bool isFadeOut)
    {
        if (_bgmAudio.clip == null)
            return;
        float endValue = isFadeOut ? 0f : 1f;
        var tween = DOTween.To(() => _bgmAudio.volume, vol => _bgmAudio.volume = vol, endValue, 0.5f);

        await UniTask.WaitWhile(() => tween.active);
    }

    public void SubscribeToSoundHandler(UnityAction<float> subscribeEvent)
    {
        if (subscribeEvent == null)
            return;

        _updateSoundVolumEvent += subscribeEvent;
        subscribeEvent(SoundVolumPer);
    }

    public void UnsubscribeToSoundHandler(UnityAction<float> subscribeEvent)
    {
        _updateSoundVolumEvent -= subscribeEvent;
    }
}
