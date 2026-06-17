using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ComboObject : MonoBehaviour
{
    private RectTransform _myRT;
    public RectTransform MyRT
    {
        get 
        {
            if (_myRT == null)
                _myRT = GetComponent<RectTransform>();

            return _myRT;
        }
    }
    [SerializeField] private Image _title;
    [SerializeField] private Image _digit;
    private List<Image> _digits = new List<Image>();
    private Vector2 _lastDigitPos;
    private float _addWidth;

    public void Init()
    {
        _digits.Clear();
        _lastDigitPos = _digit.rectTransform.anchoredPosition;
        _addWidth = _digit.rectTransform.rect.width * 2;
        _digits.Add(_digit);
        CreateDigit(4);
    }

    private void CreateDigit(int cnt)
    {
        for (int i = 0; i < cnt; ++i)
        {
            var digit = Instantiate<Image>(_digit, this.transform);
            _lastDigitPos = new Vector2(_lastDigitPos.x + _addWidth, _lastDigitPos.y);
            digit.rectTransform.anchoredPosition = _lastDigitPos;
            _digits.Add(digit);
        }
    }

    async public UniTask ToastCombo(params Sprite[] digits)
    {
        if(_digits.Count < digits.Length)
            CreateDigit(digits.Length - _digits.Count);

        List<UniTask> tasks = new List<UniTask>() { Utility.AsyncToastGraphicObject(_title) };

        for (int i = 0; i < digits.Length; ++i)
        {
            _digits[i].sprite = digits[i];
            tasks.Add(Utility.AsyncToastGraphicObject(_digits[i]));
        }

        for (int i = digits.Length; i < _digits.Count; ++i)
        {
            _digits[i].DOFade(0, 0);
        }

        await UniTask.WhenAll(tasks);
    }
}
