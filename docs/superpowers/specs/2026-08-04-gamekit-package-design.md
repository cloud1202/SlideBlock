# GameKit 스타트팩 패키지 설계

- 작성일: 2026-08-04
- 상태: 승인됨
- 출처: Color-Brick의 매니저 인프라를 재사용 가능한 형태로 추출

## 배경

Color-Brick에서 VContainer 기반 매니저 계층, Addressable 래퍼, 입력·UI 베이스를 정리하면서
새 모바일 게임을 시작할 때 매번 다시 짜게 되는 코드가 상당량 쌓였다. 이를 UPM 패키지로 뽑아
개인용 스타트팩으로 만든다.

## 목표

새 Unity 프로젝트에서 git URL 하나를 추가하고 enum 몇 개와 얇은 구체 클래스를 정의하면,
매니저 계층이 DI로 뜨고 게이팅·입력·Addressable 로드가 동작하는 상태까지 도달한다.

## 비목표

- **Color-Brick을 이 패키지로 이관하지 않는다.** 코드를 복사해 독립 패키지를 만들고 둘은 이후
  따로 간다. 동작 중인 게임을 깨뜨릴 위험을 지지 않는다.
- **화면을 제공하지 않는다.** UI 프리팹과 씬 구성은 게임마다 다르므로 코드 베이스만 제공한다.
- **자동 테스트를 작성하지 않는다.** 현재 프로젝트에 테스트 어셈블리가 없고, Unity 에디터
  없이는 컴파일 검증조차 불가능한 환경이다. 검증은 최소 소비자 프로젝트로 수동 확인한다.

## 결정적 제약 — 패키지는 `Assets/`를 참조할 수 없다

Unity는 `Packages/`를 `Assets/`보다 먼저 컴파일한다. 따라서 UPM 패키지 어셈블리는 `Assets/`에
설치된 어셈블리·DLL을 참조할 수 없다. Color-Brick의 의존성이 두 갈래로 갈린다.

| UPM (참조 가능) | `Assets/` 설치 (참조 불가) |
|---|---|
| UniTask, VContainer | Firebase |
| Addressables, InputSystem | GoogleMobileAds |
| ugui / TextMeshPro | GooglePlayGames |
| | DOTween (`Assets/Plugins/Demigiant`) |

이 제약이 패키지 범위를 결정한다. `FirebaseManager`, `TelemetryManager` 구현체, `UserSettings`,
`AdmobManager`는 패키지에 들어갈 수 없다.

### DOTween은 의존하지 않고 걷어낸다

DOTween은 무료판이므로 OpenUPM(`com.demigiant.dotween`)으로 전환하면 패키지가 참조할 수도
있다. 그러나 **의존하지 않는 쪽을 택한다.**

패키지 후보 코드에서 DOTween 사용처는 세 곳뿐이고 전부 단순 보간이다 — `SoundManager`의 볼륨
페이드, `Utility`의 토스트 애니메이션, `SlideToggle`의 핸들 이동. 특히 `SlideToggle`은 **이미
DOTween 없이 같은 곡선을 구현해 두었다.** 에디터 프리뷰용으로 OutCubic을 손으로 계산한다
(`1f - Mathf.Pow(1f - t, 3f)`). 런타임은 DOTween, 에디터는 수동으로 같은 커브가 두 번 구현된
상태다.

`Utility`에 UniTask 기반 보간 헬퍼를 두고 세 곳을 그것으로 통일한다. 얻는 것:

- 패키지의 서드파티 런타임 의존이 **0**이 된다. 새 프로젝트에서 OpenUPM scoped registry를
  등록할 필요가 없다 — 스타트팩 목적에 정확히 부합한다
- `SoundManager`를 패키지에 넣을 수 있게 된다. 제외했던 유일한 이유가 DOTween이었다
- `SlideToggle`의 중복 구현이 사라진다

## 패키지 구성

이름: `com.layoncraft.gamekit`

