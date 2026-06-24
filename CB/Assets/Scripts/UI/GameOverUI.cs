using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;

public class GameOverUI : BaseUI, IScore
{
    [SerializeField] private HighScoreObject _highScore;
    [SerializeField] private TextMeshProUGUI _score;
    [SerializeField] private TextMeshProUGUI _combo;

    private CancellationTokenSource _confettiToken = null;
    public override void Init()
    {
        _highScore.Init();
        base.Init();
        SetScore(0);
        SoundManager.Instance.PlayBGM().Forget();
    }

    public void SetScores()
    {
    }

    public void SetScore(int score)
    {
        if(GameManager.Instance.HighScore < score)
        {
            UpdateHighScore(score).Forget();
        }
        _score.text = Utility.NumberRegularExpression(score);
    }

    async private UniTask UpdateHighScore(int score)
    {
        SoundManager.Instance.PlaySFX(SoundData.Confetti).Forget();
        _confettiToken = new CancellationTokenSource();
        FirebaseManager.Instance.SaveHighScore(SaveFieldType.HighScore_Classic, score);
        _highScore.Burst(_confettiToken.Token).Forget();

        await UniTask.WaitForSeconds(2f);
        AdmobManager.Instance.CreateInterstitial();
    }

    public void UpdateCombo(int combo, Vector2 boundCenter)
    {
        _combo.text = Utility.NumberRegularExpression(combo);
    }

    public void OnClickRetryBtn()
    {
        GameManager.Instance.StartRound().Forget();
        OnClickCloseBtn();
    }

    public void OnClickHomeBtn()
    {
        GameManager.Instance.ExitRound();
        OnClickCloseBtn();
    }
    public void OnClickCloseBtn()
    {
        _confettiToken?.Cancel();
        _confettiToken?.Dispose();
        _confettiToken = null;
        gameObject.SetActive(false);
    }
}
