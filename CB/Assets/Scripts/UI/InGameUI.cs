using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;

public class InGameUI : BaseUI, IScore
{
    [SerializeField] private TextMeshProUGUI _highScore;
    [SerializeField] private TextMeshProUGUI _score;
    [SerializeField] private IngameScoreObject _scoreObj;
    [SerializeField] private ToastCombo _combo;

    private VibrateData _vibrationData = new VibrateData();
    private CancellationTokenSource _vibrationToken= null;
    private IBaseUI _menuUI;

    public override void Init()
    {
        SoundManager.Instance.PlayBGM(SoundData.Ingame).Forget();
        _scoreObj.ResetToken();
        _scoreObj.Init();
        InitLoadUI().Forget();
    }

    async private UniTask InitLoadUI()
    {
        if (_menuUI == null)
            _menuUI = await PrefabManager.Instance.InstantiateUI<IBaseUI>(PrefabData.MenuUI);

        base.Init();
    }
    public void SetScores()
    {
        _vibrationData.ResetData();
        ResetToken();
        SetScore(0);

        _highScore.text = Utility.NumberRegularExpression(GameManager.Instance.HighScore);
    }

    public void SetScore(int score)
    {
        _score.text = Utility.NumberRegularExpression(score);
    }

    public void UpdateCombo(int comboValue, Vector2 boundCenter)
    {
        if(comboValue == 0)
        {
            ResetToken();
            return;
        }

        if (_vibrationToken == null)
        {
            _vibrationData.InitData();
            _vibrationToken = new CancellationTokenSource();
            Utility.AsyncVibrateObject(_score.transform.parent, _vibrationToken, _vibrationData).Forget();
            _scoreObj.Burst(_vibrationToken.Token).Forget();
        }
        _vibrationData.UpdateFrequency(comboValue);
        _combo.SetCombo(comboValue, boundCenter).Forget();
    }

    private void ResetToken()
    {
        _vibrationToken?.Cancel();
        _vibrationToken?.Dispose();
        _vibrationToken = null;
    }

    public void OnClickMenuBtn()
    {
        _menuUI.Init();
    }

    public override void Close()
    {
        ResetToken();
        _scoreObj.ResetToken();
        base.Close();
    }
}
