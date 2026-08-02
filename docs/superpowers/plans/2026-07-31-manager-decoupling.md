# 매니저 결합도 정리 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** FirebaseManager의 9개 책임을 TelemetryManager/UserSettings로 분리하고, InputManager 백키를 스택으로 모델링해 개별 UI가 InputManager를 직접 참조하지 않게 한다.

**Architecture:** `FirebaseManager`(SDK+인증+Firestore I/O)를 백엔드 전용으로 축소하고, 그 위에 `TelemetryManager`(로그·애널리틱스)와 `UserSettings`(유저 설정 상태)를 단방향 의존으로 얹는다. FirebaseManager는 두 신규 매니저를 참조하지 않아 순환이 없다. 입력은 백키를 스택으로 만들고 `BaseUI`가 push/pop 배선을 흡수한다.

**Tech Stack:** Unity 2022+, VContainer 1.x, UniTask, Firebase (Analytics/Crashlytics/Firestore/Auth/RemoteConfig), Google Play Games, Addressables

**설계 문서:** `docs/superpowers/specs/2026-07-31-manager-decoupling-design.md`

## Global Constraints

- **인터페이스를 추출하지 않는다.** 목적은 결합도 정리이며 추상화 계층 추가는 비목표다.
- **유닛 테스트를 작성하지 않는다.** 프로젝트에 테스트 asmdef가 없고 도입도 비목표다. 각 태스크의 검증은 **Unity 에디터 컴파일 확인 + 플레이 모드 수동 확인**으로 한다.
- 플랫폼 분기는 기존 관례를 따른다: `#if UNITY_ANDROID || UNITY_EDITOR` (Firebase 실경로) ↔ `#else` (WebGL, `WebAnalyticsBridge`).
- 모든 매니저는 `BaseManager`를 상속하고 `GameLifetimeScope`에 `RegisterEntryPoint<T>(Lifetime.Singleton).AsSelf()`로 등록한다.
- `BaseManager`는 이미 `IInitializable`을 구현하며 `virtual void Initialize()`를 갖는다. **신규 매니저에서 `Initialize`라는 이름을 재사용하지 않는다.**
- 비동기 초기화는 생성자에서 `XxxAsync().Forget()`으로 띄우는 기존 패턴(`AddressableManager.SetAddressable`, `ReferenceManager.Init`)을 따른다.
- PlayerPrefs 키는 `SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.X)]`로 접근한다. 문자열 리터럴을 직접 쓰지 않는다.
- 커밋 메시지는 한국어로 쓰고 다음 줄로 끝낸다: `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`

---

## File Structure

**신규**

| 파일 | 책임 |
|---|---|
| `CB/Assets/Scripts/Core/TelemetryManager.cs` | 크래시리틱스 + 애널리틱스 공개 창구. 무상태. |
| `CB/Assets/Scripts/Core/UserSettings.cs` | `UserData` 소유. 설정 접근자 + PlayerPrefs 미러 + Firestore 저장 트리거. |

**수정**

| 파일 | 변경 |
|---|---|
| `CB/Assets/Scripts/Share/Enum/ManagerType.cs` | `Telemetry`, `UserSettings` 추가 |
| `CB/Assets/Scripts/Core/FirebaseManager.cs` | 애널리틱스·크래시리틱스·설정 접근자 제거, `LoadUserAsync`/`SaveUser`/`ReportScore` 노출 |
| `CB/Assets/Scripts/Core/AdmobManager.cs` | `FirebaseManager` → `TelemetryManager` |
| `CB/Assets/Scripts/Game/RoundManager.cs` | `FirebaseManager` → `TelemetryManager` |
| `CB/Assets/Scripts/Core/GameManager.cs` | `FirebaseManager` → `TelemetryManager` + `UserSettings`, 백키를 스택으로 |
| `CB/Assets/Scripts/Core/SoundManager.cs` | `FirebaseManager` → `UserSettings` |
| `CB/Assets/Scripts/Core/InputManager.cs` | 백키 스택 + 입력 차단 스택 추가, `UseInputHandler` 제거 |
| `CB/Assets/Scripts/UI/BaseUI.cs` | `InputManager` 주입 + 백키 훅 |
| `CB/Assets/Scripts/UI/PopupQuestionUI.cs` | 백키 직접 구독 제거 |
| `CB/Assets/Scripts/UI/MenuUI.cs` | `UseInputHandler` → `PushInputBlock`/`PopInputBlock` |
| `CB/Assets/Scripts/UI/GameOverUI.cs` | `OnClickCloseBtn`을 `Close()` 경유로 |
| `CB/Assets/Scripts/UI/GameLobbyUI.cs` | `OnDestroy` 시그니처를 `protected override`로 |
| `CB/Assets/Scripts/UI/IngameScoreUI.cs` | `OnDestroy` 시그니처를 `protected override`로 |
| `CB/Assets/Scripts/GameLifetimeScope.cs` | 신규 매니저 2개 등록 |

**변경 없음 (확인만)**

- `CB/Assets/Scripts/UI/SoundSettingUI.cs` — `m_soundManager.IsBGMOn/IsSFXOn`을 쓰는데, 이 프로퍼티는 `SoundManager`에 그대로 남는다.
- `CB/Assets/Scripts/UI/InquriyUI.cs`, `GameLobbyUI.cs`의 `FirebaseManager` 사용 — 문의·리더보드는 FirebaseManager에 남는다.

---

## Task 1: TelemetryManager 추출

로그·애널리틱스를 별도 매니저로 빼고 외부 소비자(Admob, RoundManager, GameManager)를 옮긴다. **FirebaseManager의 애널리틱스 메서드는 이 태스크에서 지우지 않는다** — 내부 설정 setter(`bgm_off` 등)가 아직 쓰고 있어 Task 2에서 함께 정리한다.

