using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : SingletonInstance<GameManager>, IManager
{
    public int HighScore { get; private set; }
    private IRound _roundManager;
    private IBaseUI _lobbyUI;

    async public UniTask Bootstrap()
    {
        ResolutionScreen.InitResolution();
        await AddressableManager.Instance.SetAddressable();
        await PrefabManager.Instance.LoadAssetReference();
        await SoundManager.Instance.LoadAssetReference();
        await PrefabManager.Instance.InitLoadObjects();
        _lobbyUI = await PrefabManager.Instance.InstantiateStaticUI<IBaseUI>(PrefabData.LobbyUI);

        _lobbyUI.Init();

        InputManager.Instance.SubscribeToInputHandler(InputType.Game_Exit, OnClickExit);
    }

    async public UniTask StartRound()
    {
        if (_roundManager == null)
        {
            _roundManager = await PrefabManager.Instance.InstantiateObject<IRound>(PrefabData.RoundManager);
            await _roundManager.Init();
        }

        HighScore = await FirebaseManager.Instance.GetField(SaveFieldType.HighScore_Classic, 0);
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

}
