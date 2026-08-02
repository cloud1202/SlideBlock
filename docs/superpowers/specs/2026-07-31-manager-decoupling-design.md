# 매니저 결합도 정리 설계

- 작성일: 2026-07-31
- 브랜치: `VContainer`
- 상태: 승인됨

## 배경

VContainer 마이그레이션을 끝낸 뒤 두 가지 구조적 문제가 남았다.

**FirebaseManager가 갓 오브젝트다.** 한 클래스가 9개 책임을 진다 — SDK 초기화, 인증, Firestore 문서 I/O, 유저 설정 접근자, 애널리틱스, 크래시리틱스, 리모트컨피그, 리더보드, 문의 메일. 그 결과 매니저·UI 6곳이 모두 이 클래스를 주입받는다. 각자 실제로 쓰는 건 일부뿐인데도 전체를 끌고 온다.

| 소비자 | 실제 사용 API |
|---|---|
| AdmobManager | `Log` |
| RoundManager | `LogModeStart`, `LogModeQuit`, `LogGameOver`, `SetCustomKey` |
| SoundManager | `IsBGMOn`, `IsSFXOn`, `IsLoadData` |
| GameManager | `IsLoadData`, `ClassicScore`, `IsSymbolOn`, `Log`, `LogEvent`, `LogModePause` |
| GameLobbyUI | `ShowLeaderboardUI` |
| InquriyUI | `SendInquiryAsync` |

**InputManager 참조가 UI 전반에 흩어져 있다.** 콜백 등록 방식이라 팝업·메뉴·보드가 각자 InputManager를 주입받고 구독/해제를 직접 관리한다. 특히 백키(`Game_Exit`)는 GameManager와 PopupQuestionUI가 **동시에** 구독한다. 지금 동작하는 이유는 GameManager가 먼저 구독돼 먼저 실행되고, 그 시점에 팝업이 살아있어 `ShowExitToast`의 `TryGetInstance` 가드에 걸려 빠져나가기 때문이다. **구독 순서에 의존하는 상태**이며 순서가 뒤집히면 팝업이 닫힌 뒤 새 팝업이 다시 뜬다.

## 목표

- FirebaseManager를 책임별로 분리해 각 소비자가 필요한 것만 주입받게 한다.
- 개별 UI가 InputManager를 직접 참조하지 않게 한다.
- 백키를 스택으로 모델링해 최상단 UI만 처리하게 한다.

## 비목표

- **인터페이스 추출 및 유닛 테스트 도입은 하지 않는다.** 결합도 정리가 목적이며, 추상화 계층은 추가하지 않는다. 나중에 테스트를 붙일 때 인터페이스를 얹기 쉬운 상태까지만 만든다.
- Addressable 격리는 이미 asmdef로 완성돼 있어 변경하지 않는다. `Game`/`UI` 어셈블리는 `Core_Resource`를 참조하지 않으며, `PrefabManager`의 공개 API도 `IAssetResource`를 시그니처에 노출하지 않는다.
- 리더보드·문의·강제업데이트는 분리하지 않는다. 각각 소비자가 1곳뿐이라 분리해도 결합도 이득이 없다.

## 설계

### 매니저 배치

```
FirebaseManager   SDK init + 인증 + Firestore I/O   (백엔드 전용)
      ↑
      ├── TelemetryManager   크래시리틱스 + 애널리틱스   (전역 공용, 무상태)
      └── UserSettings       UserData 소유 + 설정 접근자  (상태 보유)
```

의존은 단방향이다. **FirebaseManager는 TelemetryManager를 참조하지 않는다** — 자기 내부 에러 보고는 지금처럼 `Crashlytics`/`LLogger`를 직접 호출한다. 이렇게 해야 순환 의존이 생기지 않으며, Firebase 매니저가 Firebase API를 직접 쓰는 것은 자연스럽다.

소비자 의존성 변화:

| 소비자 | Before | After |
|---|---|---|
| AdmobManager | FirebaseManager | TelemetryManager |
| RoundManager | FirebaseManager | TelemetryManager |
| SoundManager | FirebaseManager | UserSettings |
| GameManager | FirebaseManager | TelemetryManager + UserSettings |
| GameLobbyUI | FirebaseManager | FirebaseManager (리더보드) |
| InquriyUI | FirebaseManager | FirebaseManager (문의) |