**Files:**
- Create: `CB/Assets/Scripts/Core/TelemetryManager.cs`
- Modify: `CB/Assets/Scripts/Share/Enum/ManagerType.cs`
- Modify: `CB/Assets/Scripts/Core/AdmobManager.cs`
- Modify: `CB/Assets/Scripts/Game/RoundManager.cs`
- Modify: `CB/Assets/Scripts/Core/GameManager.cs`
- Modify: `CB/Assets/Scripts/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `BaseManager(ManagerInitTracker)`, `FirebaseManager.IsInitialized`
- Produces: `TelemetryManager` — `Log(string)`, `LogError(Exception)`, `SetCustomKey(string, string)`, `LogEvent(string)`, `LogEvent(string, string, string)`, `LogModeStart(string)`, `LogModeQuit(string, float, int)`, `LogModePause(string, float, int)`, `LogGameOver(string, int, int)`. Android/Editor 전용 오버로드로 `LogEvent(string, params Parameter[])`.

- [ ] **Step 1: `ManagerType`에 값 추가**

`CB/Assets/Scripts/Share/Enum/ManagerType.cs` 전체를 다음으로 교체한다.

```csharp
public enum ManagerType
{
    Addressable  = 1 << 0,
    Prefab       = 1 << 1,
    Sound        = 1 << 2,
    TextData     = 1 << 3,
    Input        = 1 << 4,
    Firebase     = 1 << 5,
    Admob        = 1 << 6,
    Game         = 1 << 7,
    Telemetry    = 1 << 8,
    UserSettings = 1 << 9,
}
```

- [ ] **Step 2: `TelemetryManager` 생성**

`CB/Assets/Scripts/Core/TelemetryManager.cs`를 새로 만든다.

```csharp
using System;
#if UNITY_ANDROID || UNITY_EDITOR
using Firebase.Analytics;
using Firebase.Crashlytics;
#endif

/// <summary>
/// 앱 전역의 로그/애널리틱스 창구. 무상태이며 실패해도 게임 흐름에 영향을 주지 않는다.
/// Firebase SDK 초기화 전에는 모든 호출이 no-op으로 빠진다.
/// </summary>
public class TelemetryManager : BaseManager
{
    private readonly FirebaseManager m_firebase;

    public TelemetryManager(ManagerInitTracker tracker, FirebaseManager firebase) : base(tracker)
    {
        LLogger.Log("TelemetryManager");
        m_firebase = firebase;
        CompleteInit(ManagerType.Telemetry);
    }

#if UNITY_ANDROID || UNITY_EDITOR

    #region Crashlytics

    public void Log(string message)
    {
        if (!m_firebase.IsInitialized)
            return;
        Crashlytics.Log(message);
    }

    public void LogError(Exception e)
    {
        if (!m_firebase.IsInitialized)
            return;
        Crashlytics.LogException(e);
    }

    public void SetCustomKey(string key, string value)
    {
        if (!m_firebase.IsInitialized)
            return;
        Crashlytics.SetCustomKey(key, value);
    }

    #endregion

    #region Analytics

    public void LogEvent(string eventName)
    {
        if (!m_firebase.IsInitialized)
            return;
        FirebaseAnalytics.LogEvent(eventName);
    }

    public void LogEvent(string eventName, string paramName, string paramValue)
    {
        if (!m_firebase.IsInitialized)
            return;
        FirebaseAnalytics.LogEvent(eventName, new Parameter(paramName, paramValue));
    }

    public void LogEvent(string eventName, params Parameter[] parameters)
    {
        if (!m_firebase.IsInitialized)
            return;
        FirebaseAnalytics.LogEvent(eventName, parameters);
    }

    public void LogModeStart(string mode)
    {
        LogEvent("game_start", new Parameter("mode", mode));
    }

    public void LogModeQuit(string mode, float playDurationSec, int currentScore)
    {
        LogEvent("game_quit",
            new Parameter("mode", mode),
            new Parameter("play_duration_sec", playDurationSec),
            new Parameter("score", currentScore));
    }

    public void LogModePause(string mode, float playDurationSec, int currentScore)
    {
        LogEvent("game_pause",
            new Parameter("mode", mode),
            new Parameter("play_duration_sec", playDurationSec),
            new Parameter("score", currentScore));
    }

    public void LogGameOver(string mode, int finalScore, int maxCombo)
    {
        LogEvent("game_over",
            new Parameter("mode", mode),
            new Parameter("final_score", finalScore),
            new Parameter("max_combo", maxCombo));
    }

    #endregion

#else

    #region Crashlytics (WebGL no-op)

    public void Log(string message) { }
    public void LogError(Exception e) { }
    public void SetCustomKey(string key, string value) { }

    #endregion

    #region Analytics (WebGL)

    public void LogEvent(string eventName)
        => WebAnalyticsBridge.LogEvent(eventName);

    public void LogEvent(string eventName, string paramName, string paramValue)
        => WebAnalyticsBridge.LogEvent(eventName, paramName, paramValue);

    public void LogModeStart(string mode)
        => WebAnalyticsBridge.LogModeStart(mode);

    public void LogModeQuit(string mode, float playDurationSec, int currentScore)
        => WebAnalyticsBridge.LogModeQuit(mode, playDurationSec, currentScore);

    public void LogModePause(string mode, float playDurationSec, int currentScore)
        => WebAnalyticsBridge.LogModePause(mode, playDurationSec, currentScore);

    public void LogGameOver(string mode, int finalScore, int maxCombo)
        => WebAnalyticsBridge.LogGameOver(mode, finalScore, maxCombo);

    #endregion

#endif
}
```

- [ ] **Step 3: `GameLifetimeScope`에 등록**

`CB/Assets/Scripts/GameLifetimeScope.cs`의 `FirebaseManager` 등록 **바로 다음 줄**에 추가한다.

```csharp
builder.RegisterEntryPoint<FirebaseManager>(Lifetime.Singleton).AsSelf();
builder.RegisterEntryPoint<TelemetryManager>(Lifetime.Singleton).AsSelf();
```

- [ ] **Step 4: `AdmobManager`를 TelemetryManager로 교체**

`CB/Assets/Scripts/Core/AdmobManager.cs`에서 필드와 생성자를 바꾼다.

```csharp
    private BannerView bannerView;
    private TelemetryManager m_telemetry;
    public AdmobManager(ManagerInitTracker tracker, TelemetryManager telemetry) : base(tracker)
    {
        Logging("Admob 초기화");
        m_telemetry = telemetry;
        RequestConsent();
    }
```

그리고 두 호출부를 바꾼다.

- `CreateBanner()` 안: `m_firebaseManager.Log("Bottom Admob Banner Create");` → `m_telemetry.Log("Bottom Admob Banner Create");`
- `CreateInterstitial()` 안: `m_firebaseManager.Log("Update High Score Admob Create");` → `m_telemetry.Log("Update High Score Admob Create");`

- [ ] **Step 5: `RoundManager`를 TelemetryManager로 교체**

`CB/Assets/Scripts/Game/RoundManager.cs`에서 필드 선언과 `Construct`를 바꾼다.

```csharp
    private GameManager m_gameManager;
    private PrefabManager m_prefabManager;
    private TelemetryManager m_telemetry;
```

```csharp
    [Inject]
    public void Construct(GameManager gameManager, PrefabManager prefabManager, TelemetryManager telemetry)
    {
        m_gameManager = gameManager;
        m_prefabManager = prefabManager;
        m_telemetry = telemetry;
    }