```
com.layoncraft.gamekit/
├── package.json
├── README.md
├── Runtime/
│   ├── Utility/     → LayonCraft.GameKit.Utility.asmdef
│   ├── Core/        → LayonCraft.GameKit.Core.asmdef
│   ├── Resource/    → LayonCraft.GameKit.Resource.asmdef
│   ├── Input/       → LayonCraft.GameKit.Input.asmdef
│   └── UI/          → LayonCraft.GameKit.UI.asmdef
└── Editor/          → LayonCraft.GameKit.Editor.asmdef
```

의존 방향은 단방향이다.

```
Utility ← Core ← Resource
           ↑        ↑
         Input ←── UI
```

어셈블리를 5개로 나눈 이유는 프로젝트가 필요한 것만 참조하게 하기 위해서다. InputSystem을
쓰지 않는 프로젝트가 Input 어셈블리를 끌어오지 않아도 된다.

Color-Brick의 `Share` 어셈블리는 유지하지 않는다. 그 안에서 패키지로 갈 것은 `BaseManager`와
`ManagerInitTracker` 정도이고 나머지(`BrickType`, `PrefabData`, `UserData` 등)는 전부 프로젝트
소유라, 별도 어셈블리를 둘 이유가 없다. 해당 항목은 `Core`에 흡수한다.

### 어셈블리별 내용

| 어셈블리 | 내용 | 외부 의존 |
|---|---|---|
| Utility | `LLogger`, `Colors`, `EnumConverter`, `Timer`, `ResolutionScreen`, `VibrateData`, `Tweening`(보간 헬퍼), 토스트 애니메이션 | UniTask |
| Core | `BaseManager`, `ManagerInitTracker`, `ITelemetry`, `ConsoleTelemetry`, `PlayerPrefsStore` | VContainer, UniTask |
| Resource | `AddressableManager`, `AssetReferenceBase`, `ReferenceManager`, `PrefabManagerBase`, `SoundManagerBase`, `TextDataManagerBase`, `GameTextTableBase`, `InstantiateObject`, `IAssetResource` | Addressables |
| Input | `InputManager` | InputSystem |
| UI | `BaseUI`, `CloseBaseUI`, `SafeAreaFitter`, `SlideToggle` | ugui / TextMeshPro |

`SlideToggle`은 `ToolKit/Component/`에 있던 재사용 UI 컴포넌트다. 같은 `ToolKit/SDK/BrickColorEditor/`는
Color-Brick 전용이라 제외한다.

## 런타임 설계

### 에셋 계층 — 제네릭 베이스 + 프로젝트 상속

프로젝트마다 달라지는 것은 에셋 키 enum(`PrefabData`)과 프리로드 그룹 enum(`ContainLabel`)이다.
`AssetReferenceBase<E, T>`가 이미 enum에 대해 제네릭이므로 같은 패턴을 매니저 계층으로 넓힌다.

```csharp
// 패키지
public abstract class AssetReferenceBase<TKey, TLabel, TAsset> : ScriptableObject
    where TKey : Enum where TLabel : Enum where TAsset : UnityEngine.Object

public abstract class ReferenceManager<TKey, TLabel> : BaseManager
    where TKey : Enum where TLabel : Enum

public abstract class PrefabManagerBase<TKey, TLabel> : ReferenceManager<TKey, TLabel>
public abstract class TextDataManagerBase<TKey> : BaseManager where TKey : Enum
public abstract class GameTextTableBase<TKey> : ScriptableObject where TKey : Enum
```

```csharp
// 프로젝트
public enum PrefabData { LobbyUI, LoadingUI, Board }
public enum ContainLabel { Common = 1 << 0, Round = 1 << 1 }

public class PrefabAssetReference : AssetReferenceBase<PrefabData, ContainLabel, GameObject> { }
public class PrefabManager : PrefabManagerBase<PrefabData, ContainLabel> { }
```

호출부 문법(`InstantiateStaticUI(PrefabData.LobbyUI)`)이 그대로 유지된다.

검토 중 확인된 사항: 현재 `ReferenceManager<T>`의 타입 파라미터 `T`는 **어디에서도 사용되지
않는다.** 싱글톤 패턴(`SingletonInstance<T>`)의 잔재로 보인다. 이를 실제 의미가 있는 파라미터로
교체하는 셈이다.

### 매니저 준비 게이트 — `Type` 키

