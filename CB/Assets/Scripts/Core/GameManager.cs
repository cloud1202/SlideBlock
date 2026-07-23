using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

public class GameManager : SingletonInstance<GameManager>, IManager
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

    public float catureEnterTime { get; set; }

    async public UniTask Bootstrap()
    {
        ResolutionScreen.InitResolution();
        InputManager.Instance.SubscribeToInputHandler(InputType.Game_Exit, OnClickExit);
        await UniTask.WaitUntil(() => FirebaseManager.Instance.IsInitialized);
        FirebaseManager.Instance.Log("AddressableManager Init");
        await AddressableManager.Instance.SetAddressable();
        FirebaseManager.Instance.Log("PrefabManager Init");
        await PrefabManager.Instance.LoadAssetReference();
        FirebaseManager.Instance.Log("SoundManager Init");
        await SoundManager.Instance.LoadAssetReference();
        FirebaseManager.Instance.Log("TextDataManager Init");
        await TextDataManager.Instance.LoadAssetReference();
        FirebaseManager.Instance.Log("PrefabManager Load");
        await PrefabManager.Instance.InitLoadObjects();
        FirebaseManager.Instance.Log("Force Update Check");
        await FirebaseManager.Instance.CheckForForceUpdateAsync();
        await UniTask.WaitUntil(() => FirebaseManager.Instance?.IsUpdate ?? false);
        _lobbyUI = await PrefabManager.Instance.InstantiateStaticUI<IBaseUI>(PrefabData.LobbyUI);
        _loadingUI = await PrefabManager.Instance.InstantiateStaticUI<IBaseUI>(PrefabData.LoadingUI);
        _lobbyUI.Init();
        _loadingUI.Init();
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
#if UNITY_ANDROID || UNITY_EDITOR
        if (PrefabManager.Instance.TryGetInstance<IPopupQuestion>(PrefabData.PopupQuestionUI, out IPopupQuestion popup))
            return;
        popup = await PrefabManager.Instance.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI);

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
            FirebaseManager.Instance.LogModePause("Classic", Time.realtimeSinceStartup - catureEnterTime, _roundManager.CurrentScore);

        FirebaseManager.Instance.Log("App paused");
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
        FirebaseManager.Instance.LogEvent("app_quit","real_time", Time.realtimeSinceStartup.ToString());
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