```

호출부 5곳을 바꾼다.

- `EnterRound()`: `m_firebaseManager.LogModeStart("Classic");` → `m_telemetry.LogModeStart("Classic");`
- `EndRound()`: `m_firebaseManager.SetCustomKey("mode", "Classic");` → `m_telemetry.SetCustomKey("mode", "Classic");`
- `EndRound()`: `m_firebaseManager.LogGameOver(...)` → `m_telemetry.LogGameOver(...)`
- `ExitRound()`: `m_firebaseManager.LogModeQuit(...)` → `m_telemetry.LogModeQuit(...)`
- `DestroyMatchBricks()`: `m_firebaseManager.SetCustomKey("score", ...)` → `m_telemetry.SetCustomKey("score", ...)`

- [ ] **Step 6: `GameManager`에 TelemetryManager 주입**

`CB/Assets/Scripts/Core/GameManager.cs`에 필드를 추가하고 생성자에 파라미터를 넣는다. `FirebaseManager`는 Task 2에서 제거하므로 **이 태스크에서는 유지**한다.

```csharp
    private FirebaseManager m_firebaseManager;
    private InputManager m_inputManger;
    private PrefabManager m_prefabManager;
    private TelemetryManager m_telemetry;

    public GameManager(
        ManagerInitTracker tracker,
        FirebaseManager firebaseManager,
        InputManager inputManger,
        PrefabManager prefabManager,
        TelemetryManager telemetry) : base(tracker)
    {
        LLogger.Log("GameManager");
        m_firebaseManager = firebaseManager;
        m_inputManger = inputManger;
        m_prefabManager = prefabManager;
        m_telemetry = telemetry;
        Bootstrap().Forget();
    }
```

`OnApplicationPause`/`OnApplicationQuit` 안의 호출을 바꾼다. (두 메서드는 현재 호출되지 않는 죽은 코드다. Task 5에서 되살린다.)

```csharp
    private void OnApplicationPause(bool pause)
    {
        if (!pause)
            return;

        PlayerPrefs.Save();

        if (_roundManager != null)
            m_telemetry.LogModePause("Classic", Time.realtimeSinceStartup - catureEnterTime, _roundManager.CurrentScore);

        m_telemetry.Log("App paused");
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
        m_telemetry.LogEvent("app_quit", "real_time", Time.realtimeSinceStartup.ToString());
    }
```

- [ ] **Step 7: 컴파일 확인**

Unity 에디터로 전환해 자동 컴파일을 트리거한다. Console 창에 컴파일 에러가 없어야 한다.

기대 결과: 에러 0건. `m_firebaseManager`를 참조하는 곳이 `AdmobManager`/`RoundManager`에 남아 있으면 "does not exist in the current context" 에러가 나므로, 그 경우 Step 4~5에서 놓친 호출부를 찾아 고친다.

- [ ] **Step 8: 플레이 모드 확인**

에디터에서 플레이한다.

기대 결과:
- Console에 `TelemetryManager` 로그가 다른 매니저 생성자 로그들과 함께 찍힌다.
- `Bootstrap` 로그가 찍히고 로비 UI까지 도달한다.
- 게임을 한 판 시작했다가 게임오버까지 진행해 `NullReferenceException`이 없는지 확인한다. 이 경로에서 `LogModeStart`(라운드 진입) → `SetCustomKey`(점수 갱신) → `LogGameOver`(종료)가 순서대로 호출된다.
- 애널리틱스 이벤트는 Firebase 콘솔 반영에 지연이 있으므로 즉시 확인이 어렵다. `TelemetryManager`의 각 메서드 첫 줄에 임시로 `LLogger.Log`를 넣어 호출 여부만 확인하고, 확인 후 제거하는 방법을 쓸 수 있다.

- [ ] **Step 9: 커밋**

```bash
git add CB/Assets/Scripts/Core/TelemetryManager.cs \
        CB/Assets/Scripts/Core/TelemetryManager.cs.meta \
        CB/Assets/Scripts/Share/Enum/ManagerType.cs \
        CB/Assets/Scripts/Core/AdmobManager.cs \
        CB/Assets/Scripts/Game/RoundManager.cs \
        CB/Assets/Scripts/Core/GameManager.cs \
        CB/Assets/Scripts/GameLifetimeScope.cs
git commit -m "refactor: TelemetryManager 추출

로그/애널리틱스를 FirebaseManager에서 분리해 AdmobManager와
RoundManager가 Firebase를 직접 참조하지 않게 한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: UserSettings 추출 및 초기화 게이팅 정리

`UserData` 소유권을 `UserSettings`로 옮기고, FirebaseManager를 Firestore I/O 창구로 축소한다. 이 시점에 FirebaseManager의 애널리틱스·크래시리틱스 공개 메서드는 호출자가 사라지므로 삭제한다.

**Files:**
- Create: `CB/Assets/Scripts/Core/UserSettings.cs`
- Modify: `CB/Assets/Scripts/Core/FirebaseManager.cs`
- Modify: `CB/Assets/Scripts/Core/SoundManager.cs`
- Modify: `CB/Assets/Scripts/Core/GameManager.cs`
- Modify: `CB/Assets/Scripts/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `TelemetryManager.LogEvent(string)` (Task 1), `ManagerType.UserSettings` (Task 1)
- Produces:
  - `UserSettings` — `bool IsLoaded`, `int ClassicScore`, `bool IsBGMOn`, `bool IsSFXOn`, `bool IsSymbolOn`
  - `FirebaseManager` — `UniTask<UserData> LoadUserAsync()`, `void SaveUser(UserData)`, `UniTask ReportScore(int)`

- [ ] **Step 1: `FirebaseManager`에 Firestore I/O API 노출**

`CB/Assets/Scripts/Core/FirebaseManager.cs`에서 다음을 **삭제**한다.

- `public bool IsLoadData => _user != null;`
- `private UserData _user = null;`
- `ClassicScore`, `IsBGMOn`, `IsSFXOn`, `IsSymbolOn` 프로퍼티 4개 전부
- `LogEvent` 오버로드 3개, `LogModeStart`, `LogModeQuit`, `LogModePause`, `LogGameOver`
- `public void Log(string)`, `public void LogError(Exception)`, `public void SetCustomKey(string, string)` (Android/Editor 분기와 `#else` 분기 양쪽)
- `TryReportLeaderboard()` (아래에서 `ReportScore`로 대체)

`InitCrashlytics()`는 **남긴다** — Firebase 초기화 직후 자기가 호출한다.

내부에서 쓰던 `LogError(e)` 호출 3곳(`TryPlayGamesAuthentication`, `LoadUserData`, `CheckForForceUpdateAsync`)은 `Crashlytics.LogException(e)` 직접 호출로 바꾼다. WebGL 분기에서는 이 메서드들이 `#if UNITY_ANDROID || UNITY_EDITOR` 안에 있으므로 문제없다.