FirebaseManager 직접 참조가 6곳에서 3곳(UserSettings 내부, GameLobbyUI, InquriyUI)으로 준다.

### TelemetryManager (신규, `Core`)

FirebaseManager에서 애널리틱스·크래시리틱스 영역을 그대로 옮긴다. 플랫폼 분기(`#if UNITY_ANDROID || UNITY_EDITOR` ↔ `WebAnalyticsBridge`)도 함께 옮긴다.

```csharp
public class TelemetryManager : BaseManager
{
    public TelemetryManager(ManagerInitTracker tracker, FirebaseManager firebase);

    // Crashlytics
    void Log(string message);
    void LogError(Exception e);
    void SetCustomKey(string key, string value);

    // Analytics
    void LogEvent(string eventName);
    void LogEvent(string eventName, string paramName, string paramValue);
    void LogEvent(string eventName, params Parameter[] parameters);   // Android/Editor 전용
    void LogModeStart(string mode);
    void LogModeQuit(string mode, float playDurationSec, int currentScore);
    void LogModePause(string mode, float playDurationSec, int currentScore);
    void LogGameOver(string mode, int finalScore, int maxCombo);
}
```

FirebaseManager를 주입받는 이유는 초기화 여부 확인 하나뿐이다. 모든 메서드는 `firebase.IsInitialized`가 false면 no-op으로 빠진다. 생성자에서 `CompleteInit(ManagerType.Telemetry)`를 찍는다.

`Crashlytics.SetUserId`는 TelemetryManager로 옮기지 않는다. 인증 완료 시점을 아는 쪽이 FirebaseManager이고, 여기서 TelemetryManager를 호출하면 순환 의존이 생긴다. FirebaseManager가 지금처럼 `Crashlytics.SetUserId`를 직접 호출한다.

### UserSettings (신규, `Core`)

현재 FirebaseManager 안에 있는 `UserData _user` 소유권과 설정 접근자를 옮긴다.

```csharp
public class UserSettings : BaseManager
{
    public UserSettings(ManagerInitTracker tracker, FirebaseManager firebase);

    bool IsLoaded { get; }
    int  ClassicScore { get; set; }
    bool IsBGMOn { get; set; }
    bool IsSFXOn { get; set; }
    bool IsSymbolOn { get; set; }
}
```

각 setter는 기존 동작을 유지한다 — PlayerPrefs 미러 기록, `_user` 갱신, `IsDirty = true`, Firestore 저장 요청. `ClassicScore` setter의 리더보드 보고는 `firebase.ReportScore(value)` 호출로 바꾼다.

로드 흐름: 생성자에서 `LoadAsync().Forget()`으로 시작해 `CheckedManagers(ManagerType.Firebase)`로 SDK 준비를 기다린 뒤 `_user = await firebase.LoadUserAsync()`, 완료 후 `CompleteInit(ManagerType.UserSettings)`. 기존 `AddressableManager`/`ReferenceManager`가 생성자에서 비동기 초기화를 띄우는 패턴과 동일하다.

메서드 이름을 `Initialize`로 쓰지 않는다. `BaseManager`가 VContainer의 `IInitializable`을 구현하면서 이미 `virtual void Initialize()`를 갖고 있어 충돌한다.

### FirebaseManager (축소, `Core`)

남는 책임은 SDK 초기화, 인증, Firestore I/O, 리모트컨피그, 리더보드, 문의다.

```csharp
public class FirebaseManager : BaseManager
{
    bool IsInitialized { get; }
    bool IsUpdate { get; }
    string UserId { get; }
    string Nickname { get; }

    UniTask<UserData> LoadUserAsync();
    void SaveUser(UserData user);
    UniTask ReportScore(int score);

    UniTask CheckForForceUpdateAsync();
    UniTask ShowLeaderboardUI();
    UniTask<InquiryResult> SendInquiryAsync(string content, string userEmail = null);
}
```

제거 대상: `ClassicScore`/`IsBGMOn`/`IsSFXOn`/`IsSymbolOn` 접근자, `IsLoadData`, `_user` 필드, 애널리틱스·크래시리틱스 공개 메서드.

### 초기화 게이팅 의미 정리

