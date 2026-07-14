using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

public class GameManager : IAsyncStartable, System.IDisposable
{
    public int HighScore
    {
        get => FirebaseManager.Instance.ClassicScore;

        set
        {
            if (FirebaseManager.Instance.ClassicScore == value)
                return;

            FirebaseManager.Instance.ClassicScore = value;
        }
    }
    public bool IsSymbolOn
    {
        get => FirebaseManager.Instance.IsSymbolOn;

        set
        {
            if (FirebaseManager.Instance.IsSymbolOn == value)
                return;

            FirebaseManager.Instance.IsSymbolOn = value;
            _roundManager?.ChangeSymbolState();
        }
    }

    public LanguageType Language = LanguageType.English;
    private IRound _roundManager;
    private IBaseUI _lobbyUI;
    private IBaseUI _loadingUI;

    private readonly LifetimeScope _rootScope;
    private readonly AddressableManager _addressableManager;
    private readonly PrefabManager _prefabManager;
    private readonly SoundManager _soundManager;
    private readonly TextDataManager _textDataManager;
    private readonly InputManager _inputManager;
    private readonly FirebaseManager _firebaseManager;
    private readonly AdmobManager _admobManager;


    public GameManager(
        LifetimeScope rootScope,
        AddressableManager addressableManager,
        PrefabManager prefabManager,
        SoundManager soundManager,
        TextDataManager textDataManager,
        InputManager inputManager,
        FirebaseManager firebaseManager,
        AdmobManager admobManager)
    {
        _rootScope = rootScope;
        _addressableManager = addressableManager;
        _prefabManager = prefabManager;
        _soundManager = soundManager;
        _textDataManager = textDataManager;
        _inputManager = inputManager;
        _firebaseManager = firebaseManager;
        _admobManager = admobManager;
    }
    async public UniTask Bootstrap()
    {
        ResolutionScreen.InitResolution();
        await AddressableManager.Instance.SetAddressable();
        await PrefabManager.Instance.LoadAssetReference();
        await SoundManager.Instance.LoadAssetReference();
        await TextDataManager.Instance.LoadAssetReference();
        await PrefabManager.Instance.InitLoadObjects();
        _lobbyUI = await PrefabManager.Instance.InstantiateStaticUI<IBaseUI>(PrefabData.LobbyUI);
        _loadingUI = await PrefabManager.Instance.InstantiateStaticUI<IBaseUI>(PrefabData.LoadingUI);
        _lobbyUI.Init();
        _loadingUI.Init();
        InputManager.Instance.SubscribeToInputHandler(InputType.Game_Exit, OnClickExit);
        await UniTask.WaitUntil(() => FirebaseManager.Instance.IsLoadData);
        await UniTask.WaitForSeconds(2f);
        _loadingUI.Close();
    }

    async public UniTask StartRound()
    {
        if (_roundManager == null)
        {
            _roundManager = await PrefabManager.Instance.InstantiateObject<IRound>(PrefabData.RoundManager);
            await _roundManager.Init();
        }
        _lobbyUI.Close();
        _roundManager.EnterRound();
    }

    public void ExitRound()
    {
        if (_roundManager == null)
            return;

        _roundManager.ExitRound();
        _roundManager = null;
        _lobbyUI.Init();
    }

    private void OnClickExit(InputAction.CallbackContext callback)
    {
        ShowExitToast().Forget();
    }

    async private UniTask ShowExitToast()
    {
        var popup = await PrefabManager.Instance.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI);

        popup.SetNoticeContent(GameTextData.POPUP_EXIT_GAME);
        popup.RegistQuestionAction(() => Application.Quit());
        
    }

    private void OnApplicationPause(bool pause)
    {
        //if (pause)
        //    FirebaseManager.Instance.SaveUserData();
        //
    }

    private void OnApplicationQuit()
    {
        //FirebaseManager.Instance.SaveUserData();
    }

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }

    public UniTask StartAsync(CancellationToken cancellation = default)
    {
        throw new System.NotImplementedException();
    }
}
