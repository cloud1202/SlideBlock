using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InGameUI : BaseUI, IScore
{
    [SerializeField] private TextMeshProUGUI _highScore;
    [SerializeField] private TextMeshProUGUI _score;
    [SerializeField] private ToastCombo _combo;

    private IBaseUI _menuUI;

    public override void Init()
    {
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
        SetScore(0);

        _highScore.text = GameManager.Instance.HighScore.ToString();
    }

    public void SetScore(int score)
    {
        _score.text = Utility.NumberRegularExpression(score);
        LLogger.Log($"Score :: {score}");
    }

    public void UpdateCombo(int comboValue, Vector2 boundCenter)
    {
        _combo.SetCombo(comboValue, boundCenter).Forget();
    }

    public void OnClickMenuBtn()
    {
        _menuUI.Init();
    }
}
