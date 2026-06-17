using UnityEngine;
using TMPro;
using System.Threading;
using Cysharp.Threading.Tasks;

public class UtilityTester : MonoBehaviour
{
    [SerializeField] private TMP_InputField _input;
    [SerializeField] private Transform _vibObj;
    private int _value = 0;
    private int _combo = 0;

    private VibrateData _vibData = new VibrateData();

    private CancellationTokenSource _cancellationTokenSource;
    private void Awake()
    {
        _cancellationTokenSource = null;
        _input.onEndEdit.AddListener(UpdateValue);
    }
    private void OnDestroy()
    {
        _input.onEndEdit.RemoveListener(UpdateValue);
    }

    private void UpdateValue(string str)
    {
        if (int.TryParse(str, out _value) == false)
            return;

    }

    public void OnClickDigits()
    {
        var dat = Utility.GetDigits(_value);

        for (int i = 0; i < dat.Length; ++i)
        {
            LLogger.Log(dat[i].ToString());
        }
    }

    public void OnClickCombo()
    {
        _combo++;
        LLogger.Log($"COMBO :: {_combo}");
        _vibData.UpdateFrequency(_combo);
        if (_cancellationTokenSource == null)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            Utility.AsyncVibrateObject(_vibObj, _cancellationTokenSource, _vibData).Forget();
        }
    }

    public void OnClickCancelCombo()
    {
        _combo = 0;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }
}