`InitializeFirebase()`를 다음으로 교체한다. **의존성 실패 분기와 WebGL 분기에도 `CompleteInit`을 넣는다** — 없으면 `CheckedManagers` 대기가 영원히 끝나지 않는다.

```csharp
    private void InitializeFirebase()
    {
#if UNITY_ANDROID || UNITY_EDITOR
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var status = task.Result;
            if (status != DependencyStatus.Available)
            {
                Error($"Firebase 의존성 문제: {status}");
                // IsInitialized는 false로 두되 게이트는 연다.
                // 대기가 풀리고 Telemetry/Firestore 호출은 전부 no-op으로 빠진다.
                CompleteInit(ManagerType.Firebase);
                return;
            }
            _firestore = FirebaseFirestore.DefaultInstance;
            Logging("Firebase 초기화 완료");

            InitCrashlytics();
            SignInAuth();
            IsInitialized = true;
            CompleteInit(ManagerType.Firebase);
        });
#else
        Logging("WebGL: PlayerPrefs 기반 로컬 데이터로 초기화");
        IsInitialized = true;
        CompleteInit(ManagerType.Firebase);
#endif
    }
```

`SignInAuth()`의 세 갈래에 있던 `LoadUserData().Forget()` 호출을 **전부 제거**한다. 이제 `UserSettings`가 로딩을 주도한다. `SignInAuth(Task<AuthResult>)` 안의 `LoadUserData().Forget()`도 제거한다.

기존 `SaveUserData()`를 다음으로 교체한다.

```csharp
    public void SaveUser(UserData user)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        PlayerPrefs.Save();
        if (!IsInitialized || string.IsNullOrEmpty(UserId) || user == null)
        {
            Warning("Firestore 저장 실패: 아직 초기화/로그인되지 않음");
            return;
        }
        LLogger.Log("Save Firestore");
        var docRef = _firestore.Collection(USERS_COLLECTION).Document(UserId);

        docRef.SetAsync(user, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
                Error($"Firestore 저장 실패 ({UserId}): {task.Exception}");

            user.IsDirty = false;
        });
#else
        PlayerPrefs.Save();
#endif
    }
```

기존 `LoadUserData()`를 다음으로 교체한다. 인증 완료를 기다리되, 실패해도 로컬 데이터로 진행한다.

```csharp
    private const float AUTH_WAIT_SECONDS = 10f;

    /// <summary>
    /// 유저 문서를 읽어온다. 인증이 끝나지 않았으면 최대 AUTH_WAIT_SECONDS 대기하고,
    /// 그래도 안 되면 PlayerPrefs 기반 로컬 UserData를 돌려준다. 절대 null을 반환하지 않는다.
    /// </summary>
    public async UniTask<UserData> LoadUserAsync()
    {
#if UNITY_ANDROID || UNITY_EDITOR
        if (IsInitialized)
        {
            float deadline = Time.realtimeSinceStartup + AUTH_WAIT_SECONDS;
            await UniTask.WaitUntil(() =>
                !string.IsNullOrEmpty(UserId) || Time.realtimeSinceStartup > deadline);

            if (!string.IsNullOrEmpty(UserId))
                return await FetchUserDocumentAsync();

            Warning("인증 대기 시간 초과. 로컬 데이터로 진행한다.");
        }
#endif
        Logging("로컬 PlayerPrefs 기반 UserData로 진행");
        return new UserData();
    }

#if UNITY_ANDROID || UNITY_EDITOR
    private async UniTask<UserData> FetchUserDocumentAsync()
    {
        Logging("유저 데이터 로드 시작");
        var docRef = _firestore.Collection(USERS_COLLECTION).Document(UserId);
        try
        {
            var snapshot = await docRef.GetSnapshotAsync().AsUniTask();

            if (snapshot.Exists)
            {
                Logging("유저 데이터 로드 완료");
                return snapshot.ConvertTo<UserData>();
            }

            Logging("신규 유저, 초기 문서 생성");
            var created = new UserData();
            created.CreatedAt = Timestamp.GetCurrentTimestamp();
            SaveUser(created);
            return created;
        }
        catch (Exception e)
        {
            Crashlytics.LogException(e);
            Error(e.ToString());
            return new UserData();
        }
    }
#endif
```

`ReportScore`를 추가한다. 기존 `TryReportLeaderboard()`가 `ClassicScore`를 직접 읽던 것을 인자로 받게 바꾼 것이다.

```csharp
#if UNITY_ANDROID || UNITY_EDITOR
    public async UniTask ReportScore(int score)
    {
        if (await IsAuthenticated())
            PlayGamesPlatform.Instance.ReportScore(score, GPGSIds.leaderboard_high_score, ResultReportLeaderboard);
    }

    private void ResultReportLeaderboard(bool isComplete)
    {
        Logging($"리더보드 보고 결과: {isComplete}");
    }
#else
    public UniTask ReportScore(int score) => UniTask.CompletedTask;
#endif
```

`ResultReportLeaderboard`가 원래 `LogEvent("report_leaderboard", ...)`를 호출했지만, FirebaseManager는 더 이상 애널리틱스를 갖지 않는다. TelemetryManager를 주입하면 순환이 생기므로 `Logging`으로 대체한다.

`SendInquiryAsync`의 `LogError(e)`도 `Crashlytics.LogException(e)`로 바꾼다.

- [ ] **Step 2: `UserSettings` 생성**

`CB/Assets/Scripts/Core/UserSettings.cs`를 새로 만든다.

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 유저 설정 상태의 단일 소유자. PlayerPrefs를 즉시 미러링하고 Firestore 저장을 요청한다.
/// 로드가 끝나기 전에는 IsLoaded가 false이며, 접근자를 호출하면 안 된다.
/// </summary>
public class UserSettings : BaseManager
{
    private readonly FirebaseManager m_firebase;
    private readonly TelemetryManager m_telemetry;
    private UserData _user;

    public UserSettings(ManagerInitTracker tracker, FirebaseManager firebase, TelemetryManager telemetry)
        : base(tracker)
    {
        LLogger.Log("UserSettings");
        m_firebase = firebase;
        m_telemetry = telemetry;
        LoadAsync().Forget();
    }

    public bool IsLoaded => _user != null;

    private async UniTask LoadAsync()
    {
        await CheckedManagers(ManagerType.Firebase);
        _user = await m_firebase.LoadUserAsync();
        CompleteInit(ManagerType.UserSettings);
        Logging("유저 설정 준비 완료");
    }

    public int ClassicScore
    {
        get => _user.ClassicScore;
        set
        {
            SetPref(SaveFieldType.HighScore_Classic, value);
            _user.ClassicScore = value;
            _user.IsDirty = true;
            m_firebase.SaveUser(_user);
            m_firebase.ReportScore(value).Forget();
        }
    }

