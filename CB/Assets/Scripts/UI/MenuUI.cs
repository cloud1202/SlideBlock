using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public class MenuUI : CloseBaseUI
{
    [SerializeField] private SlideToggle _symbolToggle;
    private IBaseUI _inquriyUI;

    private GameManager m_gameManager;
    private PrefabManager m_prefabManager;

    [Inject]
    public void Construct(GameManager gameManager, PrefabManager prefabManager)
    {
        m_gameManager = gameManager;
        m_prefabManager = prefabManager;
    }
    private void Awake()
    {
        _symbolToggle.OnValueChanged += OnSymbolToggleChanged;
    }
    protected override void OnDestroyed()
    {
        _symbolToggle.OnValueChanged -= OnSymbolToggleChanged;
    }

    public override void Init()
    {
        _symbolToggle.SetValueWithoutNotify(m_gameManager.IsSymbolOn);
        InitLoadUI().Forget();
    }

    async private UniTask InitLoadUI()
    {
        if (_inquriyUI == null)
            _inquriyUI = await m_prefabManager.InstantiateDynamicUI<IBaseUI>(PrefabData.InquriyUI, this.transform);

        base.Init();
    }

    private void OnSymbolToggleChanged(bool value)
    {
        m_gameManager.IsSymbolOn = value;
    }


    public void OnClickInquriyBtn()
    {
        _inquriyUI.Init();
    }

    public void OnClickRetryBtn()
    {
        m_gameManager.StartRound().Forget();
        OnClickCloseBtn();
    }

    public void OnClickHomeBtn()
    {
        m_gameManager.ExitRound();
        OnClickCloseBtn();
    }

    public override void Close()
    {
        _inquriyUI?.Close();
        base.Close();
    }

    public void OnClickCloseBtn()
    {
        Close();
    }
}
