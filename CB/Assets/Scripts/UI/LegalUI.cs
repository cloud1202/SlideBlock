using UnityEngine;

public class LegalUI : CloseBaseUI
{
    private const string PRIVACY_POLICY = "https://layoncraft.github.io/PrivacyPolicy/SlideBlock";
    private const string TERMS = "https://layoncraft.github.io/Terms/SlideBlock";
    public void OnClickPrivacyPolicy()
    {
        Application.OpenURL(PRIVACY_POLICY);
    }
    public void OnClickTerms()
    {
        Application.OpenURL(TERMS);
    }
    public void OnClickCloseBtn()
    {
        Close();
    }
}