    public bool IsBGMOn
    {
        get => _user.IsBGMOn;
        set
        {
            SetPref(SaveFieldType.IsBGMOn, value ? 1 : 0);
            if (!value) m_telemetry.LogEvent("bgm_off");
            _user.IsBGMOn = value;
            _user.IsDirty = true;
            m_firebase.SaveUser(_user);
        }
    }

    public bool IsSFXOn
    {
        get => _user.IsSFXOn;
        set
        {
            SetPref(SaveFieldType.IsSFXOn, value ? 1 : 0);
            if (!value) m_telemetry.LogEvent("sfx_off");
            _user.IsSFXOn = value;
            _user.IsDirty = true;
            m_firebase.SaveUser(_user);
        }
    }

    public bool IsSymbolOn
    {
        get => _user.IsSymbolOn;
        set
        {
            SetPref(SaveFieldType.IsSymbolOn, value ? 1 : 0);
            if (value) m_telemetry.LogEvent("symbol_on");
            _user.IsSymbolOn = value;
            _user.IsDirty = true;
            m_firebase.SaveUser(_user);
        }
    }

    private static void SetPref(SaveFieldType field, int value)
    {
        PlayerPrefs.SetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(field)], value);
    }
}
```

기존 코드가 bool을 `value.GetHashCode()`로 저장했는데 `bool.GetHashCode()`는 true=1, false=0을 돌려주므로 `value ? 1 : 0`과 저장값이 같다. 읽는 쪽(`UserData` 생성자)이 `> 0`으로 비교하므로 호환된다.

- [ ] **Step 3: `GameLifetimeScope`에 등록**

`TelemetryManager` 등록 바로 다음 줄에 추가한다.

```csharp
builder.RegisterEntryPoint<TelemetryManager>(Lifetime.Singleton).AsSelf();
builder.RegisterEntryPoint<UserSettings>(Lifetime.Singleton).AsSelf();
```

- [ ] **Step 4: `SoundManager`를 UserSettings로 교체**

`CB/Assets/Scripts/Core/SoundManager.cs`의 필드와 생성자를 바꾼다.

```csharp
    private float _sfxVolumPer;
    private UserSettings m_userSettings;

    public SoundManager(ManagerInitTracker tracker, AddressableManager addressablemanager, UserSettings userSettings)
        : base(tracker, addressablemanager)
    {
        LLogger.Log("SoundManager");
        m_userSettings = userSettings;
    }
```

`IsBGMOn`/`IsSFXOn` 프로퍼티의 대상만 바꾼다. **프로퍼티 자체는 남긴다** — `SoundSettingUI`가 쓰고 있다.

```csharp
    public bool IsBGMOn
    {
        get { return m_userSettings.IsBGMOn; }
        set
        {
            if (m_userSettings.IsBGMOn == value)
                return;

            m_userSettings.IsBGMOn = value;
            _bgmAudio.mute = !value;
        }
    }

    public bool IsSFXOn
    {
        get { return m_userSettings.IsSFXOn; }
        set
        {
            if (m_userSettings.IsSFXOn == value)
                return;

            m_userSettings.IsSFXOn = value;
            _sfxAudio.mute = !value;
        }
    }
```

`LoadSaveFieldData()`를 게이트 대기로 바꾼다.

```csharp
    async private UniTask LoadSaveFieldData()
    {
        await CheckedManagers(ManagerType.UserSettings);
        _bgmAudio.mute = !m_userSettings.IsBGMOn;
        _sfxAudio.mute = !m_userSettings.IsSFXOn;
    }
```

- [ ] **Step 5: `GameManager`에서 FirebaseManager 제거**

`CB/Assets/Scripts/Core/GameManager.cs`의 필드와 생성자에서 `FirebaseManager`를 빼고 `UserSettings`를 넣는다.

```csharp
    private InputManager m_inputManger;
    private PrefabManager m_prefabManager;
    private TelemetryManager m_telemetry;
    private UserSettings m_userSettings;

    public GameManager(
        ManagerInitTracker tracker,
        InputManager inputManger,
        PrefabManager prefabManager,
        TelemetryManager telemetry,
        UserSettings userSettings) : base(tracker)
    {
        LLogger.Log("GameManager");
        m_inputManger = inputManger;
        m_prefabManager = prefabManager;
        m_telemetry = telemetry;
        m_userSettings = userSettings;
        Bootstrap().Forget();
    }
```

`HighScore`와 `IsSymbolOn`의 대상을 바꾼다.

```csharp
    public int HighScore
    {
        get => m_userSettings.ClassicScore;

        set
        {
            if (m_userSettings.ClassicScore == value)
                return;

            m_userSettings.ClassicScore = value;
        }
    }
    public bool IsSymbolOn
    {
        get => m_userSettings.IsSymbolOn;

        set
        {
            if (m_userSettings.IsSymbolOn == value)
                return;

            m_userSettings.IsSymbolOn = value;
            _roundManager?.ChangeSymbolState();
        }
    }
```

`Bootstrap()`에서 게이트에 `UserSettings`를 넣고 `IsLoadData` 폴링을 제거한다. 주석 처리된 옛 부트스트랩 코드도 함께 지운다.

```csharp
    async public UniTask Bootstrap()
    {
        LLogger.Log("Bootstrap");
        ResolutionScreen.InitResolution();

        await CheckedManagers(
            ManagerType.Addressable,
            ManagerType.Prefab,
            ManagerType.Sound,
            ManagerType.TextData,
            ManagerType.Firebase,
            ManagerType.Input,
            ManagerType.UserSettings
            );

        m_inputManger.SubscribeToInputHandler(InputType.Game_Exit, OnClickExit);
        CompleteInit(ManagerType.Game);
        _lobbyUI = await m_prefabManager.InstantiateStaticUI<IBaseUI>(PrefabData.LobbyUI);
        _loadingUI = await m_prefabManager.InstantiateStaticUI<IBaseUI>(PrefabData.LoadingUI);
        _lobbyUI.Init();
        _loadingUI.Init();
        await UniTask.WaitForSeconds(2f);
        _loadingUI.Close();
    }
