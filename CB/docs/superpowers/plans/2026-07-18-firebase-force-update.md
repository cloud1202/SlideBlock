# Firebase Remote Config 강제 업데이트 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 앱 시작 시 Firebase Remote Config의 `min_required_version` 값과 현재 앱 버전을 비교해, 구버전이면 업데이트하거나 앱을 종료하는 것 외에는 진행할 수 없는 강제 업데이트 팝업을 띄운다.

**Architecture:** 별도 매니저를 만들지 않고 기존 `Assets/Scripts/Core/FirebaseManager.cs`에 `#region RemoteConfig`를 추가한다. `GameManager.Bootstrap()`에서 `PrefabManager.Instance.InitLoadObjects()` 직후 이 체크를 호출한다. 팝업은 기존 `PopupQuestionUI` 프리팹을 재사용하되, "닫기(X)" 버튼이 No 콜백 없이 그냥 닫혀버리는 기존 버그를 먼저 고쳐서 강제성을 보장한다.

**Tech Stack:** Firebase Unity SDK (`Firebase.RemoteConfig`), Cysharp UniTask (`AsUniTask()` 확장으로 `Task` → `UniTask` 변환), 기존 `PrefabManager`/`TextDataManager`/Addressables UI 시스템.

## Global Constraints

- Remote Config 조회 실패(오프라인 등) 시 항상 통과시킨다 (fail-open) — 정상 유저가 오프라인이라는 이유로 게임을 못 켜면 안 됨.
- Remote Config 파라미터 키는 정확히 `min_required_version` (문자열, semver 형식, 예: `"1.0.0"`).
- Play 스토어 URL: `https://play.google.com/store/apps/details?id=com.LayonStudio.SlideBlock` (패키지명은 `ProjectSettings/ProjectSettings.asset`의 `applicationIdentifier.Android` 값과 반드시 일치).
- 강제 단계는 단일 단계만 존재한다 (권장/선택 업데이트 단계는 범위 밖).
- `LanguageType` enum은 현재 `English, MAX` 두 값뿐이라 (`Assets/Scripts/Share/Enum/LanguageType.cs`), `GameTextSO`의 `text[]` 배열은 인덱스 0(English) 하나만 채우면 된다. 한국어 슬롯은 없다.
- 이 코드베이스에는 매니저/UI 클래스에 대한 자동화 테스트가 없다 (`Assets/Scripts` 어디에도 Tests 폴더 없음). 각 태스크의 "테스트" 단계는 Unity Editor Play 모드에서의 수동 확인으로 대체한다.
- Bootstrap 관련 기존 코드 스타일: 로그는 `FirebaseManager.Instance.Log(...)` 또는 각 매니저의 protected `Logging`/`Warning`/`Error` 헬퍼(`SingletonInstance<T>` 제공)를 사용한다. `Debug.Log`를 직접 쓰지 않는다.

---

### Task 1: Firebase Remote Config SDK 임포트 + 콘솔 파라미터 등록 (수동 사전 준비)

**Files:**
- Modify (수동, Unity Editor 밖에서): Firebase 콘솔 설정
- Modify (수동, Unity Editor 안에서): `Assets/Firebase/` 폴더에 RemoteConfig 모듈 추가

**Interfaces:**
- Produces: `Firebase.RemoteConfig` 네임스페이스와 `FirebaseRemoteConfig.DefaultInstance`가 프로젝트에서 컴파일/사용 가능한 상태. Task 4가 이걸 그대로 사용한다.

이 프로젝트의 `Assets/Firebase/`에는 이미 `FirebaseApp`, `Auth`, `Firestore`, `Analytics`, `Crashlytics`, `Messaging` 모듈이 임포트되어 있지만 `RemoteConfig`는 없다.

- [ ] **Step 1: Firebase Unity SDK에서 Remote Config 모듈 임포트**

  1. https://firebase.google.com/download/unity 에서 이미 이 프로젝트가 쓰고 있는 것과 같은 버전의 Firebase Unity SDK 압축 파일을 받는다 (또는 이미 로컬에 받아둔 SDK가 있으면 그걸 사용).
  2. 압축을 풀면 나오는 `FirebaseRemoteConfig.unitypackage`를 Unity에서 `Assets > Import Package > Custom Package...`로 임포트한다.
  3. Unity가 EDM4U(`Assets/ExternalDependencyManager`)를 통해 Android 의존성 XML을 갱신하도록 둔다 (기존 Firebase 모듈들과 동일한 방식).

