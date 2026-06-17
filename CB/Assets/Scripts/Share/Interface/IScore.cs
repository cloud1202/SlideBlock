using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IScore : IBaseUI
{

    public void SetScore(int score);

    public void SetScores();

    public void UpdateCombo(int comboValue, Vector2 boundCenter = default);
}
