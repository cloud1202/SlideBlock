using UnityEngine;
using TMPro;

public class UtilityTester : MonoBehaviour
{
    [SerializeField] private TMP_InputField _input;

    private int _value = 0;
    private void Awake()
    {
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
}