- [ ] **Step 2: 컴파일 확인**

  Unity Editor 콘솔에 컴파일 에러가 없는지 확인. 특히 `Assets/Scripts/Core/Core.asmdef`가 `Firebase.RemoteConfig` 어셈블리를 참조할 수 있는지 확인 (다른 Firebase 모듈들과 같은 방식으로 이미 참조되어 있을 가능성이 높음 — 만약 참조 에러가 나면 `Core.asmdef`에 `Firebase.RemoteConfig` 어셈블리 참조를 추가).

- [ ] **Step 3: Firebase 콘솔에 파라미터 등록**

  Firebase 콘솔 → 해당 프로젝트 → Remote Config → 파라미터 추가:
  - 키: `min_required_version`
  - 기본값: 현재 배포된 버전과 동일하게 (예: `1.0.0`)
  - "변경사항 게시" 클릭

- [ ] **Step 4: 커밋**

  이 태스크는 코드 변경이 없거나(에셋/패키지 임포트만) `Assets/Firebase/RemoteConfig` 폴더와 `Assets/ExternalDependencyManager`쪽 XML 변경만 생긴다. 이후 태스크(코드 변경)와 함께 한 번에 커밋해도 무방하므로, 여기서는 별도 커밋 없이 다음 태스크로 진행한다.

---

### Task 2: PopupQuestionUI의 닫기(X) 버튼이 No 콜백을 우회하는 문제 수정

**Files:**
- Modify: `Assets/Scripts/UI/PopupQuestionUI.cs:28-41`

**Interfaces:**
- Consumes: 없음 (기존 클래스 내부 리팩터링)
- Produces: `OnClickCloseBtn()`이 이제 `_onClickNo`를 호출한 뒤 닫는다. `OnClickYesBtn()`은 `_onClickNo`를 호출하지 않고 바로 닫는다. Task 4에서 `RegistQuestionAction`의 `onClickNoAction`으로 `Application.Quit`를 등록하는데, 이 수정이 없으면 X 버튼으로 그 콜백 없이 그냥 닫혀버려서 강제 업데이트를 무력화할 수 있다.

현재 코드 (`Assets/Scripts/UI/PopupQuestionUI.cs:28-41`):

```csharp
public void OnClickCloseBtn()
{
    base.Close();
}
public void OnClickYesBtn()
{
    _onClickYes?.Invoke();
    OnClickCloseBtn();
}
public void OnClickNoBtn()
{
    _onClickNo?.Invoke();
    OnClickCloseBtn();
}
```

프리팹(`Assets/AddressableAssets/Prefabs/UI/PopupQuestion.prefab`)에는 Yes/No 버튼 외에 별도의 "닫기(X)" 버튼이 있고, 그 버튼은 `OnClickCloseBtn()`에 직접 바인딩되어 있다 (Yes/No를 거치지 않음). 현재는 X를 누르면 `_onClickNo`가 전혀 호출되지 않고 그냥 닫히기만 한다 — 게임 종료 확인창(`GameManager.ShowExitToast`)에서는 문제가 안 됐지만(그 팝업은 No 콜백 자체가 없으므로), 강제 업데이트 팝업에서는 X로 빠져나가면 No에 등록한 `Application.Quit`가 실행되지 않아 그냥 게임을 계속할 수 있게 되는 구멍이 생긴다.

- [ ] **Step 1: 코드 수정**

  `Assets/Scripts/UI/PopupQuestionUI.cs:28-41`을 아래로 교체:

  ```csharp
  public void OnClickCloseBtn()
  {
      _onClickNo?.Invoke();
      base.Close();
  }
  public void OnClickYesBtn()
  {
      _onClickYes?.Invoke();
      base.Close();
  }
  public void OnClickNoBtn()
  {
      OnClickCloseBtn();
  }
  ```

  이제 X 버튼과 No 버튼 모두 `_onClickNo`를 호출한 뒤 닫히고, Yes 버튼만 `_onClickNo` 없이 바로 닫힌다.

