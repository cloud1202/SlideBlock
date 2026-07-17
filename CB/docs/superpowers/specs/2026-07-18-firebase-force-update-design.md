# Firebase Remote Config 기반 강제 업데이트 설계

## 배경

Google Play에 출시한 SlideBlock(패키지명 `com.LayonStudio.SlideBlock`)에 새 버전을 배포했을 때, 치명적인 버전(크래시, 심각한 밸런스 버그 등)을 쓰고 있는 유저를 최신 버전으로 유도하는 기능이 필요하다.

당초 Google Play In-App Update API(Play Core)를 검토했으나 다음 이유로 채택하지 않음:
- 업데이트 우선순위(`updatePriority`)를 앱 코드가 아니라 릴리즈 배포 시점에 Play Console/Play Developer API에서 별도로 설정해야 해서 운영 편의성이 떨어짐.
- Play Core는 Unity 에디터/비-Android 환경에서 동작하지 않아 테스트하려면 매번 내부 테스트 트랙에 올려야 함.

대신 이미 프로젝트에 붙어 있는 Firebase 인프라(`FirebaseManager`)를 활용해 Remote Config로 최소 필요 버전을 관리하고, 앱 시작 시 자체적으로 버전을 비교해 강제 업데이트 팝업을 띄우는 방식을 채택한다. 이 방식은 배포 없이 Remote Config 값만 바꿔서 언제든 강제 업데이트를 켜고 끌 수 있고, 에디터 Play 모드에서도 그대로 테스트할 수 있다는 장점이 있다.

## 요구사항

- 앱 시작 시 1회, 원격 설정에 저장된 `min_required_version`과 현재 앱 버전(`Application.version`)을 비교한다.
- 현재 버전이 최소 버전보다 낮으면, 업데이트하거나 앱을 종료하는 것 외에는 진행할 수 없는 팝업을 띄운다 (강제 단일 단계, 권장/선택 단계는 없음).
- 원격 설정 조회에 실패하거나(오프라인 등) 아직 값이 없으면 통과시킨다 (fail-open) — 네트워크 문제로 정상 유저가 아예 게임을 못 켜는 일이 없어야 한다.
- 별도의 매니저 클래스를 새로 만들지 않고, 기존 `FirebaseManager`에 기능을 통합한다.

## 아키텍처

### FirebaseManager 확장

`Assets/Scripts/Core/FirebaseManager.cs`에 기존 `#region PlayGames`, `#region Crashlytics` 등과 같은 패턴으로 `#region RemoteConfig`를 추가한다.

```
#region RemoteConfig

private const string MIN_VERSION_KEY = "min_required_version";
private const string PLAY_STORE_URL = "https://play.google.com/store/apps/details?id=com.LayonStudio.SlideBlock";

public async UniTask CheckForForceUpdateAsync()
{
    try
    {
        var remoteConfig = FirebaseRemoteConfig.DefaultInstance;
        await remoteConfig.SetDefaultsAsync(new Dictionary<string, object> { { MIN_VERSION_KEY, "0.0.0" } }).AsUniTask();
        await remoteConfig.FetchAndActivateAsync().AsUniTask();

        var minVersion = new Version(remoteConfig.GetValue(MIN_VERSION_KEY).StringValue);
        var currentVersion = new Version(Application.version);

        if (currentVersion < minVersion)
            await ShowForceUpdatePopupAsync();
    }
    catch (Exception e)
    {
        LogError(e);
        Warning($"강제 업데이트 체크 실패, 통과 처리: {e}");
    }
}

private async UniTask ShowForceUpdatePopupAsync()
{
    var popup = await PrefabManager.Instance.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI);
    popup.SetNoticeContent(GameTextData.POPUP_UPDATE_REQUIRED);
    popup.RegistQuestionAction(
        onClickYesAction: () =>
        {
            Application.OpenURL(PLAY_STORE_URL);
            ShowForceUpdatePopupAsync().Forget(); // 닫히지 않도록 즉시 재표시
        },
        onClickNoAction: Application.Quit
    );
    await UniTask.Yield(); // 팝업 인스턴스화 대기 목적, 실질적으로 여기서 흐름이 묶임
}

#endregion
```