```

- [ ] **Step 6: 컴파일 확인**

Unity 에디터에서 컴파일한다.

기대 결과: 에러 0건. `m_firebaseManager`가 남아 있거나 `IsLoadData`를 참조하는 곳이 있으면 에러가 난다. 그 경우 해당 위치를 위 지침대로 고친다.

- [ ] **Step 7: 플레이 모드 확인 — 설정 저장 회귀 검사**

이 태스크의 가장 큰 위험은 설정 저장 경로다. 반드시 다음을 확인한다.

1. 플레이 시작 → Console에 `UserSettings` 생성 로그와 `유저 설정 준비 완료`가 찍힌다.
2. 로비 → 메뉴 → 사운드 설정에서 BGM을 끈다. 소리가 즉시 멈춘다.
3. 플레이 종료 후 다시 플레이한다. BGM이 여전히 꺼져 있다.
4. `Edit > Project Settings`의 PlayerPrefs 에디터(`CB/Assets/Scripts/Editor/PlayerPrefsEditor.cs`)로 `IsBGMOn` 키가 `0`인지 확인한다.
5. Firebase 콘솔에서 해당 유저 문서의 `IsBGMOn` 필드가 `false`인지 확인한다. (에디터 실행이면 `editor_uid` PlayerPrefs 값이 문서 ID다.)

- [ ] **Step 8: 커밋**

```bash
git add CB/Assets/Scripts/Core/UserSettings.cs \
        CB/Assets/Scripts/Core/UserSettings.cs.meta \
        CB/Assets/Scripts/Core/FirebaseManager.cs \
        CB/Assets/Scripts/Core/SoundManager.cs \
        CB/Assets/Scripts/Core/GameManager.cs \
        CB/Assets/Scripts/GameLifetimeScope.cs
git commit -m "refactor: UserSettings 추출 및 초기화 게이팅 정리

UserData 소유권을 UserSettings로 옮기고 FirebaseManager를 Firestore
I/O 창구로 축소한다. WebGL/의존성 실패 분기의 CompleteInit 누락을
고치고, GameManager의 IsLoadData 폴링을 ManagerType.UserSettings
게이트로 대체한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: InputManager 백키 스택

백키를 스택으로 모델링하고 GameManager를 새 API로 옮긴다. UI 마이그레이션은 Task 4에서 한다.

**Files:**
- Modify: `CB/Assets/Scripts/Core/InputManager.cs`
- Modify: `CB/Assets/Scripts/Core/GameManager.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `InputManager` — `void PushBackHandler(Action)`, `void PopBackHandler(Action)`, `void PushInputBlock()`, `void PopInputBlock()`. 기존 `SubscribeToInputHandler`/`UnsubscribeToInputHandler`는 시그니처 변경 없이 유지.

- [ ] **Step 1: `InputManager`에 스택 API 추가**

`CB/Assets/Scripts/Core/InputManager.cs`의 using과 생성자를 바꾸고 두 region을 추가한다. `SubscribeToInputHandler`/`UnsubscribeToInputHandler` 본문은 손대지 않는다.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

public class InputManager : BaseManager
{
    private PlayerInput _inputHandler;
    private readonly List<Action> _backHandlers = new List<Action>();
    private int _blockDepth;

    public InputManager(ManagerInitTracker tracker) : base(tracker)
    {
        LLogger.Log("InputManager");
        _inputHandler = new PlayerInput();
        _inputHandler.Player.Enable();
        _inputHandler.Player.Exit.performed += OnBackKeyPerformed;
        CompleteInit(ManagerType.Input);
    }

    #region 백키 스택

    /// <summary>
    /// 백키 핸들러를 스택 최상단에 올린다. 이미 있으면 최상단으로 끌어올린다.
    /// </summary>
    public void PushBackHandler(Action handler)
    {
        if (handler == null)
            return;

        _backHandlers.Remove(handler);
        _backHandlers.Add(handler);
    }

    /// <summary>
    /// 백키 핸들러를 제거한다. 최상단이 아니어도 안전하며, 없으면 아무 일도 하지 않는다.
    /// </summary>
    public void PopBackHandler(Action handler)
    {
        if (handler == null)
            return;

        _backHandlers.Remove(handler);
    }

    private void OnBackKeyPerformed(CallbackContext context)
    {
        if (_backHandlers.Count == 0)
            return;

        _backHandlers[_backHandlers.Count - 1].Invoke();
    }

    #endregion

    #region 입력 차단 스택

    public void PushInputBlock()
    {
        _blockDepth++;
        ApplyInputBlock();
    }

    public void PopInputBlock()
    {
        if (_blockDepth > 0)
            _blockDepth--;

        ApplyInputBlock();
    }

    private void ApplyInputBlock()
    {
        if (_blockDepth > 0)
        {
            _inputHandler.Player.Click.Disable();
            _inputHandler.Player.Point.Disable();
        }
        else
        {
            _inputHandler.Player.Click.Enable();
            _inputHandler.Player.Point.Enable();
        }
    }

    #endregion
```

기존 `UseInputHandler` 프로퍼티는 **아직 지우지 않는다.** `MenuUI`가 Task 4에서 옮겨간다.

- [ ] **Step 2: `GameManager`를 백키 스택으로 교체**

`Bootstrap()` 안의 구독 한 줄을 바꾼다.

```csharp
        m_inputManger.PushBackHandler(OnBackKey);
```

`OnClickExit`를 인자 없는 `OnBackKey`로 교체한다. `UnityEngine.InputSystem` using이 다른 곳에서 안 쓰이면 함께 지운다.

```csharp
    private void OnBackKey()
    {
        ShowExitToast().Forget();
    }
```

`ShowExitToast()`의 중복 팝업 가드는 그대로 둔다. 스택 도입 후에는 팝업이 열려 있으면 GameManager 핸들러가 아예 호출되지 않으므로 불필요해지지만, 남아 있어도 무해하다.

GameManager는 부트스트랩에서 가장 먼저 push하므로 스택 바닥에 놓인다. 위에 UI가 하나도 없을 때만 종료 팝업이 뜬다.

- [ ] **Step 3: 컴파일 확인**

기대 결과: 에러 0건.

- [ ] **Step 4: 플레이 모드 확인 — 백키**

Unity 에디터에서 백키는 `PlayerInput` 액션맵의 `Exit` 바인딩(Android 뒤로가기 / 키보드 Esc)으로 발생한다.

1. 로비에서 백키 → 종료 팝업이 뜬다.
2. 팝업이 뜬 상태에서 백키 → **팝업이 닫히고 새 팝업이 다시 뜨지 않는다.** (이 시점엔 아직 PopupQuestionUI가 옛 경로를 쓰므로, 팝업 자신의 구독과 GameManager 스택 핸들러가 함께 동작한다. 새 팝업이 다시 뜨면 Task 4에서 해결된다 — 여기서는 로비 백키가 정상 동작하는지만 확인한다.)

- [ ] **Step 5: 커밋**

