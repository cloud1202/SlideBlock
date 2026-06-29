using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BaseParticlePlayer<T> : MonoBehaviour
    where T : BaseParticle, new()
{
    [SerializeField] protected Sprite _particle;
    [SerializeField] protected int _poolSize = 24;
    [SerializeField] protected Vector2 _particleSize = new Vector2(10f, 10f);

    [SerializeField] private float _gravity;
    [SerializeField] private float _lifeTime;
    [SerializeField] private float _fadeStart;
    [SerializeField] private float _minSize;
    [SerializeField] private float _maxSize;
    protected ParticleData particleData;
    protected bool _bursting;

    protected readonly Queue<T> _pool = new();

    // swap-back 제거를 위해 List 유지 (index 접근 필요)
    protected readonly List<T> _active = new();

    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------
    public virtual void Init()
    {
        particleData = new ParticleData(_gravity, _lifeTime, _fadeStart, _minSize, _maxSize);

        int cnt = _poolSize - _pool.Count;
        for (int i = 0; i < cnt; i++)
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(transform, false);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.sprite = _particle;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = _particleSize;

            go.SetActive(false);

            var instance = new T();
            instance.Init(rt, img, particleData);
            _pool.Enqueue(instance);
        }
    }

    // -------------------------------------------------------
    // Update — 모든 활성 파티클을 한 루프에서 처리
    // -------------------------------------------------------
    protected virtual void Update()
    {
        if (_active.Count == 0) return;

        float dt = Time.deltaTime;
        int i = 0;

        while (i < _active.Count)
        {
            bool alive = _active[i].Tick(dt);
            if (!alive)
            {
                // swap-back: 마지막 원소와 교체 후 RemoveAt(last) → O(1)
                ReturnToPool(_active[i]);

                int last = _active.Count - 1;
                _active[i] = _active[last];
                _active.RemoveAt(last);
                // i를 증가시키지 않음 — 교체된 원소를 다음 루프에서 처리
            }
            else
            {
                i++;
            }
        }
    }

    // -------------------------------------------------------
    // 풀 반환
    // -------------------------------------------------------
    protected void ReturnToPool(T p)
    {
        _pool.Enqueue(p);
    }

    // -------------------------------------------------------
    // 풀에서 꺼내기
    // -------------------------------------------------------
    protected bool TryDequeue(out T particle)
    {
        if (_pool.Count > 0)
        {
            particle = _pool.Dequeue();
            _active.Add(particle);
            return true;
        }
        particle = default;
        return false;
    }

    // -------------------------------------------------------
    // 전체 정지 (씬 전환, 패널 닫기 등)
    // -------------------------------------------------------
    public virtual void StopAll()
    {
        foreach (var p in _active)
        {
            ReturnToPool(p);
            p.Stop();
        }

        _active.Clear();
        // Stop()이 SetActive(false)를 처리하므로 풀 반환만 하면 됨
        // (이미 Stop된 상태이지만 풀에 다시 넣어 재사용 가능하게)
        // → 필요하면 _pool에 다시 넣어도 되지만 Init()에서 풀 크기를 체크하므로 생략 가능
    }

    // -------------------------------------------------------
    // 서브클래스에서 Override할 Burst 진입점 (동기)
    // -------------------------------------------------------
    public virtual void Burst() { }
}
