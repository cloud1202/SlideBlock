using TMPro;
using UnityEngine;
using VContainer;

public class TextHandler : MonoBehaviour
{
    [SerializeField] private GameTextData _textData;
    [SerializeField] private TextMeshProUGUI _handler;

    protected TextDataManager m_textDataManager;
    [Inject]
    public void Construct(TextDataManager textDataManager)
    {
        m_textDataManager = textDataManager;
        _handler.text = m_textDataManager.GetGameText(_textData);
    }
}
