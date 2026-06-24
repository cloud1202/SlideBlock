using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class HighScoreObject : BaseParticlePlayer<BrickParticle>
{
    [SerializeField] private Image _trophy;
    [SerializeField] private ShootingStarParticleObject[] _stars;

    private void Awake()
    {
        _trophy.gameObject.SetActive(false);
    }
    public override void Init()
    {
        base.Init();

        for (int i = 0; i < _stars.Length; ++i)
            _stars[i].Init();
    }

    [ContextMenu("Burst")]
    public void BurstTest()
    {
        Init();
        Burst(new CancellationToken()).Forget();
    }

    async public override UniTask Burst(CancellationToken ct)
    {
        TrophyPumping(ct).Forget();
        float spawnInterval = 0.1f;

        for (int i = 0; i < _stars.Length; ++i)
            _stars[i].ShootingStar(particleData, GetVelocity(), ct).Forget();

        for (int i = 0; i < _poolSize; ++i)
        {
            if (_pool.Count == 0) break;
            var p = _pool.Dequeue();
            _active.Add(p);
            int color = Utility.RandomInt(EnumConverter.Enum32ToInt(BrickType.MAX));
            p.Play(GetSpawnPos(), GetColor(color), GetVelocity(), ct, OnComplete).Forget();
        }
        await UniTask.Delay(TimeSpan.FromSeconds(spawnInterval), cancellationToken: ct)
                     .SuppressCancellationThrow();
    }

    private Vector2 GetSpawnPos()
    {
        float halfWidth = _trophy.rectTransform.rect.height * 0.2f;
        float height = _trophy.rectTransform.rect.height * 0.9f;

        float randomX = Utility.RandomFloat(-halfWidth, halfWidth);

        return new Vector2(randomX, height);
    }

    private Vector2 GetVelocity() =>
        new(Utility.RandomFloat(-120f, 120f), Utility.RandomFloat(120f, 220f));

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

    async private UniTask TrophyPumping(CancellationToken ct)
    {
        _trophy.gameObject.SetActive(true);
        Sequence anim = DOTween.Sequence().SetAutoKill(false).SetLoops(-1, LoopType.Yoyo);
        anim.Append(_trophy.transform.DOScale(Vector3.one * 1.2f, 0.3f));
        anim.Play();

        await UniTask.Yield(ct).SuppressCancellationThrow();
        anim.Kill();
    }
}
