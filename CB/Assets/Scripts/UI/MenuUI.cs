using Cysharp.Threading.Tasks;
using UnityEngine;

public class MenuUI : BaseUI
{
    [SerializeField] private SlideToggle _symbolToggle;
    private IBaseUI _inquriyUI;

    private void Awake()
    {
        //_symbolToggle.SetValueWithoutNotify(GameManager.Instance.IsSymbolOn);
        _symbolToggle.OnValueChanged += OnSymbolToggleChanged;
    }

    private void OnDestroy()
    {
        _symbolToggle.OnValueChanged -= OnSymbolToggleChanged;
    }

    public override void Init()
    {
        InputManager.Instance.UseInputHandler = false;
        InitLoadUI().Forget();
    }

    async private UniTask InitLoadUI()
    {
        if (_inquriyUI == null)
            _inquriyUI = await PrefabManager.Instance.InstantiateDynamicUI<IBaseUI>(PrefabData.InquriyUI, this.transform);

        base.Init();
    }

    private void OnSymbolToggleChanged(bool value)
    {
        //GameManager.Instance.IsSymbolOn = value;
    }


    public void OnClickInquriyBtn()
    {
        _inquriyUI.Init();
    }

    public void OnClickRetryBtn()
    {
       // GameManager.Instance.StartRound().Forget();
        OnClickCloseBtn();
    }

    public void OnClickHomeBtn()
    {
        //GameManager.Instance.ExitRound();
        OnClickCloseBtn();
    }

    public void OnClickCloseBtn()
    {
        base.Close();
        InputManager.Instance.UseInputHandler = true;
    }
}
