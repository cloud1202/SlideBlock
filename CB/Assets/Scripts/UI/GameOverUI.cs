using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameOverUI : BaseUI, IScore
{
    [SerializeField] private Transform _highScoreCrown;
    [SerializeField] private TextMeshProUGUI _score;
    [SerializeField] private TextMeshProUGUI _combo;

    public override void Init()
    {
        _highScoreCrown.gameObject.SetActive(false);
        base.Init();
    }

    public void SetScores()
    {
    }

    public void SetScore(int score)
    {
        if(GameManager.Instance.HighScore < score)
        {
            PlayerPrefs.SetInt("HighScore", score);
            _highScoreCrown.gameObject.SetActive(true);
        }
        _score.text = score.ToString();
    }

    public void UpdateCombo(int combo, Vector2 boundCenter)
    {
        _combo.text = combo.ToString();
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
        gameObject.SetActive(false);
    }
}
