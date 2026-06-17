using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IRound
{
    public UniTask Init();

    public void EnterRound();

    public void EndRound();

    public void ExitRound();
    public void DestroyMatchBricks(int addScore, Vector2 boundCenter);
}
