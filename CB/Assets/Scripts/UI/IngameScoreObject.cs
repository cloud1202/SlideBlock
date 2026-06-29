using TMPro;
using UnityEngine;

public class IngameScoreObject : BaseParticlePlayer<BrickParticle>
{
    [SerializeField] private TextMeshProUGUI _scoreTMP;

    private float _spawnTimer;

    private const float SpawnInterval = 0.1f;

    public override void Init()
    {
        StopBurst();
        StopAll();
        base.Init();
    }

    public override void Burst()
    {
        _bursting   = true;
        _spawnTimer = 0f;
    }

    public void StopBurst()
    {
        _bursting = false;
    }

    protected override void Update()
    {
        base.Update();

        if (!_bursting) return;

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= SpawnInterval)
        {
            _spawnTimer -= SpawnInterval;
            SpawnBatch();
        }
    }

    private void SpawnBatch()
    {
        int count      = Utility.RandomInt(6, 12);
        int colorIndex = Utility.RandomInt(EnumConverter.Enum32ToInt(BrickType.MAX));

        for (int i = 0; i < count; i++)
        {
            if (!TryDequeue(out var p)) break;
            p.Play(GetSpawnPos(), GetColor(colorIndex), GetVelocity());
        }
    }

    private Vector2 GetSpawnPos()
    {
        Bounds bounds    = _scoreTMP.textBounds;
        float halfWidth  = bounds.size.x * 0.5f;
        Vector2 textCenter = _scoreTMP.rectTransform.anchoredPosition + (Vector2)bounds.center;

        return new Vector2(textCenter.x + Utility.RandomFloat(-halfWidth, halfWidth), 0f);
    }

    private Vector2 GetVelocity() =>
        new(Utility.RandomFloat(-120f, 120f), Utility.RandomFloat(80f, 220f));

    private Color GetColor(int index)
    {
        var set = Colors.Sets[0];
        return set[Mathf.Clamp(index, 0, set.Length - 1)];
    }
}
