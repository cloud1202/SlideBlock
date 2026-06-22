using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public class BrickParticle : BaseParticle
{
    public async override UniTaskVoid Play(
        Vector2 spawnPos,
        Color color,
        Vector2 velocity,
        CancellationToken ct,
        Action<BrickParticle> onComplete)
    {
        _rt.anchoredPosition = spawnPos;
        _rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
        _rt.sizeDelta = Vector2.one * UnityEngine.Random.Range(8f, 16f);

        color.a = 1f;
        _img.color = color;

        _rt.gameObject.SetActive(true);

        float elapsed = 0f;
        float rotSpeed = UnityEngine.Random.Range(-180f, 180f);
        Vector2 pos = spawnPos;
        Vector2 vel = velocity;

        while (elapsed < particleData.LifeTime)
        {
            if (_rt == null)
                return;
            float dt = Time.deltaTime;
            vel.y += particleData.Gravity * dt;
            pos += vel * dt;
            elapsed += dt;

            _rt.anchoredPosition = pos;
            _rt.Rotate(0f, 0f, rotSpeed * dt);

            float lifeRatio = elapsed / particleData.LifeTime;
            if (lifeRatio > particleData.FadeStart)
            {
                Color faded = color;
                faded.a = 1f - (lifeRatio - particleData.FadeStart) / (1f - particleData.FadeStart);
                _img.color = faded;
            }

            if (ct.IsCancellationRequested) break;

            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        _rt.gameObject.SetActive(false);
        onComplete(this);
    }
}
