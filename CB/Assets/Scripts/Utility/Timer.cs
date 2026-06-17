using Cysharp.Threading.Tasks;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

public struct TimerModule
{
    private float _duration;
    private float _elapsed;
    private bool _isRunning;
    public bool IsFinished => _elapsed >= _duration;

    private Action _finishAction;
    private CancellationTokenSource _tokenSource;

    public TimerModule(float duration, Action finishAction = null)
    {
        _duration = duration;
        _elapsed = 0f;
        _isRunning = false;
        _finishAction = finishAction;
        _tokenSource = null;
    }

    public void Start()
    {
        Reset();
        _isRunning = true;
        _tokenSource = new CancellationTokenSource();
        Tick(_tokenSource.Token).Forget();
    }
    public void Reset()
    {
        _elapsed = 0f;
        _isRunning = false;
        _tokenSource?.Cancel();
        _tokenSource?.Dispose();
        _tokenSource = null;
    }

    async public UniTask Tick(CancellationToken token)
    {
        while(!IsFinished)
        {
            await UniTask.NextFrame();
            if(_isRunning) _elapsed += Time.deltaTime;
            
            if (token.IsCancellationRequested) return;
        }

        _finishAction?.Invoke();
    }

}


public static class Timer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimerModule CreateTimer(float duration, Action finisheAction = null)
    {
        return new TimerModule(duration, finisheAction);
    }
}
