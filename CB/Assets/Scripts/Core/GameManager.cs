using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameManager : SingletonInstance<GameManager>, IManager
{
    public int HighScore { get; private set; }
    private IRound _roundManager;
    private IBaseUI _lobbyUI;
    async public UniTask Bootstrap()
    {
        HighScore = PlayerPrefs.GetInt("HighScore",0);
        await AddressableManager.Instance.SetAddressable();
        await PrefabManager.Instance.LoadAssetReference();
        await PrefabManager.Instance.InitLoadObjects();
        _lobbyUI = await PrefabManager.Instance.InstantiateUI<IBaseUI>(PrefabData.LobbyUI);

        _lobbyUI.Init();
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

    public void ScoreApply(int score)
    {
    }
}