- [ ] **Step 2: 기존 사용처(게임 종료 확인창) 회귀 확인 — Play 모드 수동 테스트**

  Unity Editor에서 Play 모드 진입 → 로비 화면에서 뒤로가기/종료 입력(`InputType.Game_Exit`)을 트리거 → 종료 확인 팝업이 뜨는지 확인 → X 버튼을 눌러서 팝업이 정상적으로 닫히고 게임이 종료되지 않는지 확인 (이 팝업은 `RegistQuestionAction(QuitGame)`으로 No 콜백이 없으므로, X를 눌러도 `_onClickNo`가 `null`이라 아무 효과 없이 닫히기만 해야 함 — 기존과 동일한 동작).

- [ ] **Step 3: 커밋**

  ```bash
  git add Assets/Scripts/UI/PopupQuestionUI.cs
  git commit -m "fix: PopupQuestionUI 닫기(X) 버튼이 No 콜백을 우회하던 문제 수정"
  ```

---

### Task 3: GameTextData에 강제 업데이트 문구 추가

**Files:**
- Modify: `Assets/Scripts/Share/Enum/GameTextData.cs:1-20`
- Modify (Unity Editor 안에서 수동): `Assets/AddressableAssets/ScriptableObject/GameTextSO.asset`

**Interfaces:**
- Produces: `GameTextData.POPUP_UPDATE_REQUIRED` enum 값과, `TextDataManager.Instance.GetGameText(GameTextData.POPUP_UPDATE_REQUIRED)`가 빈 문자열이 아닌 안내 문구를 반환. Task 4가 이 enum 값을 그대로 사용한다.

- [ ] **Step 1: enum 값 추가**

  `Assets/Scripts/Share/Enum/GameTextData.cs` 맨 끝에 추가 (기존 값들 순서는 건드리지 않음 — `GameTextSO.GameText.Index`는 `id` 필드로부터 계산되므로 순서 변경에 영향받지 않지만, 그래도 안전하게 끝에 추가):

  ```csharp
  public enum GameTextData
  {
      POPUP_YES,
      POPUP_NO,
      INQURIY_SEND,
      POPUP_BACK,
      INQURIY_EMAIL_PH,
      INQURIY_CONTENT_PH,
      POPUP_EXIT_GAME,
      LOBBY_LEGAL,
      LOBBY_CLASSIC,
      LEGAL_PRIVACY_TITLE,
      LEGAL_TERMS_TITLE,
      INQURIY_SEND_FAIL_1,
      INQURIY_SEND_FAIL_2,
      INQURIY_SEND_FAIL_3,
      INQURIY_SEND_FAIL_4,
      INQURIY_SEND_SUCCESS,
      LOBBY_LEADERBOARD,
      POPUP_UPDATE_REQUIRED,
  }
  ```

- [ ] **Step 2: GameTextSO 에셋에 문구 등록 (Unity Editor 수동 작업)**

  1. Unity Editor에서 `Assets/AddressableAssets/ScriptableObject/GameTextSO.asset`을 선택.
  2. Inspector에서 `Text Data` 리스트에 새 엔트리 추가 (+ 버튼).
  3. `Id`를 `POPUP_UPDATE_REQUIRED`로 선택.
  4. `Text` 배열 크기를 1로 하고, 인덱스 0에 다음 문구 입력: `"A new update is available. Please update to the latest version to continue playing."`
     (`LanguageType`이 현재 `English, MAX` 뿐이라 인덱스 0=English 하나만 채우면 됨.)
  5. 저장 (Ctrl+S).

- [ ] **Step 3: Play 모드에서 값 로드 확인**

  Play 모드 진입 후 Console에 `TextDataManager` 관련 에러("Not Found Game Text : POPUP_UPDATE_REQUIRED")가 안 뜨는지 확인. (이 시점에는 아직 이 텍스트를 실제로 화면에 띄우는 코드가 없으므로, 직접 호출해서 확인하려면 임시로 `Awake()` 등에 `LLogger.Log(TextDataManager.Instance.GetGameText(GameTextData.POPUP_UPDATE_REQUIRED))`를 넣었다가 확인 후 지워도 되고, 간단히는 Task 4~5까지 마친 뒤 한 번에 확인해도 된다.)

