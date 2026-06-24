using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameLobbyUI : BaseUI
{
    [SerializeField] RectTransform _logo;
    [SerializeField] RectTransform _btnGroup;

    private float _initLogoRatio;
    private float _initBtnGroupRatio;
    public override void Init()
    {
        SoundManager.Instance.PlayBGM(SoundData.Lobby).Forget();
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
