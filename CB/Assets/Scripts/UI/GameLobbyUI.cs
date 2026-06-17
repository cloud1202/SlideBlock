using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameLobbyUI : BaseUI
{

    public void OnClickClassicBtn()
    {
        GameManager.Instance.StartRound().Forget();
    }
}
