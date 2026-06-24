using GoogleMobileAds.Api;
using System.Collections.Generic;

public class AdmobManager : SingletonInstance<AdmobManager>, IManager
{
    private BannerView bannerView;
    public override void Init()
    {
        base.Init();
        Logging("Admob 초기화 시작");
#if UNITY_EDITOR
#else
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
#endif
        // Google Mobile Ads SDK 초기화
        MobileAds.Initialize(initStatus => {
            Logging("Admob 초기화 완료");
        });

        CreateBanner();
    }

    private void CreateBanner()
    {
#if UNITY_EDITOR

        // 플랫폼에 맞는 광고 단위 ID 설정 (테스트용 ID)
        string adUnitId = "ca-app-pub-3940256099942544/6300978111";

#else
        string adUnitId = "ca-app-pub-7932391001617366/3326470671";
#endif
        Logging($"AD Banner  : {adUnitId}");
        // 화면 하단에 320x50 표준 배너 생성
        bannerView = new BannerView(adUnitId, AdSize.Banner, AdPosition.Bottom);

        AdRequest request = new AdRequest();
        bannerView.LoadAd(request);

    }

    public void CreateInterstitial()
    {
        var adRequest = new AdRequest();
#if UNITY_EDITOR

        // 플랫폼에 맞는 광고 단위 ID 설정 (테스트용 ID)
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

    public void OnDestroy()
    {
        // 리소스 해제
        if (bannerView != null)
        {
            bannerView.Destroy();
        }
    }
}