```bash
git add CB/Assets/Scripts/Core/InputManager.cs \
        CB/Assets/Scripts/Core/GameManager.cs
git commit -m "feat: InputManager 백키/입력차단 스택 추가

백키를 스택으로 모델링해 최상단 핸들러만 실행되게 하고,
GameManager를 새 API로 옮긴다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: BaseUI 백키 훅 및 UI 정리

`BaseUI`가 백키 배선을 흡수해 개별 UI가 `InputManager`를 참조하지 않게 한다.

**Files:**
- Modify: `CB/Assets/Scripts/UI/BaseUI.cs`
- Modify: `CB/Assets/Scripts/UI/PopupQuestionUI.cs`
- Modify: `CB/Assets/Scripts/UI/MenuUI.cs`
- Modify: `CB/Assets/Scripts/UI/GameOverUI.cs`
- Modify: `CB/Assets/Scripts/UI/GameLobbyUI.cs`
- Modify: `CB/Assets/Scripts/Core/InputManager.cs`

**Interfaces:**
- Consumes: `InputManager.PushBackHandler/PopBackHandler/PushInputBlock/PopInputBlock` (Task 3)
- Produces: `BaseUI` — `protected InputManager m_input`, `protected virtual bool UseBackKey`, `protected virtual void OnBackKey()`, `protected virtual void OnDestroy()`

- [ ] **Step 1: `BaseUI`에 훅 추가**

`CB/Assets/Scripts/UI/BaseUI.cs` 전체를 다음으로 교체한다.

```csharp
using UnityEngine;
using VContainer;

public class BaseUI : MonoBehaviour, IBaseUI
{
    protected InputManager m_input;

    // VContainer는 상속 계층을 타고 올라가며 [Inject] 메서드를 수집하므로
    // 파생 클래스의 Construct와 별개로 이 메서드도 호출된다.
    // 주의: 호출 순서는 파생 → 베이스다. 파생 Construct 안에서 m_input을 쓰면 안 된다.
    [Inject]
    public void ConstructBaseUI(InputManager input)
    {
        m_input = input;
    }

    /// <summary>백키를 처리할 UI는 true로 오버라이드한다.</summary>
    protected virtual bool UseBackKey => false;

    /// <summary>백키가 이 UI에 도달했을 때의 동작.</summary>
    protected virtual void OnBackKey()
    {
        Close();
    }

    public virtual void Init()
    {
        gameObject.SetActive(true);
        if (UseBackKey)
            m_input.PushBackHandler(OnBackKey);
    }

    public virtual void Close()
    {
        if (UseBackKey)
            m_input.PopBackHandler(OnBackKey);
        gameObject.SetActive(false);
    }

    // Close()를 우회해 파괴되는 경우를 대비한 안전망.
    protected virtual void OnDestroy()
    {
        if (UseBackKey)
            m_input?.PopBackHandler(OnBackKey);
    }
}
```

push를 `OnEnable`이 아니라 `Init()`에 두는 이유: Addressables는 오브젝트를 활성 상태로 생성하므로 `Awake`/`OnEnable`이 VContainer 주입보다 먼저 실행되고, 그 시점엔 `m_input`이 null이다. `Init()`은 인스턴스화가 끝난 뒤 소유자가 호출하므로 주입 완료가 보장된다.

- [ ] **Step 2: `PopupQuestionUI`에서 직접 구독 제거**

`CB/Assets/Scripts/UI/PopupQuestionUI.cs`에서 `InputManager` 필드와 주입을 빼고 훅을 오버라이드한다.

```csharp
    protected TextDataManager m_textDataManager;

    [Inject]
    public void Construct(TextDataManager textDataManager)
    {
        m_textDataManager = textDataManager;
    }

    protected override bool UseBackKey => true;

    protected override void OnBackKey()
    {
        OnClickCloseBtn();
    }

    public override void Close()
    {
        base.Close();
        Destroy(this.gameObject);
    }
```

기존 `public override void Init()` 오버라이드는 `base.Init()`만 호출하게 되므로 **통째로 삭제**한다. `using UnityEngine.InputSystem;`도 더 이상 필요 없으면 지운다.

- [ ] **Step 3: `MenuUI`를 입력 차단 스택으로 교체**

`CB/Assets/Scripts/UI/MenuUI.cs`에서 `InputManager` 필드와 주입 파라미터를 뺀다.

```csharp
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

    protected override bool UseBackKey => true;

    protected override void OnBackKey()
    {
        OnClickCloseBtn();
    }
```

기존 `private void OnDestroy()`를 베이스 오버라이드로 바꾼다.

```csharp
    protected override void OnDestroy()
    {
        _symbolToggle.OnValueChanged -= OnSymbolToggleChanged;
        base.OnDestroy();
    }
```

`Init()`과 `OnClickCloseBtn()`의 입력 차단을 스택으로 바꾼다.

```csharp
    public override void Init()
    {
        m_input.PushInputBlock();
        InitLoadUI().Forget();
    }

    public void OnClickCloseBtn()
    {
        base.Close();
        m_input.PopInputBlock();
    }
```

`InitLoadUI()`가 끝에서 `base.Init()`을 호출하므로 백키 push는 거기서 일어난다.

- [ ] **Step 4: `GameOverUI`의 Close 경로 통일**

`CB/Assets/Scripts/UI/GameOverUI.cs`의 `OnClickCloseBtn()`을 바꾼다.

```csharp
    public void OnClickCloseBtn()
    {
        _highScore.StopBurst();
        Close();
    }
```

- [ ] **Step 5: `GameLobbyUI`의 OnDestroy 시그니처 수정**

`CB/Assets/Scripts/UI/GameLobbyUI.cs`의 `private void OnDestroy()`가 베이스를 가리므로 오버라이드로 바꾼다.

```csharp
    protected override void OnDestroy()
    {
        ResolutionScreen.Unsubscribe(ChangeResolution);
        base.OnDestroy();
    }
```

- [ ] **Step 6: `IngameScoreUI`의 OnDestroy 시그니처 수정**

`CB/Assets/Scripts/UI/IngameScoreUI.cs`도 `BaseUI`를 상속하면서 `private void OnDestroy()`를 갖고 있다. 오버라이드로 바꾼다.

```csharp
    protected override void OnDestroy()
    {
        ResolutionScreen.Unsubscribe(ChangeResolution);
        base.OnDestroy();
    }
