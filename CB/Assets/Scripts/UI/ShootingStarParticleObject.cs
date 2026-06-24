using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class ShootingStarParticleObject : BaseParticlePlayer<BrickParticle>
{
    [SerializeField] private Image _star;
    [SerializeField] private Vector2 _start;
    private ParticleData _particleData;
    [ContextMenu("Burst")]
    public override void Init()
    {
        Color initColor = _star.color;
        initColor.a = 1f;
        _star.color = initColor;
        _star.rectTransform.anchoredPosition = _start;
        base.Init();
    }

    public async UniTaskVoid ShootingStar(
       ParticleData particle,
       Vector2 velocity,
       CancellationToken ct)
    {
        float elapsed = 0f;
        float emitTimer = 0f;

        _star.gameObject.SetActive(true);

        Vector2 pos = _start;
        Vector2 vel = velocity;

        while (elapsed < particle.LifeTime)
        {
            if (_star == null)
                return;
            float dt = Time.deltaTime;
            vel.y += particle.Gravity * dt;
            pos += vel * dt;
            elapsed += dt;
            Vector2 dir = (pos - _star.rectTransform.anchoredPosition).normalized;

            _star.rectTransform.anchoredPosition = pos;
            emitTimer += dt;
            float lifeRatio = elapsed / particle.LifeTime;
            if (emitTimer >= 0.035f)
            {
                emitTimer = 0f;

                SpawnTrailParticle(pos,-dir);
            }
            if (lifeRatio > (particle.FadeStart))
            {
                Color faded = _star.color;
                faded.a = 1f - (lifeRatio - particle.FadeStart) / (1f - particle.FadeStart);
                _star.color = faded;
            }

            if (ct.IsCancellationRequested) break;

            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        _star.gameObject.SetActive(false);
    }

    private void SpawnTrailParticle(Vector2 pos, Vector2 backDir)
    {
        int color = Utility.RandomInt(EnumConverter.Enum32ToInt(BrickType.MAX));
        for (int i = 0; i < 1; i++)
        {
            Vector2 dir = Quaternion.Euler(
                0f,
                0f,
                UnityEngine.Random.Range(-25f, 25f)) * backDir;

            Vector2 velocity = dir * UnityEngine.Random.Range(80f, 180f);

            BrickParticle particle = _pool.Dequeue();

            particle.Play(
                pos,
                 GetColor(color),
                velocity,
                destroyCancellationToken,
                OnComplete).Forget();
        }
    }

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
