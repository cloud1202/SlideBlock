using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToastCombo : MonoBehaviour
{
    private Queue<ComboObject> _comboToast = new Queue<ComboObject>();
    [SerializeField] private Sprite[] _digits;
    [SerializeField] private ComboObject _combo;
    private Vector2 _boundary = new Vector2(250f, 400f);

    public void Awake()
    {
        _comboToast.Clear();
        for (int i = 0; i < 16; ++i)
        {
            var combo = Instantiate<ComboObject>(_combo, this.transform);
            combo.Init();
            _comboToast.Enqueue(combo);
        }
    }

    async public UniTask SetCombo(int comboValue, Vector2 boundCenter)
    {
        var combo = _comboToast.Dequeue();
        combo.transform.position = boundCenter;

        if (Mathf.Abs(combo.MyRT.anchoredPosition.x) > _boundary.x)
            combo.MyRT.anchoredPosition = new Vector2(Mathf.Sign(combo.MyRT.anchoredPosition.x) * _boundary.x, combo.MyRT.anchoredPosition.y);

        if (Mathf.Abs(combo.MyRT.anchoredPosition.y) > _boundary.y)
            combo.MyRT.anchoredPosition = new Vector2(combo.MyRT.anchoredPosition.x, Mathf.Sign(combo.MyRT.anchoredPosition.y) * _boundary.y);

        var dat = Utility.GetDigits(comboValue);
        Sprite[] sprites = new Sprite[dat.Length];
        for (int i = 0; i < dat.Length; ++i)
        {
            sprites[i] = _digits[dat[i]];
        }

        await combo.ToastCombo(sprites);

        _comboToast.Enqueue(combo);
    }
}
