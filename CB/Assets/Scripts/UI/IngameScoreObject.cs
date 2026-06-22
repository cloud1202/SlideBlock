using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using TMPro;
using UnityEngine;

public class IngameScoreObject : BaseParticlePlayer<BrickParticle>
{
    [SerializeField] private TextMeshProUGUI _scoreTMP;
    private CancellationTokenSource _confettiToken = null;


    public override void Init()
    {
        if (_confettiToken == null)
            _confettiToken = new CancellationTokenSource();
        base.Init();
    }
    // 콤보 발생 시 호출
    async public override UniTask Burst(CancellationToken ct)
    {
        float spawnInterval = 0.1f;

        while (ct.IsCancellationRequested == false)
        {
            int count = Utility.RandomInt(6, 12);
            int color = Utility.RandomInt(EnumConverter.Enum32ToInt(BrickType.MAX));
            for (int i = 0; i < count; ++i)
            {
                if (_pool.Count == 0) break;
                var p = _pool.Dequeue();
                _active.Add(p);
                p.Play(GetSpawnPos(), GetColor(color), GetVelocity(), _confettiToken.Token, OnComplete).Forget();
            }
            await UniTask.Delay(TimeSpan.FromSeconds(spawnInterval), cancellationToken: ct)
                         .SuppressCancellationThrow();
        }
    }

    public void ResetToken()
    {
        _confettiToken?.Cancel();
        _confettiToken?.Dispose();
        _confettiToken = null;
    }

    private Vector2 GetSpawnPos()
    {
        // 텍스트 실제 렌더링 크기 추출 (preferredWidth 대신 textBounds 사용)
        Bounds bounds = _scoreTMP.textBounds;
        float halfWidth = bounds.size.x * 0.5f;

        // 텍스트 박스 기준 로컬 중심 좌표 + bounds 오프셋 보정
        Vector2 textCenter = _scoreTMP.rectTransform.anchoredPosition + (Vector2)bounds.center;

        float randomX = textCenter.x + Utility.RandomFloat(-halfWidth, halfWidth);

        return new Vector2(randomX, 0f);
    }

    private Vector2 GetVelocity() =>
        new(Utility.RandomFloat(-120f, 120f), Utility.RandomFloat(80f, 220f));

    private Color GetColor(int index)
    {
        var set = Colors.Sets[0];
        return set[Mathf.Clamp(index, 0, set.Length - 1)];
    }

    private void OnComplete(BrickParticle p)
    {
        _active.Remove(p);
        _pool.Enqueue(p);
    }

}
