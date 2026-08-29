using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

public interface IRound
{
    public int CurrentScore { get; }
    public event Action OnUpdateSymbolState;
    public UniTask Init();

    public void ChangeSymbolState();

    public void EnterRound();

    public void EndRound();

    public void ExitRound();
    public void DestroyMatchBricks(int addScore, Vector2 boundCenter);
}
