using Cysharp.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using System.Collections.Generic;

[ManagerOrder(2)]
public class AdmobManager : SingletonInstance<AdmobManager>, IManager
{
    public bool IsPrivacyOptionsRequire = ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

    private BannerView bannerView;
    public override void Init()
    {
        base.Init();
        Logging("Admob 초기화 시작");

        RequestConsent();
    }

    private void InitializeAndLoadAds()
    {
        // Google Mobile Ads SDK 초기화
        MobileAds.Initialize(initStatus =>
        {
            Logging("Admob 초기화 완료");
            CreateBanner();
        });
    }

    private void CreateBanner()
    {
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
            "ab211322-c33c-4e56-baf5-86a13cbd86a2"
        };
        RequestConfiguration requestConfiguration = new RequestConfiguration();
        requestConfiguration.TestDeviceIds = testDeviceIds;

        MobileAds.SetRequestConfiguration(requestConfiguration);

        for(int i = 0; i < testDeviceIds.Count; ++i)
        {
            Logging($"Test Device 등록 : {testDeviceIds[i]}");
        }
        var debugSettings = new ConsentDebugSettings
        {
            // 이 기기를 유럽(EEA)에 있는 것처럼 강제 설정
            DebugGeography = DebugGeography.EEA,

            // 아래 해시 ID를 등록한 기기에서만 디버그 지오그래피가 적용됨
            TestDeviceHashedIds = new List<string> { "ab211322-c33c-4e56-baf5-86a13cbd86a2" }
        };

        requestParams = new ConsentRequestParameters
        {
            ConsentDebugSettings = debugSettings
        };
#else
        requestParams = new ConsentRequestParameters();
#endif

        // 앱 실행마다 매번 호출해야 함 (재동의 필요 여부를 매번 체크하기 때문)
        ConsentInformation.Update(requestParams, OnConsentInfoUpdated);
    }

    private void OnConsentInfoUpdated(FormError updateError)
    {
        if (updateError != null)
        {
            LLogger.Log($"[UMP] 동의 정보 갱신 실패: {updateError}", LLogger.LogLevel.Warning);
            return;
        }

        // 필요한 경우에만 자동으로 폼을 띄워줌 (내용은 1단계에서 만든 메시지 그대로)
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
            LLogger.Log($"[UMP] 동의 폼 처리 중 오류: {formError}", LLogger.LogLevel.Warning);
        }

        if (ConsentInformation.CanRequestAds())
        {
            // 여기서부터 AdMob 배너/전면 광고 로드 시작
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
