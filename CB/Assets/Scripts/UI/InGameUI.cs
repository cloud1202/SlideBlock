using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class InGameUI : BaseUI, IScore
{
    [SerializeField] private TextMeshProUGUI _highScore;

    private IBaseUI _menuUI;
    private IScore _scoreUI;

    public override void Init()
    {
        SoundManager.Instance.PlayBGM(SoundData.Ingame).Forget();
        InitLoadUI().Forget();
    }

    async private UniTask InitLoadUI()
    {
        if (_scoreUI == null)
            _scoreUI = await PrefabManager.Instance.InstantiateDynamicUI<IScore>(PrefabData.IngameScoreUI);

        if (_menuUI == null)
            _menuUI = await PrefabManager.Instance.InstantiateDynamicUI<IBaseUI>(PrefabData.MenuUI);

        _scoreUI.Init();
        SetScores();
        base.Init();
    }
    public void SetScores()
    {
        _scoreUI.SetScores();

        //_highScore.text = Utility.NumberRegularExpression(GameManager.Instance.HighScore);
    }

    public void SetScore(int score)
    {
        _scoreUI.SetScore(score);
    }

    public void UpdateCombo(int comboValue, Vector2 boundCenter)
    {
        _scoreUI.UpdateCombo(comboValue, boundCenter);
    }
    public void OnClickMenuBtn()
    {
        _menuUI.Init();
    }

    public override void Close()
    {
        _menuUI.Close();
        _scoreUI.Close();
        base.Close();
    }
}
