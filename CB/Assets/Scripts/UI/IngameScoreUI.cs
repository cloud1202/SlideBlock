using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;

public class IngameScoreUI : BaseUI, IScore
{
    [SerializeField] private TextMeshProUGUI _score;
    [SerializeField] private RectTransform _scoreBox;
    [SerializeField] private IngameScoreObject _scoreObj;
    [SerializeField] private ToastCombo _combo;

    private VibrateData _vibrationData = new VibrateData();
    private CancellationTokenSource _vibrationToken = null;
    private float _initScoreRatio;

    public override void Init()
    {
        _scoreObj.ResetToken();
        _scoreObj.Init();
        base.Init();
    }

    private void Awake()
    {
        _initScoreRatio = _scoreBox.anchoredPosition.y / ResolutionScreen.REF_HEIGHT;

        ResolutionScreen.Subscribe(ChangeResolution);
    }
    private void OnDestroy()
    {
        ResolutionScreen.Unsubscribe(ChangeResolution);
    }

    public void SetScores()
    {
        _vibrationData.ResetData();
        ResetToken();
        SetScore(0);
    }

    public void SetScore(int score)
    {
        _score.text = Utility.NumberRegularExpression(score);
    }

    public void UpdateCombo(int comboValue, Vector2 boundCenter)
    {
        if (comboValue == 0)
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

    public override void Close()
    {
        ResetToken();
        _scoreObj.ResetToken();
        base.Close();
    }

    private void ChangeResolution(float width, float height, float scaleFactor)
    {
        if (ResolutionScreen.REF_ASPECT < (width / height))
        {
            _scoreBox.anchoredPosition = new Vector2(0, -30f);
            return;
        }

        var canvasHeight = PrefabManager.Instance.MainCanvas.rect.height;
        _scoreBox.anchoredPosition = new Vector2(0, canvasHeight * _initScoreRatio);
    }
}
