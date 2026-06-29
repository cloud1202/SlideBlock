using UnityEngine;

public class BrickParticle : BaseParticle
{
    // --- 런타임 상태 ---
    private Vector2 _pos;
    private Vector2 _vel;
    private float _elapsed;
    private float _rotSpeed;
    private Color _baseColor;

    // Play/PlaySpray 구분용
    private bool _isSpray;

    // -------------------------------------------------------
    // 활성화
    // -------------------------------------------------------
    public void Play(Vector2 spawnPos, Color color, Vector2 velocity)
    {
        Activate(spawnPos, color, velocity, particleData.MinSize, particleData.MaxSize);
        _isSpray = false;
    }

    public void PlaySpray(Vector2 spawnPos, Color color, Vector2 velocity)
    {
        Activate(spawnPos, color, velocity, 6f, 8f);
        _isSpray = true;
    }

    private void Activate(Vector2 spawnPos, Color color, Vector2 velocity, float minSize, float maxSize)
    {
        _pos      = spawnPos;
        _vel      = velocity;
        _elapsed  = 0f;
        _rotSpeed = UnityEngine.Random.Range(-180f, 180f);
        _baseColor = color;
        _baseColor.a = 1f;

        _rt.anchoredPosition = _pos;
        _rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
        _rt.sizeDelta = Vector2.one * UnityEngine.Random.Range(minSize, maxSize);

        _img.color = _baseColor;
        _rt.gameObject.SetActive(true);
        IsActive = true;
    }

    // -------------------------------------------------------
    // Update 루프에서 호출 — 완료되면 false 반환
    // -------------------------------------------------------
    public override bool Tick(float dt)
    {
        _vel.y += particleData.Gravity * dt;
        _pos   += _vel * dt;
        _elapsed += dt;

        _rt.anchoredPosition = _pos;
        _rt.Rotate(0f, 0f, _rotSpeed * dt);

        float lifeRatio = _elapsed / particleData.LifeTime;
        if (lifeRatio > particleData.FadeStart)
        {
            Color faded = _baseColor;
            faded.a = 1f - (lifeRatio - particleData.FadeStart) / (1f - particleData.FadeStart);
            _img.color = faded;
        }

        if (_elapsed >= particleData.LifeTime)
        {
            Stop();
            return false;
        }
        return true;
    }
}
