using UnityEngine;

public class LegalUI : BaseUI
{
    private const string PRIVACY_POLICY = "https://cloud1202.github.io/PrivacyPolicy/SlideBlock";
    private const string TERMS = "https://cloud1202.github.io/Terms/SlideBlock";
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
        base.Close();
    }
}
