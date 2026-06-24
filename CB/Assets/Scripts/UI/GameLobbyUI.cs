using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class GameLobbyUI : BaseUI
{
    [SerializeField] RectTransform _logo;
    [SerializeField] RectTransform _btnGroup;
    [SerializeField] TextMeshProUGUI _version;
    
    private float _initLogoRatio;
    private float _initBtnGroupRatio;

    private IBaseUI _legalUI;
    public override void Init()
    {
        SoundManager.Instance.PlayBGM(SoundData.Lobby).Forget();
        InitLoadUI().Forget();
    }

    async private UniTask InitLoadUI()
    {
        if (_legalUI == null)
            _legalUI = await PrefabManager.Instance.InstantiateDynamicUI<IBaseUI>(PrefabData.LegalUI);

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


    public void OnClickClassicBtn()
    {
        GameManager.Instance.StartRound().Forget();
    }

    private void ChangeResolution(float width, float height, float scaleFactor)
    {
        var canvasHeight = PrefabManager.Instance.MainCanvas.rect.height;
        _logo.anchoredPosition = new Vector2(0, canvasHeight * _initLogoRatio);
        _btnGroup.anchoredPosition = new Vector2(0, canvasHeight * _initBtnGroupRatio);
    }
}
