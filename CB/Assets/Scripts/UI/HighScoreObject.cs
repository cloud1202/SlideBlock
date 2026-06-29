using DG.Tweening;
using UnityEngine;

public class HighScoreObject : BaseParticlePlayer<BrickParticle>
{
    [SerializeField] private RectTransform _score;

    private Sequence _tweenPump;
    private float _spawnTimer;

    private const float SpawnInterval = 0.1f;

    public override void Init()
    {
        base.Init();
    }

    // -------------------------------------------------------
    // Burst 시작 / 정지
    // -------------------------------------------------------
    public override void Burst()
    {
        _bursting    = true;
        _spawnTimer  = 0f;
        Pumping();
    }

    public void StopBurst()
    {
        _bursting = false;
        _tweenPump?.Kill();
        _tweenPump = null;
        StopAll();
    }

    protected override void Update()
    {
        // 파티클 Tick은 부모 Update가 처리
        base.Update();

        if (!_bursting) return;

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= SpawnInterval)
        {
            _spawnTimer -= SpawnInterval;
            SpawnBatch();
        }
    }

    // -------------------------------------------------------
    // 배치 스폰
    // -------------------------------------------------------
    private void SpawnBatch()
    {
        int count = Utility.RandomInt(15, 20);
        int colorIndex = Utility.RandomInt(EnumConverter.Enum32ToInt(BrickType.MAX));

        for (int i = 0; i < count; i++)
        {
            if (!TryDequeue(out var p)) break;
            p.Play(GetSpawnPos(), GetColor(colorIndex), GetVelocity());
        }
    }

    private Vector2 GetSpawnPos()
    {
        float halfWidth = Screen.width * 0.3f;
        return new Vector2(Utility.RandomFloat(-halfWidth, halfWidth), 0f);
    }

    private Vector2 GetVelocity() =>
        new(Utility.RandomFloat(-140f, 140f), Utility.RandomFloat(80f, 220f));

    private Color GetColor(int index)
    {
        var set = Colors.Sets[0];
        return set[Mathf.Clamp(index, 0, set.Length - 1)];
    }

    private void Pumping()
    {
        _tweenPump = DOTween.Sequence().SetAutoKill(false).SetLoops(-1, LoopType.Yoyo);
        _tweenPump.Append(_score.transform.DOScale(Vector3.one * 1.2f, 0.3f));
        _tweenPump.Play();
    }
}