현재 `CompleteInit(ManagerType.Firebase)`가 `SignInAuth()` **호출 직후** 실행된다. 인증은 콜백 체인이라 그 시점에 아직 끝나지 않았다. 그래서 GameManager가 별도로 `WaitUntil(() => IsLoadData)`를 한 번 더 건다.

새 구조에서 두 게이트의 의미를 분리한다.

- `ManagerType.Firebase` — SDK 준비 완료 (`CheckAndFixDependenciesAsync` 성공 시점)
- `ManagerType.UserSettings` — 유저 데이터 로드 완료

GameManager의 `CheckedManagers`에 `ManagerType.UserSettings`를 추가하고 `WaitUntil(() => IsLoadData)` 폴링을 제거한다.

`ManagerType`에 추가할 값:

```csharp
Telemetry    = 1 << 8,
UserSettings = 1 << 9,
```

### 남은 결함 처리

FirebaseManager의 WebGL 분기와 의존성 실패 분기에 `CompleteInit(ManagerType.Firebase)` 호출이 없다. 그 경로를 타면 `CheckedManagers` 대기가 끝나지 않는다. 이번 작업에서 양쪽 분기 모두 `CompleteInit`을 넣는다. 실패 분기는 `IsInitialized`를 false로 둔 채 게이트만 열어, 대기가 풀리되 Telemetry는 no-op으로 동작하게 한다.

### InputManager 백키 스택

게임플레이 입력 API는 그대로 둔다. 소비자가 Board 하나뿐이라 직접 구독이 적절하다.

```csharp
// 기존 유지
void SubscribeToInputHandler(InputType type, Action<CallbackContext> start, Action<CallbackContext> perform, Action<CallbackContext> cancel);
void UnsubscribeToInputHandler(InputType type, ...);

// 신규 — 백키 스택
void PushBackHandler(Action handler);
void PopBackHandler(Action handler);

// 신규 — 입력 차단 (UseInputHandler 대체)
void PushInputBlock();
void PopInputBlock();
```

InputManager는 `Game_Exit`를 내부에서 한 번만 구독하고, 발생 시 스택 최상단 핸들러만 호출한다. 스택이 비면 아무것도 하지 않는다.

`PopBackHandler`는 전달받은 핸들러를 스택에서 제거하되, 최상단이 아니어도 안전하게 제거한다(중간 UI가 먼저 닫히는 경우 대비).

`PushInputBlock`/`PopInputBlock`은 depth 카운터로 관리하며, depth > 0이면 게임플레이 액션(`Player_Touch`, `Player_Point`)을 비활성화한다. 백키는 차단하지 않는다. 기존 `UseInputHandler` 프로퍼티는 제거한다.

### BaseUI 훅

배선을 베이스 클래스가 흡수해 개별 UI가 InputManager를 참조하지 않게 한다.

```csharp
public class BaseUI : MonoBehaviour, IBaseUI
{
    protected InputManager m_input;

    [Inject]
    public void ConstructBaseUI(InputManager input) => m_input = input;

    protected virtual bool UseBackKey => false;
    protected virtual void OnBackKey() => Close();

    public virtual void Init()
    {
        gameObject.SetActive(true);
        if (UseBackKey) m_input.PushBackHandler(OnBackKey);
    }

    public virtual void Close()
    {
        if (UseBackKey) m_input.PopBackHandler(OnBackKey);
        gameObject.SetActive(false);
    }

    protected virtual void OnDestroy()
    {
        if (UseBackKey) m_input?.PopBackHandler(OnBackKey);
    }
}
```

`PopupQuestionUI`는 `UseBackKey => true`, `OnBackKey() => OnClickCloseBtn()`으로 오버라이드한다. `MenuUI`는 `UseBackKey => true`와 함께 `Init`/`Close`에서 `PushInputBlock`/`PopInputBlock`을 호출한다.

**푸시 시점을 `OnEnable`이 아니라 `Init()`으로 잡는 이유**: Addressables는 오브젝트를 활성 상태로 생성하므로 `Awake`/`OnEnable`이 VContainer 주입보다 먼저 실행된다. 그 시점엔 `m_input`이 null이다. `Init()`은 인스턴스화가 끝난 뒤 소유자가 명시적으로 호출하므로 주입 완료가 보장된다.

### VContainer 동작 확인 사항

