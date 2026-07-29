using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using VContainer;

public class GameLobbyUI : BaseUI
{
    [SerializeField] RectTransform _logo;
    [SerializeField] RectTransform _btnGroup;
    [SerializeField] TextMeshProUGUI _version;
    
    private float _initLogoRatio;
    private float _initBtnGroupRatio;

    private IBaseUI _legalUI;

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

    public override void Init()
    {
        _version.text = Application.version;

        m_soundManager.PlayBGM(SoundData.Lobby).Forget();
        InitLoadUI().Forget();
    }

    async private UniTask InitLoadUI()
    {
        if (_legalUI == null)
            _legalUI = await m_prefabManager.InstantiateDynamicUI<IBaseUI>(PrefabData.LegalUI);

        base.Init();
    }
    private void Awake()
    {
        _initLogoRatio = _logo.anchoredPosition.y / ResolutionScreen.REF_HEIGHT;
        _initBtnGroupRatio = _btnGroup.anchoredPosition.y / ResolutionScreen.REF_HEIGHT;

        ResolutionScreen.Subscribe(ChangeResolution);
    }
    private void OnDestroy()
    {
        ResolutionScreen.Unsubscribe(ChangeResolution);
    }

    public void OnClickLegal()
    {
        _legalUI?.Init();
    }


    public void OnCLickLeaderboard()
    {
        FirebaseManager.Instance.ShowLeaderboardUI().Forget();
    }


    public void OnClickClassicBtn()
    {
       // GameManager.Instance.StartRound().Forget();
    }

    private void ChangeResolution(float width, float height, float scaleFactor)
    {
        var canvasHeight = m_prefabManager.MainCanvas.rect.height;
        _logo.anchoredPosition = new Vector2(0, canvasHeight * _initLogoRatio);
        _btnGroup.anchoredPosition = new Vector2(0, canvasHeight * _initBtnGroupRatio);
    }
}
