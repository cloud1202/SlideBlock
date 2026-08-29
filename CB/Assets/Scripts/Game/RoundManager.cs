using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

public class RoundManager : MonoBehaviour, IRound
{
    public int CurrentScore => _scoreValue;
    public event Action OnUpdateSymbolState;
    private const float COMBO_DELAY = 5f;
    private RoundObject _board;
    private IScore _ingameUI;
    private IScore _gameOver;

    private int _scoreValue = 0;
    private int _comboValue = 0;
    private int _maxCombo = 0;
    private TimerModule _timer;

    async public UniTask Init()
    {
        await LoadRoundObjects();
    }

    async private UniTask LoadRoundObjects()
    {
        _ingameUI = await PrefabManager.Instance.InstantiateStaticUI<IScore>(PrefabData.InGameUI);
        _gameOver = await PrefabManager.Instance.InstantiateDynamicUI<IScore>(PrefabData.GameOverUI);
        _board = await PrefabManager.Instance.InstantiateObject<RoundObject>(PrefabData.Board, this.transform);
        _board.SetRoundManager(this);

        _timer = Timer.CreateTimer(COMBO_DELAY, ResetCombo);
    }

    public void ChangeSymbolState()
    {
        OnUpdateSymbolState();
    }

    public void EnterRound()
    {
        GameManager.Instance.CaptureEnterTime = Time.realtimeSinceStartup;
        FirebaseManager.Instance.LogModeStart("Classic");
        gameObject.SetActive(true);
        _scoreValue = 0;
        _comboValue = 0;
        _maxCombo = 0;
        _ingameUI.Init();
        _board.Init();
    }

    public void EndRound()
    {
        FirebaseManager.Instance.SetCustomKey("mode", "Classic");
        FirebaseManager.Instance.LogGameOver("Classic", _scoreValue, _maxCombo);
        _ingameUI.Close();
        gameObject.SetActive(false);
        _gameOver.Init();
        _gameOver.SetScore(_scoreValue);
        _gameOver.UpdateCombo(_maxCombo);
    }

    public void ExitRound()
    {
        FirebaseManager.Instance.LogModeQuit("Classic", Time.realtimeSinceStartup - GameManager.Instance.CaptureEnterTime, _scoreValue);
        _ingameUI.Close();
        _gameOver.Close();
        Destroy(gameObject);
    }

    public void DestroyMatchBricks(int addScore, Vector2 boundCenter)
    {
        UpdateCombo(addScore > 0, boundCenter);

        _scoreValue += Utility.CalcScore(addScore, _comboValue);
        FirebaseManager.Instance.SetCustomKey("score", _scoreValue.ToString());
        _ingameUI.SetScore(_scoreValue);
    }

    private void UpdateCombo(bool isCombo, Vector2 boundCenter)
    {
        if (isCombo == false)
            return;

        Utility.AsyncDurationVibrateObject(PrefabManager.Instance.MainCamera.transform, new System.Threading.CancellationTokenSource()).Forget();
        _comboValue++;
        _ingameUI.UpdateCombo(_comboValue, boundCenter);
        _timer.Start();
        _maxCombo = Mathf.Max(_comboValue, _maxCombo);
    }

    private void ResetCombo()
    {
        _comboValue = 0;
        _ingameUI.UpdateCombo(_comboValue, Vector2.zero);
        _timer.Reset();
    }
}
