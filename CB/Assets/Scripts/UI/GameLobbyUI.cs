using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameLobbyUI : BaseUI
{
    public override void Init()
    {
        SoundManager.Instance.PlayBGM(SoundData.Lobby).Forget();
        base.Init();
    }

    public void OnClickClassicBtn()
    {
        GameManager.Instance.StartRound().Forget();
    }
}
