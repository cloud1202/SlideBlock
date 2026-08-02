using Cysharp.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using System.Collections.Generic;

#if UNITY_ANDROID || UNITY_EDITOR
public class AdmobManager : BaseManager
{
    public bool IsPrivacyOptionsRequire = ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

    private BannerView bannerView;
    private FirebaseManager m_firebaseManager;
    public AdmobManager(ManagerInitTracker tracker, FirebaseManager firebaseManager) : base(tracker)
    {
        Logging("Admob 초기화");
        m_firebaseManager = firebaseManager;
        RequestConsent();
    }


    private void InitializeAndLoadAds()
    {
        MobileAds.Initialize(initStatus =>
        {
            Logging("Admob 초기화 완료");
            CompleteInit(ManagerType.Admob);
            CreateBanner();
        });
    }

    private void CreateBanner()
    {
        m_firebaseManager.Log("Bottom Admob Banner Create");
#if UNITY_EDITOR
        string adUnitId = "ca-app-pub-3940256099942544/6300978111";

#else
        string adUnitId = "ca-app-pub-7932391001617366/3326470671";
#endif
        Logging($"AD Banner  : {adUnitId}");
        bannerView = new BannerView(adUnitId, AdSize.Banner, AdPosition.Bottom);

        AdRequest request = new AdRequest();
        bannerView.LoadAd(request);
    }

    async public UniTask CreateInterstitial(float delay = 0f)
    {
        m_firebaseManager.Log("Update High Score Admob Create");
        await UniTask.WaitForSeconds(delay);
        var adRequest = new AdRequest();
#if UNITY_EDITOR
        string adUnitId = "ca-app-pub-3940256099942544/1033173712";
#else
        string adUnitId = "ca-app-pub-7932391001617366/3829572555";

#endif
        Logging($"AD InterstitialAd  : {adUnitId}");
        InterstitialAd.Load(adUnitId, adRequest, (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null)
            {
                LLogger.Log("Load Failed Ad");
                return;
            }
            
            if(ad != null && ad.CanShowAd())
                ad.Show();
            LLogger.Log("Load Successfully Ad");
        });
    }

    #region Consent

    private void RequestConsent()
    {
        ConsentRequestParameters requestParams;
#if DEVELOP
        List<string> testDeviceIds = new List<string>()
        {
                "d3bf1a7d-f4d1-4d8d-aef0-baf9f1964149"
        };
        RequestConfiguration requestConfiguration = new RequestConfiguration();
        requestConfiguration.TestDeviceIds = testDeviceIds;

        MobileAds.SetRequestConfiguration(requestConfiguration);

        for(int i = 0; i < testDeviceIds.Count; ++i)
        {
            Logging($"Test Device ��� : {testDeviceIds[i]}");
        }
        var debugSettings = new ConsentDebugSettings
        {
            // �� ��⸦ ����(EEA)�� �ִ� ��ó�� ���� ����
            DebugGeography = DebugGeography.EEA,

            // �Ʒ� �ؽ� ID�� ����� ��⿡���� ����� �����׷��ǰ� �����
            TestDeviceHashedIds = new List<string> { "E5944ED84A275FD9C977D24B86436A32" }
        };

        requestParams = new ConsentRequestParameters
        {
            ConsentDebugSettings = debugSettings
        };
#else
        requestParams = new ConsentRequestParameters();
#endif

        // �� ���ึ�� �Ź� ȣ���ؾ� �� (�絿�� �ʿ� ���θ� �Ź� üũ�ϱ� ����)
        ConsentInformation.Update(requestParams, OnConsentInfoUpdated);
    }

    private void OnConsentInfoUpdated(FormError updateError)
    {
        if (updateError != null)
        {
            LLogger.Log($"[UMP] ���� ���� ���� ����: {updateError}", LLogger.LogLevel.Warning);
            return;
        }

        // �ʿ��� ��쿡�� �ڵ����� ���� ����� (������ 1�ܰ迡�� ���� �޽��� �״��)
        ConsentForm.LoadAndShowConsentFormIfRequired(OnConsentFormDismissed);
    }
    public void OnClickPrivacyOptionsButton()
    {
        if (IsPrivacyOptionsRequire)
        {
            ConsentForm.ShowPrivacyOptionsForm(OnConsentFormDismissed);
        }
    }

    private void OnConsentFormDismissed(FormError formError)
    {
        if (formError != null)
        {
            LLogger.Log($"[UMP] ���� �� ó�� �� ����: {formError}", LLogger.LogLevel.Warning);
        }

        if (ConsentInformation.CanRequestAds())
        {
            // ���⼭���� AdMob ���/���� ���� �ε� ����
            InitializeAndLoadAds();
        }
    }

    #endregion

    public void OnDestroy()
    {
        if (bannerView != null)
        {
            bannerView.Destroy();
        }
    }
}
#endif
