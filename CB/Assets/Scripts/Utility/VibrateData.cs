using UnityEngine;

public class VibrateData
{
    private const int BENCHMARK_VARIABLE = 100;
    private const float INIT_AMP = 0.08f;
    private const float INIT_FRE = 5f;
    private const float INIT_DUR = 1f;
    public float amplitude { get; private set; }
    public float frequency { get; private set; }
    public float duration { get; private set; }

    public VibrateData()
    {
        amplitude = INIT_AMP;
        frequency = INIT_FRE;
        duration = INIT_DUR;
    }

    public VibrateData(float amplitude, float frequency, float duration)
    {
        this.amplitude = amplitude;
        this.frequency = frequency;
        this.duration = duration;
    }

    public void UpdateFrequency(int variable)
    {
        variable = Mathf.Clamp(variable, 0, BENCHMARK_VARIABLE);
        var t = Mathf.InverseLerp(0, BENCHMARK_VARIABLE, variable);
        frequency = INIT_FRE + (1 - Mathf.Pow(1 - t, 2)) * INIT_FRE;
    }
    public void InitData()
    {
        amplitude = INIT_AMP;
        frequency = INIT_FRE;
        duration = INIT_DUR;
    }

    public void ResetData()
    {
        amplitude = 0;
        frequency = 0;
        duration = 0;
    }
}
