using TMPro;
using UnityEngine;

public class TextHandler : MonoBehaviour
{
    [SerializeField] private GameTextData _textData;
    [SerializeField] private TextMeshProUGUI _handler;

    private void Awake()
    {
        _handler.text = TextDataManager.Instance.GetGameText(_textData);
    }
}
