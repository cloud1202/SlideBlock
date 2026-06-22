using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class BaseParticlePlayer<T> : MonoBehaviour
    where T : BaseParticle, new ()
{
    [SerializeField] protected int _poolSize = 24;
    [SerializeField] protected Vector2 _particleSize = new Vector2(10f, 10f);

    [SerializeField] private float Gravity;
    [SerializeField] private float LifeTime;
    [SerializeField] private float FadeStart;  // LifeTime 대비 비율
    protected ParticleData particleData;

    protected readonly Queue<T> _pool = new();
    protected readonly List<T> _active = new();
    protected readonly Func<RectTransform , Image, T> _creator;

    public virtual void Init()
    {
        particleData = new ParticleData(Gravity, LifeTime, FadeStart);
        int cnt = _poolSize - _pool.Count;
        for (int i = 0; i < cnt; i++)
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(transform, false);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = _particleSize;

            go.SetActive(false);
            _pool.Enqueue(CreateInstance(rt, img));
        }
    }
    private T CreateInstance(RectTransform rt, Image img)
    {
        var instance = new T();
        instance.Init(rt, img, particleData);
        return instance;
    }


    async public virtual UniTask Burst(CancellationToken ct)
    {
        await UniTask.Yield(PlayerLoopTiming.Update);
    }
}
