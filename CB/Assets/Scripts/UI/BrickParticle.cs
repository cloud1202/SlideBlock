using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class BrickParticle
{
    private readonly RectTransform _rt;
    private readonly Image _img;

    private const float Gravity = -400f;
    private const float LifeTime = 1.2f;
    private const float FadeStart = 0.6f;  // LifeTime 대비 비율

    public BrickParticle(RectTransform rt, Image img)
    {
        _rt = rt;
        _img = img;
    }

    public async UniTaskVoid Play(
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

        while (elapsed < LifeTime)
        {
            float dt = Time.deltaTime;
            vel.y += Gravity * dt;
            pos += vel * dt;
            elapsed += dt;

            _rt.anchoredPosition = pos;
            _rt.Rotate(0f, 0f, rotSpeed * dt);

            float lifeRatio = elapsed / LifeTime;
            if (lifeRatio > FadeStart)
            {
                Color faded = color;
                faded.a = 1f - (lifeRatio - FadeStart) / (1f - FadeStart);
                _img.color = faded;
            }

            // ct 취소 시 즉시 반납
            if (ct.IsCancellationRequested) break;

            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        _rt.gameObject.SetActive(false);
        onComplete(this);
    }
}
