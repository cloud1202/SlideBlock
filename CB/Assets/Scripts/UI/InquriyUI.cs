using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using VContainer;

public class InquriyUI : CloseBaseUI
{
    [SerializeField] private TMP_InputField _email;
    [SerializeField] private TMP_InputField _content;

    private PrefabManager m_prefabManager;
    private FirebaseManager m_firebaseManager;

    [Inject]
    public void Construct(PrefabManager prefabManager, FirebaseManager firebaseManager)
    {
        m_prefabManager = prefabManager;
        m_firebaseManager = firebaseManager;
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
        Close();
    }

    async private UniTask SendInquiry()
    {
        var ret = await m_firebaseManager.SendInquiryAsync(_content.text, _email.text);

        var popup = await m_prefabManager.InstantiateDynamicUI<IPopupNotice>(PrefabData.PopupNoticeUI);

        popup.SetNoticeContent(ret.Message);
        popup.Init();
        if (ret.Message is GameTextData.INQURIY_SEND_SUCCESS)
            OnClickBackBtn();
        else
            _content.text = string.Empty;
    }
}
