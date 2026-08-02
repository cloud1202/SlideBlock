using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

public class GameManager : BaseManager
{
    private FirebaseManager m_firebaseManager;
    private InputManager m_inputManger;
    private PrefabManager m_prefabManager;
    public GameManager(ManagerInitTracker tracker, FirebaseManager firebaseManager, InputManager inputManger, PrefabManager prefabManager) : base(tracker)
    {
        LLogger.Log("GameManager");
        m_firebaseManager = firebaseManager;
        m_inputManger = inputManger;
        m_prefabManager = prefabManager;
        Bootstrap().Forget();
    }

    public int HighScore
    {
        get => m_firebaseManager.ClassicScore;

        set
        {
            if (m_firebaseManager.ClassicScore == value)
                return;

            m_firebaseManager.ClassicScore = value;
        }
    }
    public bool IsSymbolOn
    {
        get => m_firebaseManager.IsSymbolOn;

        set
        {
            if (m_firebaseManager.IsSymbolOn == value)
                return;

            m_firebaseManager.IsSymbolOn = value;
            _roundManager?.ChangeSymbolState();
        }
    }

    public LanguageType Language = LanguageType.English;
    private IRound _roundManager;
    private IBaseUI _lobbyUI;
    private IBaseUI _loadingUI;

    public float catureEnterTime { get; set; }

    async public UniTask Bootstrap()
    {
        LLogger.Log("Bootstrap");
        ResolutionScreen.InitResolution();
        //await UniTask.WaitUntil(() => m_firebaseManager.IsInitialized);
        //m_firebaseManager.Log("AddressableManager Init");
        //await AddressableManager.Instance.SetAddressable();
        //m_firebaseManager.Log("PrefabManager Init");
        //await PrefabManager.Instance.LoadAssetReference();
        //m_firebaseManager.Log("SoundManager Init");
        //await SoundManager.Instance.LoadAssetReference();
        //m_firebaseManager.Log("TextDataManager Init");
        //await TextDataManager.Instance.LoadAssetReference();
        //m_firebaseManager.Log("PrefabManager Load");
        //await PrefabManager.Instance.InitLoadObjects();
        //m_firebaseManager.Log("Force Update Check");
        //await m_firebaseManager.CheckForForceUpdateAsync();
       //await UniTask.WaitUntil(() => m_firebaseManager?.IsUpdate ?? false);
        await CheckedManagers(
            ManagerType.Addressable,
            ManagerType.Prefab,
            ManagerType.Sound,
            ManagerType.TextData,
            ManagerType.Firebase,
            ManagerType.Input
            );

        m_inputManger.SubscribeToInputHandler(InputType.Game_Exit, OnClickExit);
        CompleteInit(ManagerType.Game);
        _lobbyUI = await m_prefabManager.InstantiateStaticUI<IBaseUI>(PrefabData.LobbyUI);
        _loadingUI = await m_prefabManager.InstantiateStaticUI<IBaseUI>(PrefabData.LoadingUI);
        _lobbyUI.Init();
        _loadingUI.Init();
        await UniTask.WaitUntil(() => m_firebaseManager.IsLoadData);
        await UniTask.WaitForSeconds(2f);
        _loadingUI.Close();
    }

    async public UniTask StartRound()
    {
        if (_roundManager == null)
        {
            _roundManager = await m_prefabManager.InstantiateObject<IRound>(PrefabData.RoundManager);
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
#if UNITY_ANDROID || UNITY_EDITOR
        if (m_prefabManager.TryGetInstance<IPopupQuestion>(PrefabData.PopupQuestionUI, out IPopupQuestion popup))
            return;
        popup = await m_prefabManager.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI);

        popup.SetNoticeContent(GameTextData.POPUP_EXIT_GAME);
        popup.RegistQuestionAction(QuitGame);
#endif

    }

    private void QuitGame()
    {
        ExitRound();
        Application.Quit();
    }

    private void OnApplicationPause(bool pause)
    {
        if (!pause)
            return;

        PlayerPrefs.Save();

        if (_roundManager != null)
            m_firebaseManager.LogModePause("Classic", Time.realtimeSinceStartup - catureEnterTime, _roundManager.CurrentScore);

        m_firebaseManager.Log("App paused");
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
        m_firebaseManager.LogEvent("app_quit","real_time", Time.realtimeSinceStartup.ToString());
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