(실제 시그니처/네이밍은 구현 단계에서 기존 코드 스타일에 맞춰 조정될 수 있음. 핵심은 "Yes = 스토어 이동 후 팝업 재표시", "No = 앱 종료"로 팝업이 사실상 닫히지 않는다는 점.)

### GameManager.Bootstrap() 연결

`Assets/Scripts/Core/GameManager.cs`의 `Bootstrap()`에서, `await PrefabManager.Instance.InitLoadObjects();` 직후 · 로비/로딩 UI 인스턴스화 이전에 아래 한 줄을 추가한다:

```
await FirebaseManager.Instance.CheckForForceUpdateAsync();
```

이 시점을 고르는 이유:
- `PrefabManager`, `TextDataManager`가 이미 로드되어 있어야 `InstantiateDynamicUI`와 `SetNoticeContent`(GameText 조회)가 정상 동작한다.
- 강제 업데이트가 걸리는 경우, 로비/로딩 UI를 만드는 비용을 들이지 않고 바로 멈춘다.

강제 업데이트 상황에서는 `ShowForceUpdatePopupAsync()`가 사실상 반환되지 않으므로(Yes를 눌러도 팝업이 재표시됨), `Bootstrap()`의 이후 단계(로비 UI 생성 등)는 실행되지 않는다.

## 데이터 / 텍스트

- `Assets/Scripts/Share/Enum/GameTextData.cs`에 `POPUP_UPDATE_REQUIRED` 항목 추가.
- 실제 문구(한국어/영어 등)는 코드로 채울 수 없고, 기존 `GameTextEditor` 에디터 툴로 `GameTextSO` 에셋에 직접 입력해야 한다. (예시 문구: "새로운 업데이트가 있습니다. 최신 버전으로 업데이트해주세요.")

## 사전 준비 (수동, 코드 외 작업)

1. **Firebase Remote Config Unity SDK 임포트**: 현재 `Assets/Firebase`에는 Auth/Firestore/Analytics/Crashlytics/Messaging 모듈만 있고 RemoteConfig가 없음. Firebase Unity SDK에서 RemoteConfig 컴포넌트를 추가로 임포트해야 `Firebase.RemoteConfig` 네임스페이스를 사용할 수 있다.
2. **Firebase 콘솔에서 Remote Config 파라미터 등록**: 키 `min_required_version` (문자열, 예: `"1.0.0"`) 추가 및 게시.
3. **GameTextSO에 문구 입력**: 위 텍스트 항목을 에디터 툴로 채운다.

## 에러 처리

- Remote Config fetch/activate 실패(오프라인, 타임아웃, Firebase 미초기화 등) 시 예외를 잡아 `FirebaseManager.LogError`로 Crashlytics에 기록하고, 강제 업데이트 없이 정상 부팅을 계속한다 (fail-open).
- 버전 문자열이 `System.Version`으로 파싱 불가능한 형식이면(Remote Config 값 오타 등) 예외가 발생하며, 이 역시 동일하게 fail-open 처리된다.

## 테스트 / 운영

- **에디터 테스트**: Firebase 콘솔에서 `min_required_version`을 현재 `Application.version`(`1.0.0`)보다 높게 설정한 뒤 Unity 에디터 Play 모드로 실행하면, 배포 없이도 강제 업데이트 팝업 동작을 그대로 확인할 수 있다. (Play Core 방식과 달리 내부 테스트 트랙이 필요 없음)
- **운영**: 이후 치명적 버그가 발견되면, 새 버전을 스토어에 배포한 뒤 Firebase 콘솔에서 `min_required_version`을 해당 버전으로 올리기만 하면 구버전 사용자에게 강제 업데이트가 걸린다. 앱을 다시 빌드/배포할 필요 없음.

## 범위 제외 (Out of Scope)

- Google Play In-App Update(Play Core) API는 사용하지 않는다.
- 강제 단계 외의 "권장 업데이트" 같은 부드러운 알림 단계는 이번 범위에 포함하지 않는다 (향후 필요 시 별도 설계).
- iOS 등 타 플랫폼 스토어 URL 대응은 이번 범위에 포함하지 않는다 (현재 Android/Play Store만 대상).
