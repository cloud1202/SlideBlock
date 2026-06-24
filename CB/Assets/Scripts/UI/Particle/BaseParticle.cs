using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

[SerializeField]
public struct ParticleData
{
    public float Gravity;
    public float LifeTime;
    public float FadeStart;  // LifeTime 대비 비율
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
    //public ParticleData()
    //{
    //    Gravity = -400f;
    //    LifeTime = 1.2f;
    //    FadeStart = 0.6f;  // LifeTime 대비 비율
    //}
}

public class BaseParticle
{
    protected RectTransform _rt;
    protected Image _img;

    protected ParticleData particleData;


    public void Init(RectTransform rt, Image img, ParticleData data)
    {
        _rt = rt;
        _img = img;
        particleData = data;
    }
}
