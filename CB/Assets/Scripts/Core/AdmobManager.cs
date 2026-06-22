using GoogleMobileAds.Api;
using UnityEngine;

public class AdmobManager : SingletonInstance<AdmobManager>, IManager
{
    private BannerView bannerView;
    public override void Init()
    {
        base.Init();
        // Google Mobile Ads SDK 초기화
        MobileAds.Initialize(initStatus => { });

        //CreateBanner();
    }

    private void CreateBanner()
    {
        // 플랫폼에 맞는 광고 단위 ID 설정 (테스트용 ID)
        string adUnitId = "ca-app-pub-3940256099942544/6300978111";

        // 화면 하단에 320x50 표준 배너 생성
        bannerView = new BannerView(adUnitId, AdSize.Banner, AdPosition.Bottom);

        // 광고 요청 생성
        AdRequest request = new AdRequest();

        // 배너 뷰에 광고 로드
        bannerView.LoadAd(request);
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
