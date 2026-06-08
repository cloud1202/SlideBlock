using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public class GameManager : SingletonInstance<GameManager>, IManager
{
    async public UniTask Bootstrap()
    {
        await AddressableManager.Instance.SetAddressable();
        await PrefabManager.Instance.LoadAssetReference();
        await PrefabManager.Instance.LoadCanvas();
        await PrefabManager.Instance.LoadLobbyUI();
    }

    public async UniTask StartGame()
    {
        //await PlayerManager.Instance.SpawnLocalPlayer();
    }
}
