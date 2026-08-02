using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using VContainer;

public class InGameUI : BaseUI, IScore
{
    [SerializeField] private TextMeshProUGUI _highScore;

    private IBaseUI _menuUI;
    private IScore _scoreUI;

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
       m_soundManager.PlayBGM(SoundData.Ingame).Forget();
        InitLoadUI().Forget();
    }

    async private UniTask InitLoadUI()
    {
        if (_scoreUI == null)
            _scoreUI = await m_prefabManager.InstantiateDynamicUI<IScore>(PrefabData.IngameScoreUI);

        if (_menuUI == null)
            _menuUI = await m_prefabManager.InstantiateDynamicUI<IBaseUI>(PrefabData.MenuUI);

        _scoreUI.Init();
        SetScores();
        base.Init();
    }
    public void SetScores()
    {
        _scoreUI.SetScores();

        _highScore.text = Utility.NumberRegularExpression(m_gameManager.HighScore);
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