`ManagerType` enum은 프로젝트마다 달라지는데 패키지 코드가 이를 참조해야 해서 이음새가
지저분해진다. `Type`을 키로 쓰면 enum 자체가 사라진다.

```csharp
public class ManagerInitTracker
{
    private readonly HashSet<Type> _ready = new HashSet<Type>();

    public void MarkReady(Type type) => _ready.Add(type);
    public bool IsReady(Type type) => _ready.Contains(type);
    public UniTask WaitUntilReady(params Type[] types)
        => UniTask.WaitUntil(() => types.All(IsReady));
}

public abstract class BaseManager
{
    protected void CompleteInit() => m_tracker.MarkReady(GetType());
    protected UniTask CheckedManagers(params Type[] types) => m_tracker.WaitUntilReady(types);
}
```

호출부:

```csharp
await CheckedManagers(typeof(PrefabManager), typeof(SoundManager), typeof(UserSettings));
CompleteInit();
```

이 설계는 Color-Brick에서 실제로 발생했던 버그를 구조적으로 불가능하게 만든다. `FirebaseManager`가
`CompleteInit(ManagerType.Admob)`으로 잘못 마킹해 `Bootstrap`이 무한 대기했던 건이 있었는데,
`GetType()`으로 자동 결정되면 틀릴 값이 존재하지 않는다. 유지할 enum이 하나 줄고, 매니저를
추가하고 enum에 넣지 않아 조용히 어긋나는 경우도 사라진다.

대가는 `typeof(PrefabManager)`가 `ManagerType.Prefab`보다 길고, 게이트 목록이 enum 정의처럼
한곳에 모이지 않는다는 점이다.

### 텔레메트리 경계

```csharp
public interface ITelemetry
{
    void Log(string message);
    void LogError(Exception e);
    void SetCustomKey(string key, string value);
    void LogEvent(string name);
    void LogEvent(string name, string paramName, string paramValue);
}

public sealed class ConsoleTelemetry : ITelemetry { /* LLogger로 출력 */ }
```

패키지 매니저들이 자유롭게 로그를 남길 수 있고, Firebase 없이도 콘솔로 동작한다. 프로젝트는
`FirebaseTelemetry : ITelemetry`를 만들어 DI에서 교체한다.

`LogModeStart`/`LogGameOver` 같은 게임별 이벤트 어휘는 패키지에 넣지 않는다. 프로젝트가
`ITelemetry.LogEvent` 위에 얹는다.

### 광고는 추상화하지 않는다

`AdmobManager`는 통째로 프로젝트에 둔다. 호출부가 배너 하나, 전면광고 하나뿐이라 인터페이스를
두는 것은 과하다.

### 사운드 — 설정 의존을 뒤집는다

`SoundManagerBase`를 패키지에 넣으려면 하나 더 정리해야 한다. 현재 `SoundManager`는
`m_userSettings.IsBGMOn`을 직접 읽는데 `UserSettings`는 프로젝트 소유다.

패키지 베이스는 음소거 상태를 **자기 필드로** 갖고, 영속화는 프로젝트 하위 클래스가 맡는다.

```csharp
// 패키지
public abstract class SoundManagerBase<TKey, TLabel> : ReferenceManager<TKey, TLabel>
{
    public bool BgmMuted { get; set; }        // 세터가 AudioSource.mute까지 반영
    public bool SfxMuted { get; set; }

    public UniTask PlayBgm(TKey key);
    public UniTask PlaySfx(TKey key, CancellationToken ct = default);
    protected UniTask FadeAsync(AudioSource src, float target, float duration);  // Tweening 헬퍼 사용
}

// 프로젝트
public class SoundManager : SoundManagerBase<SoundData, ContainLabel>
{
    // UserSettings와 양방향 동기화
}
```

이 방향이면 패키지가 오디오 소스 생성·볼륨·페이드·재생이라는 기계적인 부분을 전부 갖고,
프로젝트는 "이 값을 어디에 저장하는가"만 결정한다.

### 텍스트 테이블의 언어 선택

`GameTextTableBase<TKey>`의 각 항목은 `string[] text`를 갖고 언어 인덱스로 접근한다. 언어 enum
(`LanguageType`)은 프로젝트 소유이므로 패키지가 참조할 수 없다. 따라서 매니저는 정수 인덱스만
안다.

