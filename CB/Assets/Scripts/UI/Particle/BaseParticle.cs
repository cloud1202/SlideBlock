using UnityEngine;
using UnityEngine.UI;

[SerializeField]
public struct ParticleData
{
    public float Gravity;
    public float LifeTime;
    public float FadeStart;  // LifeTime 기준 비율
    public float MinSize;
    public float MaxSize;

    public ParticleData(float gravity, float lifeTime, float fadeStart, float minSize, float maxSize)
    {
        Gravity = gravity;
        LifeTime = lifeTime;
        FadeStart = fadeStart;
        MinSize = minSize;
        MaxSize = maxSize;
    }
}

public class BaseParticle
{
    protected RectTransform _rt;
    protected Image _img;
    protected ParticleData particleData;

    public bool IsActive { get; protected set; }

    public void Init(RectTransform rt, Image img, ParticleData data)
    {
        _rt = rt;
        _img = img;
        particleData = data;
    }

    /// <summary>
    /// 매 프레임 Update에서 호출. 완료되면 false를 반환.
    /// </summary>
    public virtual bool Tick(float dt) => false;

    public virtual void Stop()
    {
        IsActive = false;
        if (_rt != null)
            _rt.gameObject.SetActive(false);
    }
}