- [ ] **Step 4: 커밋**

  ```bash
  git add Assets/Scripts/Share/Enum/GameTextData.cs Assets/AddressableAssets/ScriptableObject/GameTextSO.asset
  git commit -m "feat: 강제 업데이트 안내 문구(POPUP_UPDATE_REQUIRED) 추가"
  ```

---

### Task 4: FirebaseManager에 RemoteConfig 강제 업데이트 체크 추가

**Files:**
- Modify: `Assets/Scripts/Core/FirebaseManager.cs`
  - `using` 목록 (파일 상단)
  - 새 `#region RemoteConfig` 블록을 `#region PlayGames` 블록 끝(현재 459번째 줄, `#endregion`) 뒤 `#region Public API`(현재 461번째 줄) 앞에 삽입

**Interfaces:**
- Consumes:
  - `PrefabManager.Instance.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI)` (기존, `Assets/Scripts/Core/PrefabManager.cs:41`)
  - `IPopupQuestion.SetNoticeContent(GameTextData)`, `IPopupQuestion.RegistQuestionAction(Action, Action)` (기존, `Assets/Scripts/Share/Interface/IPopupQuestion.cs`)
  - `GameTextData.POPUP_UPDATE_REQUIRED` (Task 3에서 추가)
  - `PopupQuestionUI`의 수정된 X 버튼 동작 (Task 2)
- Produces: `public UniTask CheckForForceUpdateAsync()` — Task 5에서 `GameManager.Bootstrap()`이 호출.

**중요 — 왜 `await UniTask.Yield();`가 필요한가:**
`PrefabManager.InstantiateDynamicUI`는 이미 생성된 인스턴스가 있으면 새로 만들지 않고 캐시된 같은 GameObject를 재사용한다 (`PrefabManager.cs:65-69`, `if (obj.isInstance) return obj.instance.GetComponent<TI>();`). Yes 버튼을 누르면 `OnClickYesBtn()`이 우리 콜백(스토어 열기 + 팝업 재호출)을 먼저 실행한 뒤 `base.Close()`로 같은 프레임에 팝업을 비활성화한다. 만약 재호출한 `ShowForceUpdatePopupAsync()`가 아무 대기 없이 바로 `InstantiateDynamicUI`를 호출하면, `base.Close()`가 실행되기 *전에* 같은 인스턴스를 다시 활성화(`Init()` → `SetActive(true)`)해버리고, 그 직후 `base.Close()`가 실행되면서 방금 켠 팝업을 도로 꺼버린다 — 결과적으로 팝업이 사라져버려 강제성이 깨진다. `await UniTask.Yield()`로 한 프레임 양보하면 `base.Close()`가 먼저 끝난 뒤에 재활성화가 일어나 정상적으로 다시 뜬다.

- [ ] **Step 1: using 추가**

  `Assets/Scripts/Core/FirebaseManager.cs` 상단 `using` 목록에 추가:

  ```csharp
  using Firebase.RemoteConfig;
  ```

- [ ] **Step 2: RemoteConfig region 추가**

  `#region PlayGames` 블록의 `#endregion` (현재 459번째 줄) 바로 뒤, `#region Public API` (현재 461번째 줄) 바로 앞에 삽입:

  ```csharp
  #region RemoteConfig

  private const string MIN_VERSION_KEY = "min_required_version";
  private const string PLAY_STORE_URL = "https://play.google.com/store/apps/details?id=com.LayonStudio.SlideBlock";

  public async UniTask CheckForForceUpdateAsync()
  {
      try
      {
          var remoteConfig = FirebaseRemoteConfig.DefaultInstance;
          await remoteConfig.SetDefaultsAsync(new System.Collections.Generic.Dictionary<string, object>
          {
              { MIN_VERSION_KEY, "0.0.0" }
          }).AsUniTask();

          await remoteConfig.FetchAndActivateAsync().AsUniTask();

          var minVersion = new Version(remoteConfig.GetValue(MIN_VERSION_KEY).StringValue);
          var currentVersion = new Version(Application.version);

          if (currentVersion < minVersion)
          {
              Logging($"강제 업데이트 필요: 현재 {currentVersion} < 최소 {minVersion}");
              await ShowForceUpdatePopupAsync();
          }
      }
      catch (Exception e)
      {
          LogError(e);
          Warning($"강제 업데이트 체크 실패, 통과 처리: {e}");
      }
  }

  private async UniTask ShowForceUpdatePopupAsync()
  {
      // OnClickYesBtn -> base.Close()가 같은 프레임에 실행되므로, 재호출 시 팝업을
      // 재활성화하기 전에 Close()가 먼저 끝나도록 한 프레임 대기한다.
      await UniTask.Yield();

      var popup = await PrefabManager.Instance.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI);
      popup.SetNoticeContent(GameTextData.POPUP_UPDATE_REQUIRED);
      popup.RegistQuestionAction(
          onClickYesAction: () =>
          {
              Application.OpenURL(PLAY_STORE_URL);
              ShowForceUpdatePopupAsync().Forget();
          },
          onClickNoAction: Application.Quit
      );
  }

  #endregion
  ```

  (`System`, `Cysharp.Threading.Tasks`는 파일 상단에 이미 `using`되어 있음 — `System.Collections.Generic`은 안 되어 있어서 위 코드에서 전체 이름을 그대로 씀.)

