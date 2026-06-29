using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

[ManagerOrder(0)]
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
    private IRound _roundManager;
    private IBaseUI _lobbyUI;
    private IBaseUI _loadingUI;

    async public UniTask Bootstrap()
    {
        ResolutionScreen.InitResolution();
        await AddressableManager.Instance.SetAddressable();
        await PrefabManager.Instance.LoadAssetReference();
        await SoundManager.Instance.LoadAssetReference();
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

        popup.Init(() => Application.Quit());
        
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
}