```

`BaseUI`를 상속하지 않는 `SoundSettingUI`(`MonoBehaviour`)와 `SafeAreaFitter`(`MonoBehaviour`)는 충돌하지 않으므로 건드리지 않는다.

- [ ] **Step 7: `InputManager`에서 죽은 API 제거**

`CB/Assets/Scripts/Core/InputManager.cs`에서 `UseInputHandler` 프로퍼티를 삭제한다. 소비자가 없어졌다.

`SubscribeToInputHandler`/`UnsubscribeToInputHandler`의 `case InputType.Game_Exit:` 분기도 양쪽에서 삭제한다. 백키는 이제 스택 전용이며, 이 경로로 다시 구독하면 스택 의미가 깨진다.

- [ ] **Step 8: 컴파일 확인**

기대 결과: 에러 0건, CS0114 경고 0건. `BaseUI`를 상속하면서 `OnDestroy`를 `private void`로 둔 UI가 더 있으면 CS0114 경고가 뜨므로, 그 경우 해당 파일도 `protected override`로 바꾸고 `base.OnDestroy()`를 호출한다.

- [ ] **Step 9: 플레이 모드 확인 — 백키 스택**

1. 로비에서 백키 → 종료 팝업이 뜬다.
2. 팝업 상태에서 백키 → **팝업만 닫힌다. 새 팝업이 다시 뜨지 않는다.**
3. 게임 시작 → 메뉴 열기 → 백키 → 메뉴만 닫힌다.
4. 메뉴가 열린 동안 보드를 드래그해도 블록이 움직이지 않는다.
5. 메뉴를 닫은 뒤 보드 드래그가 정상 동작한다.
6. 게임오버 UI를 닫았다가 다시 열어도 백키가 정상 동작한다. (스택 누수가 없는지 확인)

- [ ] **Step 10: 커밋**

```bash
git add CB/Assets/Scripts/UI/BaseUI.cs \
        CB/Assets/Scripts/UI/PopupQuestionUI.cs \
        CB/Assets/Scripts/UI/MenuUI.cs \
        CB/Assets/Scripts/UI/GameOverUI.cs \
        CB/Assets/Scripts/UI/GameLobbyUI.cs \
        CB/Assets/Scripts/UI/IngameScoreUI.cs \
        CB/Assets/Scripts/Core/InputManager.cs
git commit -m "refactor: BaseUI가 백키 배선을 흡수

개별 UI가 InputManager를 직접 참조하지 않게 하고, GameManager와
팝업이 백키를 동시 구독해 구독 순서에 의존하던 문제를 해소한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: 앱 생명주기 콜백 복구 (선택)

이 태스크는 설계 문서에 없는 추가 발견 사항이다. 결합도와 무관하므로 건너뛰어도 된다.

`GameManager`는 VContainer 전환 과정에서 `MonoBehaviour`를 벗었지만 `OnApplicationPause`/`OnApplicationQuit`가 그대로 남아 있다. 순수 C# 클래스에서는 Unity가 이 메서드들을 호출하지 않으므로 **죽은 코드**다. 그 결과 `game_pause` 이벤트와 `app_quit` 이벤트가 더 이상 발생하지 않고, 일시정지 시 `PlayerPrefs.Save()`도 실행되지 않는다.

**Files:**
- Modify: `CB/Assets/Scripts/Core/GameManager.cs`

**Interfaces:**
- Consumes: `TelemetryManager.Log/LogEvent/LogModePause` (Task 1)
- Produces: 없음

- [ ] **Step 1: Unity 애플리케이션 이벤트 구독**

`CB/Assets/Scripts/Core/GameManager.cs` 생성자 끝에 구독을 추가한다.

```csharp
        m_userSettings = userSettings;
        Application.focusChanged += OnFocusChanged;
        Application.quitting += OnQuitting;
        Bootstrap().Forget();
```

- [ ] **Step 2: 콜백 메서드 교체**

기존 `OnApplicationPause`/`OnApplicationQuit`를 다음으로 교체한다.

```csharp
    private void OnFocusChanged(bool hasFocus)
    {
        if (hasFocus)
            return;

        PlayerPrefs.Save();

        if (_roundManager != null)
            m_telemetry.LogModePause("Classic", Time.realtimeSinceStartup - catureEnterTime, _roundManager.CurrentScore);

        m_telemetry.Log("App paused");
    }

    private void OnQuitting()
    {
        PlayerPrefs.Save();
        m_telemetry.LogEvent("app_quit", "real_time", Time.realtimeSinceStartup.ToString());
        Application.focusChanged -= OnFocusChanged;
        Application.quitting -= OnQuitting;
    }
```

`Application.focusChanged`는 포커스를 잃을 때 `false`로 호출되며, Android에서 앱이 백그라운드로 갈 때 발생한다. 기존 `OnApplicationPause(true)`와 실질적으로 같은 시점이다.

- [ ] **Step 3: 죽은 코드 제거**

같은 파일 하단의 다음 두 메서드를 삭제한다. `IStartable`/`IDisposable`을 구현하지 않으므로 아무도 호출하지 않는다.

```csharp
    public void Dispose()
    {
        throw new System.NotImplementedException();
    }

    public UniTask StartAsync(CancellationToken cancellation = default)
    {
        throw new System.NotImplementedException();
    }
```

`using System.Threading;`이 다른 곳에서 안 쓰이면 함께 지운다.

- [ ] **Step 4: 컴파일 확인**

기대 결과: 에러 0건.

- [ ] **Step 5: 플레이 모드 확인**

1. 플레이 중 Unity 에디터 창에서 다른 앱으로 포커스를 옮긴다.
2. Console에 `App paused` 관련 로그가 찍히거나, 최소한 예외가 발생하지 않는다.
3. 플레이를 정지한다. 예외 없이 종료된다.

- [ ] **Step 6: 커밋**

```bash
git add CB/Assets/Scripts/Core/GameManager.cs
git commit -m "fix: GameManager 앱 생명주기 콜백 복구

MonoBehaviour를 벗으면서 호출되지 않게 된 OnApplicationPause/
OnApplicationQuit를 Application.focusChanged/quitting 구독으로
대체한다. 미구현 Dispose/StartAsync 죽은 코드도 제거한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## 마무리 확인

모든 태스크 완료 후 다음을 확인한다.

- [ ] `FirebaseManager`를 주입받는 곳이 `UserSettings`, `TelemetryManager`, `GameLobbyUI`, `InquriyUI` 4곳뿐인지 확인한다.

```bash
grep -rn "FirebaseManager" CB/Assets/Scripts --include=*.cs | grep -v "Core/FirebaseManager.cs"
```

- [ ] `InputManager`를 주입받는 곳이 `BaseUI`, `GameManager`, `Board` 3곳뿐인지 확인한다.

```bash
grep -rn "InputManager" CB/Assets/Scripts --include=*.cs | grep -v "Core/InputManager.cs"
```

- [ ] 더 이상 쓰이지 않는 `CB/Assets/Scripts/Share/SingletonInstance.cs`가 남아 있다. 모든 매니저가 이 베이스에서 벗어났으므로 삭제를 검토한다. (다른 참조가 없는지 먼저 확인)

```bash
grep -rn "SingletonInstance" CB/Assets/Scripts --include=*.cs
```