- [ ] **Step 3: 컴파일 확인**

  Unity Editor 콘솔에 에러 없는지 확인.

- [ ] **Step 4: Play 모드 수동 테스트 — 정상 버전 (통과 케이스)**

  Firebase 콘솔에서 `min_required_version`을 현재 `Application.version`(`1.0.0`, `ProjectSettings/ProjectSettings.asset:145`)과 같거나 낮게 둔 상태로 Play 모드 진입 → 강제 업데이트 팝업이 뜨지 않고 정상적으로 로비까지 진행되는지 확인.

- [ ] **Step 5: Play 모드 수동 테스트 — 강제 업데이트 케이스**

  Firebase 콘솔에서 `min_required_version`을 `9.9.9`처럼 현재 버전보다 높게 설정 후 게시 → Play 모드 재진입 → 강제 업데이트 팝업이 뜨는지 확인 → "업데이트" 버튼 클릭 시 브라우저(에디터에서는 시스템 기본 브라우저)로 Play 스토어 페이지가 열리고, 팝업이 다시 나타나는지 확인 → X 버튼이나 다른 버튼을 눌러도 게임이 진행되지 않고 팝업이 계속 뜨거나 앱이 종료되는지 확인 → 테스트 후 Firebase 콘솔에서 `min_required_version`을 원래 값(`1.0.0`)으로 되돌리기.

- [ ] **Step 6: 커밋**

  ```bash
  git add Assets/Scripts/Core/FirebaseManager.cs
  git commit -m "feat: FirebaseManager에 Remote Config 기반 강제 업데이트 체크 추가"
  ```

---