```csharp
public abstract class TextDataManagerBase<TKey> : BaseManager where TKey : Enum
{
    /// <summary>현재 언어의 인덱스. 프로젝트가 자신의 LanguageType enum을 변환해 넣는다.</summary>
    public int LanguageIndex { get; set; }

    public string GetText(TKey key);   // 범위를 벗어나면 빈 문자열 + 경고
}
```

프로젝트는 `m_textData.LanguageIndex = EnumConverter.Enum32ToInt(language);`로 설정한다.

### 유저 설정 — `PlayerPrefsStore`만 제공

패키지는 enum 키 기반의 타입 안전한 PlayerPrefs 읽기/쓰기 유틸만 제공한다.

```csharp
public static class PlayerPrefsStore
{
    public static void SetKeys(string[] keys);          // 생성된 SaveFieldData.Fields를 주입
    public static int  GetInt<TField>(TField f, int def = 0)   where TField : Enum;
    public static void SetInt<TField>(TField f, int value)     where TField : Enum;
    public static bool GetBool<TField>(TField f, bool def)     where TField : Enum;
    public static void SetBool<TField>(TField f, bool value)   where TField : Enum;
    public static string GetString<TField>(TField f, string def = "") where TField : Enum;
    public static void SetString<TField>(TField f, string value)     where TField : Enum;
}
```

`SaveFieldDataGenerator`가 프로젝트 enum에서 문자열 배열을 생성하고, 프로젝트가 부팅 시
`SetKeys`로 한 번 주입한다. 이렇게 하면 패키지가 프로젝트 enum을 참조하지 않으면서도
문자열 리터럴 없이 키에 접근할 수 있다.

`UserData`, `UserSettings`, 클라우드 병합 로직은 프로젝트가 작성한다.

이유: 설정 필드가 게임마다 다르고(`ClassicScore`, `IsSymbolOn`은 Color-Brick 전용), 병합 로직은
Firestore 문서 구조에 강하게 묶여 있는데 Firestore는 패키지에 들어올 수 없다. 추상 베이스로
뽑아도 실제 재사용되는 것은 "PlayerPrefs에 먼저 쓰고 클라우드는 나중에" 수준의 얕은 뼈대뿐이다.

대신 **Color-Brick에서 확립한 설계를 패키지 README에 레시피로 기록한다.** 다음 게임에서 문서를
보고 게임에 맞는 형태로 다시 작성하는 편이 낫다. 기록할 내용:

- 클라우드 문서를 실제로 읽은 경우에만 서는 플래그로 저장을 가드해, 로컬 폴백 데이터가 클라우드를
  덮어쓰지 못하게 한다
- 인증 완료를 `UniTaskCompletionSource`로 신호화한다. 콜백 체인은 완료를 관측할 수단이 없어
  폴링과 임의의 타임아웃을 부르는데, 모든 종료 지점에서 신호를 완료시키면 사라진다
- 최종 플레이 시각을 로컬에도 기록하고, 로드 시 원격과 비교해 최신본을 채택한다. 오프라인
  세션의 변경이 다음 실행에서 되살아난다

## Editor 도구

Color-Brick의 Editor 스크립트 8개 중 `ChangeMaterial`을 제외한 7개를 옮긴다.

| 도구 | 필요한 작업 |
|---|---|
| `AssetReferenceBaseEditor` | 이동만. 이미 `typeof(AssetReferenceBase<,>), true`로 열린 제네릭에 붙어 있다 |
| `PlayerPrefsEditor` | `SaveFieldData` 참조를 프로젝트 주입으로 |
| `GameTextEditor` | `GameTextTableBase<TKey>`에 맞춰 제네릭화 |
| `SaveFieldDataGenerator` | 대상 enum 타입과 출력 경로를 설정에서 읽도록 |
| `UIPrefabPostprocessor` | 감시 경로를 설정에서 읽도록 |
| `BuildProcessor` | 버전·빌드 경로·gtag 측정 ID를 설정에서 읽도록 |
| `AutoKeystoreFile` | 키스토어 JSON 경로를 `EditorPrefs`에서 읽도록 |

### 설정 저장 위치

