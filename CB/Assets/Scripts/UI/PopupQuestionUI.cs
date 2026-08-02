using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

public class PopupQuestionUI : BaseUI, IPopupQuestion
{
    [SerializeField] private TextMeshProUGUI _content;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Vector2 _minSize;
    [SerializeField] private Vector2 _maxSize;
    [SerializeField] private Vector2 _padding;
    private Action _onClickYes;
    private Action _onClickNo;

    protected TextDataManager m_textDataManager;
    protected InputManager m_inputManager;
    [Inject]
    public void Construct(TextDataManager textDataManager, InputManager inputManager)
    {
        m_textDataManager = textDataManager;
		m_inputManager = inputManager;
    }
    public override void Init()
    {
        m_inputManager.SubscribeToInputHandler(InputType.Game_Exit, OnClickBackKey);
        base.Init();
    }

    public override void Close()
    {
        m_inputManager.UnsubscribeToInputHandler(InputType.Game_Exit, OnClickBackKey);
        base.Close();
        Destroy(this.gameObject);
    }

    public void SetNoticeContent(GameTextData content)
    {
        _content.text = m_textDataManager.GetGameText(content);
        _panel.sizeDelta = Utility.UpdateLayoutSize(_content, _minSize, _maxSize, _padding);
    }

    public void RegistQuestionAction(Action onClickYesAction = null, Action onClickNoAction = null)
    {
        _onClickYes = onClickYesAction;
        _onClickNo = onClickNoAction;
        Init();
    }

    private void OnClickBackKey(InputAction.CallbackContext callback)
    {
        OnClickCloseBtn();
    }

    public void OnClickCloseBtn()
    {
        _onClickNo?.Invoke();
        Close();
    }
    public void OnClickYesBtn()
    {
        _onClickYes?.Invoke();
        Close();
    }
    public void OnClickNoBtn()
    {
        OnClickCloseBtn();
    }
}