### Task 5: GameManager.Bootstrap()에 연결

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs:40-62`

**Interfaces:**
- Consumes: `FirebaseManager.Instance.CheckForForceUpdateAsync()` (Task 4에서 추가)

현재 코드 (`Assets/Scripts/Core/GameManager.cs:40-62`):

```csharp
async public UniTask Bootstrap()
{
    ResolutionScreen.InitResolution();
    await UniTask.WaitUntil(() => FirebaseManager.Instance.IsInitialized);
    FirebaseManager.Instance.Log("AddressableManager Init");
    await AddressableManager.Instance.SetAddressable();
    FirebaseManager.Instance.Log("PrefabManager Init");
    await PrefabManager.Instance.LoadAssetReference();
    FirebaseManager.Instance.Log("SoundManager Init");
    await SoundManager.Instance.LoadAssetReference();
    FirebaseManager.Instance.Log("TextDataManager Init");
    await TextDataManager.Instance.LoadAssetReference();
    FirebaseManager.Instance.Log("PrefabManager Load");
    await PrefabManager.Instance.InitLoadObjects();
    _lobbyUI = await PrefabManager.Instance.InstantiateStaticUI<IBaseUI>(PrefabData.LobbyUI);
    _loadingUI = await PrefabManager.Instance.InstantiateStaticUI<IBaseUI>(PrefabData.LoadingUI);
    _lobbyUI.Init();
    _loadingUI.Init();
    InputManager.Instance.SubscribeToInputHandler(InputType.Game_Exit, OnClickExit);
    await UniTask.WaitUntil(() => FirebaseManager.Instance.IsLoadData);
    await UniTask.WaitForSeconds(2f);
    _loadingUI.Close();
}
```

- [ ] **Step 1: `CheckForForceUpdateAsync()` 호출 삽입**

  `await PrefabManager.Instance.InitLoadObjects();` 바로 뒤, `_lobbyUI = await ...` 바로 앞에 추가:

  ```csharp
  async public UniTask Bootstrap()
  {
      ResolutionScreen.InitResolution();
      await UniTask.WaitUntil(() => FirebaseManager.Instance.IsInitialized);
      FirebaseManager.Instance.Log("AddressableManager Init");
      await AddressableManager.Instance.SetAddressable();
      FirebaseManager.Instance.Log("PrefabManager Init");
      await PrefabManager.Instance.LoadAssetReference();
      FirebaseManager.Instance.Log("SoundManager Init");
      await SoundManager.Instance.LoadAssetReference();
      FirebaseManager.Instance.Log("TextDataManager Init");
      await TextDataManager.Instance.LoadAssetReference();
      FirebaseManager.Instance.Log("PrefabManager Load");
      await PrefabManager.Instance.InitLoadObjects();
      FirebaseManager.Instance.Log("Force Update Check");
      await FirebaseManager.Instance.CheckForForceUpdateAsync();
      _lobbyUI = await PrefabManager.Instance.InstantiateStaticUI<IBaseUI>(PrefabData.LobbyUI);
      _loadingUI = await PrefabManager.Instance.InstantiateStaticUI<IBaseUI>(PrefabData.LoadingUI);
      _lobbyUI.Init();
      _loadingUI.Init();
      InputManager.Instance.SubscribeToInputHandler(InputType.Game_Exit, OnClickExit);
      await UniTask.WaitUntil(() => FirebaseManager.Instance.IsLoadData);
      await UniTask.WaitForSeconds(2f);
      _loadingUI.Close();
  }
  ```

- [ ] **Step 2: 컴파일 확인**

  Unity Editor 콘솔에 에러 없는지 확인.

- [ ] **Step 3: Play 모드 전체 흐름 재확인**

  **주의 (최종 리뷰에서 바로잡음):** `ShowForceUpdatePopupAsync()`는 팝업을 띄우고 콜백을 등록한 뒤 바로 반환되므로(Yes를 눌렀을 때의 재표시는 `.Forget()`으로 fire-and-forget됨), `CheckForForceUpdateAsync()`도 곧 반환되고 `Bootstrap()`은 로비/로딩 UI 생성을 포함해 계속 진행된다. 즉 강제 업데이트 상황에서도 `LobbyUI` 인스턴스화 로그는 정상적으로 뜬다 — 이건 버그가 아니다. 강제성은 "Bootstrap이 멈춘다"가 아니라 "`PopupQuestionUI`가 `DynamicCanvas` 위에서 전체 화면을 덮고 입력을 막는다"는 방식으로 보장된다(기존에 이미 배포된 종료 확인 팝업과 동일한 메커니즘).

  Task 4의 Step 4, Step 5를 다시 한번 처음부터(씬 재시작) 실행해서, 강제 업데이트 상황에서 팝업이 화면 전체를 덮어 로비를 가리고 입력을 막는지(로비 요소를 탭해도 반응하지 않는지) 확인하고, 정상 버전에서는 기존과 동일하게 로비까지 도달해 정상적으로 조작 가능한지 최종 확인.

- [ ] **Step 4: 커밋**

  ```bash
  git add Assets/Scripts/Core/GameManager.cs
  git commit -m "feat: Bootstrap에 강제 업데이트 체크 연결"
  ```

---

## 완료 후 남는 수동 작업 (이 플랜 범위 밖)

- 실제 치명적 버그로 강제 업데이트를 발동해야 할 때: 새 버전을 Play 스토어에 배포한 뒤, Firebase 콘솔에서 `min_required_version`을 그 버전 번호로 올리고 게시. 앱 재배포 불필요.
- 이번 작업으로 스토어 링크가 하드코딩되므로, 만약 향후 `applicationIdentifier`(패키지명)가 바뀌면 `FirebaseManager.cs`의 `PLAY_STORE_URL` 상수도 같이 바꿔야 함.
