using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.InputSystem.InputAction;

public class GameManager : SingletonInstance<GameManager>, IManager
{
    private IBoard _board;
    async public UniTask Bootstrap()
    {
        await AddressableManager.Instance.SetAddressable();
        await PrefabManager.Instance.LoadAssetReference();
        await PrefabManager.Instance.LoadCanvas();
        await PrefabManager.Instance.LoadLobbyUI();
        var board =  await PrefabManager.Instance.InstantiateObject<GameObject>(PrefabData.Board);
        _board = board.GetComponent<IBoard>();
        InputManager.Instance.SubscribeToInputHandler(InputType.Game_Retry, RestartGame);
    }

    public async UniTask StartGame()
    {
        //await PlayerManager.Instance.SpawnLocalPlayer();
    }

    public void RestartGame(CallbackContext context)
    {
        _board.ResetBoard();
    }
}
