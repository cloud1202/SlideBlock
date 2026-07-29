using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using VContainer;

public class InquriyUI : BaseUI
{
    [SerializeField] private TMP_InputField _email;
    [SerializeField] private TMP_InputField _content;

    private GameManager m_gameManager;
    private PrefabManager m_prefabManager;
    private SoundManager m_soundManager;

    [Inject]
    public void Construct(GameManager gameManager, PrefabManager prefabManager, SoundManager soundManager)
    {
        m_gameManager = gameManager;
        m_prefabManager = prefabManager;
        m_soundManager = soundManager;
    }
    public override void Init()
    {
        _email.text = string.Empty;
        _content.text = string.Empty;
        base.Init();
    }
    public void OnClickSendBtn()
    {
        SendInquiry().Forget();
    }

    public void OnClickBackBtn()
    {
        base.Close();
    }

    async private UniTask SendInquiry()
    {
        var ret = await FirebaseManager.Instance.SendInquiryAsync(_content.text, _email.text);

        var popup = await m_prefabManager.InstantiateDynamicUI<IPopupNotice>(PrefabData.PopupNoticeUI);

        popup.SetNoticeContent(ret.Message);
        popup.Init();
        if (ret.Message is GameTextData.INQURIY_SEND_SUCCESS)
            OnClickBackBtn();
        else
            _content.text = string.Empty;
    }
}