`GameKitEditorSettings` ScriptableObject 하나를 프로젝트가 `Assets/Editor/`에 만들고 값을 채운다.
없으면 각 도구는 기본값으로 동작하거나 조용히 비활성화된다.

**단 키스토어 경로는 `EditorPrefs`에 둔다.** 설정 SO는 git에 커밋되므로 서명 키 위치가 저장소에
남는다. `EditorPrefs`는 기기 로컬이라 추적되지 않는다.

`BuildProcessor`의 WebGL gtag 스니펫 삽입은 범용화한다. 설정에 측정 ID가 있으면 삽입하고,
비어 있으면 건너뛴다.

### 뼈대 생성 명령

`Tools/GameKit/새 프로젝트 뼈대 생성` 메뉴를 제공한다. enum 스텁, 얇은 구체 클래스,
`GameLifetimeScope` 초안을 한 번에 생성한다. Day 1 작업의 2~4단계가 정형화된 보일러플레이트라,
이것이 없으면 "스타트팩"이라는 이름값을 하지 못한다.

## 새 프로젝트 Day 1

1. `manifest.json`에 git URL 추가 (UniTask·VContainer·Addressables·InputSystem은 패키지 의존성으로 함께 해결)
2. enum 정의: `PrefabData`, `ContainLabel`, `SoundData`, `GameTextData`, `SaveFieldType`, `LanguageType`
3. 얇은 구체 클래스: `PrefabAssetReference`, `SoundAssetReference`, `PrefabManager`, `SoundManager`, `GameTextTable`, `TextDataManager`
4. `GameLifetimeScope` 작성 — 매니저 등록
5. SO 에셋 생성 + Addressable 등록

2~4단계는 뼈대 생성 명령이 대신한다. 생성되는 것은 **컴파일되는 스텁**이며, 게임에 맞는 값은
직접 채운다.

`SoundManager`는 상속 후 음소거 상태를 자기 저장소와 동기화하는 부분만 채우면 된다. 오디오
소스 생성·볼륨·페이드·재생은 `SoundManagerBase`가 갖는다.

## 범위 밖

- Firebase 일체 — `FirebaseManager`, `ITelemetry` 구현체, `UserData`, `UserSettings`, 병합 로직
- `AdmobManager`
- `GameManager` — 게임 흐름
- UI 프리팹, 씬
- Color-Brick 게임플레이 — `Board`, `Brick`, `RoundManager`, `RoundObject`, `IRound`, `IScore`

## 검증

패키지 자체에 자동 테스트를 두지 않는다. 완료 기준은 **최소 소비자 프로젝트를 하나 만들어
매니저 계층이 뜨는 것**을 수동 확인하는 것이다.

1. 새 Unity 프로젝트에 패키지를 git URL로 추가하고 컴파일 에러가 없다
2. 뼈대 생성 명령으로 enum과 구체 클래스가 생성된다
3. 플레이 시 매니저 생성자 로그가 순서대로 찍히고 `CheckedManagers` 대기가 풀린다
4. Addressable로 프리팹 하나를 로드해 화면에 띄운다
5. 백키 스택이 동작한다 — 핸들러를 둘 밀면 최상단만 실행된다

## 위험 요소

- **컴파일 검증을 Unity 에디터에서만 할 수 있다.** `dotnet build`는 이 프로젝트에서
  환경적 이유로 실패한다(Firebase 어셈블리가 .NET 4.7.2 타깃, 프로젝트는 4.7.1). 새 패키지
  프로젝트에는 Firebase가 없으므로 이 제약이 풀릴 가능성이 있고, 계획 단계에서 확인한다.
- **코드 이동이 아니라 제네릭화 리팩터링이다.** 거의 모든 파일의 시그니처가 바뀐다. 단계를
  잘게 나눠 중간에 멈춰도 쓸 수 있는 상태를 유지한다.
- **`ContainLabel`을 타입 파라미터로 뺀 것이 과할 수 있다.** 프리로드 그룹을 늘 `Common`
  하나만 쓴다면 제네릭이 한 겹 낭비다. 최소 소비자 프로젝트에서 실제 사용감을 보고, 부담스러우면
  패키지 고정 enum으로 되돌린다.
