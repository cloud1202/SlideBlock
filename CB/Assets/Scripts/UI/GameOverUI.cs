using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using VContainer;

public class GameOverUI : BaseUI, IScore
{
    [SerializeField] private HighScoreObject _highScore;
    [SerializeField] private TextMeshProUGUI _score;
    [SerializeField] private TextMeshProUGUI _combo;

    private GameManager m_gameManager;
    private PrefabManager m_prefabManager;
    private SoundManager m_soundManager;

    [Inject]
    public void Construct(GameManager gameManager, PrefabManager prefabManager, SoundManager soundManager)
    {
        m_gameManager = gameManager;
        m_prefabManager = prefabManager;
        m_soundManager = soundManager;
    }
    public override void Init()
    {
        _highScore.Init();
        base.Init();
        SetScore(0);
        m_soundManager.PlayBGM().Forget();
    }

    public void SetScores() { }

    public void SetScore(int score)
    {
        //if (GameManager.Instance.HighScore < score)
        //{
        //    UpdateHighScore(score);
        //}
        _score.text = Utility.NumberRegularExpression(score);
    }

    private void UpdateHighScore(int score)
    {
        m_soundManager.PlaySFX(SoundData.Confetti).Forget();
       // GameManager.Instance.HighScore = score;
        _highScore.Burst();

#if UNITY_ANDROID || UNITY_EDITOR
        AdmobManager.Instance.CreateInterstitial(2f).Forget();
#endif
    }

    public void UpdateCombo(int combo, Vector2 boundCenter)
    {
        _combo.text = Utility.NumberRegularExpression(combo);
    }

    public void OnClickRetryBtn()
    {
        //GameManager.Instance.StartRound().Forget();
        OnClickCloseBtn();
    }

    public void OnClickHomeBtn()
    {
        //GameManager.Instance.ExitRound();
        OnClickCloseBtn();
    }

    public void OnClickCloseBtn()
    {
        _highScore.StopBurst();
        gameObject.SetActive(false);
    }
}
