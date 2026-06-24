using Cysharp.Threading.Tasks;
using UnityEngine;

public class RoundManager : MonoBehaviour, IRound
{
    private const float COMBO_DELAY = 5f;
    private RoundObject _board;
    private IScore _score;
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
        _score = await PrefabManager.Instance.InstantiateStaticUI<IScore>(PrefabData.InGameUI);
        _gameOver = await PrefabManager.Instance.InstantiateDynamicUI<IScore>(PrefabData.GameOverUI);
        _board = await PrefabManager.Instance.InstantiateObject<RoundObject>(PrefabData.Board, this.transform);
        _board.SetRoundManager(this);

        _timer = Timer.CreateTimer(COMBO_DELAY, ResetCombo);
    }

    public void EnterRound()
    {
        _scoreValue = 0;
        _comboValue = 0;
        _maxCombo = 0;
        _score.Init();
        _board.Init();
    }

    public void EndRound()
    {
        _score.Close();
        _gameOver.Init();
        _gameOver.SetScore(_scoreValue);
        _gameOver.UpdateCombo(_maxCombo);
    }

    public void ExitRound()
    {
        FirebaseManager.Instance.LogEvent("Move Home");
        _score.Close();
        _gameOver.Close();
        Destroy(gameObject);
    }

    public void DestroyMatchBricks(int addScore, Vector2 boundCenter)
    {
        UpdateCombo(addScore > 0, boundCenter);

        _scoreValue += Utility.CalcScore(addScore, _comboValue);
        _score.SetScore(_scoreValue);
    }

    private void UpdateCombo(bool isCombo, Vector2 boundCenter)
    {
        if (isCombo == false)
            return;

        Utility.AsyncDurationVibrateObject(PrefabManager.Instance.MainCamera.transform, new System.Threading.CancellationTokenSource()).Forget();
        _comboValue++;
        _score.UpdateCombo(_comboValue, boundCenter);
        _timer.Start();
        _maxCombo = Mathf.Max(_comboValue, _maxCombo);
    }

    private void ResetCombo()
    {
        _comboValue = 0;
        _score.UpdateCombo(_comboValue, Vector2.zero);
        _timer.Reset();
    }
}