`TypeAnalyzer`는 `type.BaseType`을 타고 올라가며 `[Inject]` 메서드를 수집한다. 따라서 베이스 클래스의 `[Inject]` 메서드도 호출된다. 다만 두 가지 제약이 있다.

1. **호출 순서는 파생 → 베이스다.** 파생 클래스의 `Construct` 안에서 베이스가 주입한 `m_input`을 사용하면 안 된다.
2. 베이스와 파생의 `[Inject]` 메서드 이름을 다르게 둔다(`ConstructBaseUI` vs `Construct`). 같은 이름의 오버라이드 관계면 `GetBaseDefinition()` 중복 제거에 걸린다.

### Close 경로 통일

`GameOverUI.OnClickCloseBtn()`이 `Close()`를 거치지 않고 `gameObject.SetActive(false)`를 직접 호출한다. 이대로면 백키 스택 pop이 누락된다. `Close()` 경유로 바꾼다. 다른 UI들도 같은 패턴이 있는지 점검한다.

## 작업 순서

각 단계는 독립적이며, 중간에 멈춰도 빌드가 깨지지 않는다.

1. **TelemetryManager 추출** — 애널리틱스·크래시리틱스를 옮기고 AdmobManager·RoundManager·GameManager의 주입을 교체한다.
2. **UserSettings 추출** — `UserData` 소유권을 옮기고 SoundManager·GameManager의 주입을 교체한다. 초기화 게이팅 의미를 정리하고 WebGL/실패 분기의 `CompleteInit` 누락을 고친다.
3. **InputManager 백키 스택 + BaseUI 훅** — `PushBackHandler`/`PopBackHandler`/`PushInputBlock`/`PopInputBlock`을 추가하고 BaseUI에 훅을 넣는다. PopupQuestionUI·MenuUI·GameManager에서 백키 직접 구독을 제거한다.
4. **Close 경로 통일** — GameOverUI를 포함해 `Close()`를 우회하는 곳을 정리한다.

## 영향 파일

**신규**
- `Assets/Scripts/Core/TelemetryManager.cs`
- `Assets/Scripts/Core/UserSettings.cs`

**수정**
- `Assets/Scripts/Core/FirebaseManager.cs` — 책임 축소
- `Assets/Scripts/Core/AdmobManager.cs`, `SoundManager.cs`, `GameManager.cs`, `InputManager.cs`
- `Assets/Scripts/Game/RoundManager.cs`
- `Assets/Scripts/UI/BaseUI.cs`, `PopupQuestionUI.cs`, `MenuUI.cs`, `GameOverUI.cs`, `GameLobbyUI.cs`, `InquriyUI.cs`
- `Assets/Scripts/Share/Enum/ManagerType.cs` — `Telemetry`, `UserSettings` 추가
- `Assets/Scripts/GameLifetimeScope.cs` — 신규 매니저 2개 등록

## 검증

유닛 테스트는 도입하지 않으므로 에디터 플레이 확인으로 검증한다.

1. 플레이 진입 시 매니저 생성자 로그가 전부 찍히고 `Bootstrap`이 로비 UI까지 도달하는지
2. BGM/SFX/심볼 토글이 PlayerPrefs와 Firestore 양쪽에 반영되는지
3. 게임 진행 시 애널리틱스 이벤트가 기존과 동일하게 발생하는지
4. 백키 동작: 로비에서 종료 팝업 → 팝업만 닫힘(새 팝업이 다시 뜨지 않음), 메뉴 열린 상태에서 백키 → 메뉴만 닫힘
5. 메뉴가 열린 동안 보드 드래그가 차단되는지

## 위험 요소

- **UserData 소유권 이동**이 가장 위험하다. 저장 경로가 PlayerPrefs와 Firestore 두 갈래라 한쪽만 반영되는 회귀가 생기기 쉽다. 2단계 작업 후 설정 토글을 반드시 수동 확인한다.
- **백키 스택 누수**. `Close()`를 우회하는 경로가 남아 있으면 스택에 죽은 핸들러가 쌓인다. `OnDestroy` 안전망을 두되 4단계에서 우회 경로를 실제로 제거한다.
- 리모트컨피그 강제업데이트 팝업은 `PrefabManager`를 통해 UI를 띄우는데, FirebaseManager가 `PrefabManager`를 계속 주입받아야 한다. 이 의존은 유지된다.
