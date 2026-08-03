# GameKit 스타트팩 패키지 Implementation Plan

> 이 계획은 **직접 실행**하는 것을 전제로 작성했다. 각 태스크는 Unity 에디터에서 컴파일이
> 통과하는 상태로 끝나며, 중간에 멈춰도 그때까지의 결과물은 쓸 수 있다.

**Goal:** Color-Brick의 매니저 인프라를 서드파티 런타임 의존이 없는 UPM 패키지로 추출해, 새 게임을 git URL 하나로 시작할 수 있게 한다.

**Architecture:** 5개 런타임 어셈블리(Utility ← Core ← Resource, Input, UI)와 1개 Editor 어셈블리. 프로젝트마다 달라지는 에셋 키는 제네릭 타입 파라미터로 받고, 매니저 준비 게이트는 `Type`을 키로 쓴다. Firebase·Admob은 `ITelemetry` 인터페이스 뒤에 두거나 아예 프로젝트에 남긴다.

**Tech Stack:** Unity 6 (URP), UPM, VContainer 1.19, UniTask, Addressables 2.7, Input System 1.14

**설계 문서:** `docs/superpowers/specs/2026-08-04-gamekit-package-design.md`

## Global Constraints

- **패키지 어셈블리는 `Assets/`의 어떤 것도 참조할 수 없다.** Unity가 `Packages/`를 먼저 컴파일하기 때문이다. Firebase·GoogleMobileAds·GooglePlayGames·DOTween이 전부 여기 해당한다.
- **서드파티 런타임 의존을 추가하지 않는다.** 허용되는 것은 `package.json`에 선언 가능한 UPM 패키지뿐이다: UniTask, VContainer, Addressables, Input System, ugui.
- **자동 테스트를 작성하지 않는다.** 검증은 Unity 에디터 컴파일 + 플레이 모드 수동 확인이다.
- **Color-Brick은 건드리지 않는다.** 복사해서 독립 패키지를 만든다.
- 매니저 준비 게이트는 `Type` 키를 쓴다. `CompleteInit()`은 인자를 받지 않고 `GetType()`으로 자동 결정한다.
- 네임스페이스는 `LayonCraft.GameKit.<어셈블리>`를 쓴다. 원본 코드에는 네임스페이스가 없으므로 이번에 추가한다.
- 주석은 한국어로 쓴다. 커밋 메시지도 한국어로 쓰고 다음 줄로 끝낸다:
  `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`

## 작업 위치

- **패키지 저장소:** `c:\Users\oort_\Desktop\cloud\Develop\GameKit` (새로 만든다. 위치는 바꿔도 무방하며, 이 계획에서는 `<GameKit>`으로 표기한다)
- **원본:** `c:\Users\oort_\Desktop\cloud\Develop\Color-Brick\CB\Assets\Scripts` (읽기 전용으로만 쓴다. 이 계획에서는 `<CB>`로 표기한다)

## 파일 구조

| 경로 | 책임 |
|---|---|
| `Runtime/Utility/` | 로깅·색상·enum 변환·타이머·해상도·진동·보간·수치 포맷 |
| `Runtime/Core/` | 매니저 베이스, 준비 게이트, 텔레메트리 계약, PlayerPrefs 접근 |
| `Runtime/Resource/` | Addressable 로드/해제, 에셋 참조 SO, 매니저 베이스 4종 |
| `Runtime/Input/` | 액션 에셋, 생성 클래스, 백키 스택·입력 차단 |
| `Runtime/UI/` | UI 베이스 2종, 세이프에어리어, 슬라이드 토글 |
| `Editor/` | 도구 7종, 설정 SO, 뼈대 생성 명령 |

---

## Task 1: 패키지 뼈대

**Files:**
- Create: `<GameKit>/package.json`
- Create: `<GameKit>/README.md`
- Create: `<GameKit>/.gitignore`
- Create: `<GameKit>/Runtime/{Utility,Core,Resource,Input,UI}/` 각각에 `.asmdef`
- Create: `<GameKit>/Editor/LayonCraft.GameKit.Editor.asmdef`

**Produces:** 어셈블리 6개의 이름 — 이후 모든 태스크가 이 이름으로 참조를 건다.

- [ ] **Step 1: 저장소와 폴더 생성**

```bash
mkdir -p "/c/Users/oort_/Desktop/cloud/Develop/GameKit"
cd "/c/Users/oort_/Desktop/cloud/Develop/GameKit"
git init
mkdir -p Runtime/Utility Runtime/Core Runtime/Resource Runtime/Input Runtime/UI Editor
```

- [ ] **Step 2: `package.json` 작성**

`<GameKit>/package.json`:

```json
{
  "name": "com.layoncraft.gamekit",
  "version": "0.1.0",
  "displayName": "LayonCraft GameKit",
  "description": "모바일 게임 시작용 매니저 인프라. VContainer 기반 매니저 계층, Addressable 래퍼, 입력 스택, UI 베이스를 제공한다.",
  "unity": "6000.0",
  "dependencies": {
    "com.unity.addressables": "2.7.6",
    "com.unity.inputsystem": "1.14.0",
    "com.unity.ugui": "2.0.0"
  }
}
```

UniTask와 VContainer는 git URL 의존이라 `dependencies`에 넣을 수 없다(UPM은 레지스트리 패키지만 여기서 해결한다). 소비 프로젝트가 자신의 `manifest.json`에 직접 넣어야 하며, 이를 README에 명시한다.

- [ ] **Step 3: `.gitignore` 작성**

`<GameKit>/.gitignore`:

```
*.csproj
*.sln
*.user
.vs/
.idea/
obj/
```

- [ ] **Step 4: asmdef 6개 작성**

`<GameKit>/Runtime/Utility/LayonCraft.GameKit.Utility.asmdef`:

```json
{
    "name": "LayonCraft.GameKit.Utility",
    "rootNamespace": "LayonCraft.GameKit",
    "references": ["UniTask", "Unity.TextMeshPro"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`<GameKit>/Runtime/Core/LayonCraft.GameKit.Core.asmdef`:

```json
{
    "name": "LayonCraft.GameKit.Core",
    "rootNamespace": "LayonCraft.GameKit",
    "references": ["LayonCraft.GameKit.Utility", "UniTask", "VContainer"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`<GameKit>/Runtime/Resource/LayonCraft.GameKit.Resource.asmdef`:

```json
{
    "name": "LayonCraft.GameKit.Resource",
    "rootNamespace": "LayonCraft.GameKit",
    "references": [
        "LayonCraft.GameKit.Utility",
        "LayonCraft.GameKit.Core",
        "UniTask",
        "UniTask.Addressables",
        "VContainer",
        "Unity.Addressables",
        "Unity.ResourceManager"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`<GameKit>/Runtime/Input/LayonCraft.GameKit.Input.asmdef`:

```json
{
    "name": "LayonCraft.GameKit.Input",
    "rootNamespace": "LayonCraft.GameKit",
    "references": [
        "LayonCraft.GameKit.Utility",
        "LayonCraft.GameKit.Core",
        "UniTask",
        "VContainer",
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`<GameKit>/Runtime/UI/LayonCraft.GameKit.UI.asmdef`:

```json
{
    "name": "LayonCraft.GameKit.UI",
    "rootNamespace": "LayonCraft.GameKit",
    "references": [
        "LayonCraft.GameKit.Utility",
        "LayonCraft.GameKit.Core",
        "LayonCraft.GameKit.Input",
        "UniTask",
        "VContainer",
        "Unity.TextMeshPro"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`<GameKit>/Editor/LayonCraft.GameKit.Editor.asmdef`:

```json
{
    "name": "LayonCraft.GameKit.Editor",
    "rootNamespace": "LayonCraft.GameKit.Editor",
    "references": [
        "LayonCraft.GameKit.Utility",
        "LayonCraft.GameKit.Core",
        "LayonCraft.GameKit.Resource",
        "LayonCraft.GameKit.UI",
        "UniTask",
        "Unity.Addressables",
        "Unity.Addressables.Editor",
        "Unity.TextMeshPro"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 5: 검증용 소비 프로젝트에 연결**

새 Unity 프로젝트를 하나 만들거나 기존 테스트 프로젝트를 쓴다. 그 프로젝트의 `Packages/manifest.json`에 추가한다(경로는 로컬 파일 참조로, 개발 중에는 이게 편하다):

```json
"com.layoncraft.gamekit": "file:../../GameKit",
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
"jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.19.0"
```

**검증:** Unity를 열어 Package Manager에 "LayonCraft GameKit"이 보이고, Console에 에러가 없다. 어셈블리는 아직 비어 있으므로 컴파일할 것도 없다.

- [ ] **Step 6: 커밋**

```bash
cd "/c/Users/oort_/Desktop/cloud/Develop/GameKit"
git add -A
git commit -m "chore: 패키지 뼈대와 어셈블리 정의 추가

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: Utility 어셈블리

**Files:**
- Create: `<GameKit>/Runtime/Utility/` 아래 8개 파일
- 원본: `<CB>/Utility/*.cs`

**Produces:**
- `LLogger.Log(string, LogLevel, Color, int)` — 이후 모든 어셈블리가 로깅에 쓴다
- `EnumConverter.Enum32ToInt<T>(T)` — 제네릭 매니저들이 enum을 int 인덱스로 바꿀 때 쓴다
- `Tweening.LerpAsync(float from, float to, float duration, Action<float> apply, Func<float,float> ease = null, CancellationToken ct = default)` — Resource·UI가 페이드에 쓴다
- `Tweening.EaseOutCubic(float t)` — SlideToggle이 쓴다

- [ ] **Step 1: 그대로 복사할 파일 5개**

`<CB>/Utility/`에서 `<GameKit>/Runtime/Utility/`로 복사한다. `.meta` 파일은 복사하지 않는다(Unity가 새로 만든다).

```
LLogger.cs          Colors.cs          EnumConverter.cs
Timer.cs            VibrateData.cs
```

각 파일 맨 위에 네임스페이스를 씌운다. 예:

```csharp
namespace LayonCraft.GameKit
{
    public static class EnumConverter
    {
        // 기존 내용 그대로
    }
}
```

- [ ] **Step 2: `ResolutionScreen.cs` 복사**

같은 방식으로 복사하고 네임스페이스를 씌운다. 이 파일은 `REF_WIDTH`/`REF_HEIGHT` 상수를 갖는데, 프로젝트마다 다를 수 있으므로 다음과 같이 바꾼다:

```csharp
// 기존: private const float REF_WIDTH = 1080f;
// 변경:
public static float ReferenceWidth { get; set; } = 1080f;
public static float ReferenceHeight { get; set; } = 1920f;
```

파일 내 `REF_WIDTH` → `ReferenceWidth`, `REF_HEIGHT` → `ReferenceHeight`로 전부 치환한다.

- [ ] **Step 3: `Tweening.cs` 신설**

`<GameKit>/Runtime/Utility/Tweening.cs`:

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// UniTask 기반 보간 헬퍼. DOTween 의존을 없애기 위해 둔다.
    /// 패키지는 서드파티 런타임 의존을 갖지 않는다는 제약을 지키기 위한 것이므로,
    /// 복잡한 시퀀스가 필요하면 프로젝트에서 DOTween을 쓰면 된다.
    /// </summary>
    public static class Tweening
    {
        /// <summary>OutCubic 이징. SlideToggle의 에디터 프리뷰가 쓰던 곡선과 동일하다.</summary>
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        /// <summary>
        /// from에서 to까지 duration초 동안 보간하며 매 프레임 apply를 호출한다.
        /// duration이 0 이하면 즉시 to를 적용하고 끝낸다.
        /// </summary>
        public static async UniTask LerpAsync(
            float from,
            float to,
            float duration,
            Action<float> apply,
            Func<float, float> ease = null,
            CancellationToken ct = default)
        {
            if (apply == null)
                return;

            if (duration <= 0f)
            {
                apply(to);
                return;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();

                float t = Mathf.Clamp01(elapsed / duration);
                apply(Mathf.LerpUnclamped(from, to, ease?.Invoke(t) ?? t));

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                elapsed += Time.unscaledDeltaTime;
            }

            apply(to);
        }
    }
}
```

- [ ] **Step 4: `Utility.cs` 분할 복사**

`<CB>/Utility/Utility.cs`를 `<GameKit>/Runtime/Utility/GameKitUtility.cs`로 복사한다(클래스명도 `GameKitUtility`로 바꾼다 — `Utility`는 흔한 이름이라 소비 프로젝트와 충돌할 수 있다).

**제외할 멤버:** `CalcScore(int score, int combo)` — Color-Brick의 점수 공식이다.

**바꿀 멤버:** `AsyncToastGraphicObject`가 DOTween 시퀀스를 쓴다. 다음으로 교체한다:

```csharp
/// <summary>
/// 그래픽을 나타냈다가 위로 띄우며 사라지게 한다. 원본은 DOTween 시퀀스였으나
/// 패키지의 서드파티 의존을 없애기 위해 Tweening 헬퍼로 재구현했다.
/// </summary>
public static async UniTask AsyncToastGraphicObject(Graphic graphic, CancellationToken ct = default)
{
    if (graphic == null)
        return;

    var rt = graphic.rectTransform;
    float baseY = rt.anchoredPosition.y;

    SetAlpha(graphic, 1f);

    await Tweening.LerpAsync(baseY, baseY + 80f, 0.5f,
        y => rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y), ct: ct);

    await UniTask.Delay(TimeSpan.FromSeconds(0.2f), ignoreTimeScale: true, cancellationToken: ct);

    await Tweening.LerpAsync(1f, 0f, 0.3f, a => SetAlpha(graphic, a), ct: ct);

    await UniTask.Delay(TimeSpan.FromSeconds(0.3f), ignoreTimeScale: true, cancellationToken: ct);

    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, baseY);
}

private static void SetAlpha(Graphic graphic, float alpha)
{
    var c = graphic.color;
    c.a = alpha;
    graphic.color = c;
}
```

`using DG.Tweening;`를 지우고 `using System.Threading;`을 추가한다.

**나머지 멤버는 그대로 둔다:** `NumberRegularExpression`, `RandomInt`, `RandomFloat`, `UpdateLayoutSize`, `AsyncDurationVibrateObject`, `AsyncVibrateObject`, `GetDigits`, `SetResizeScale`

- [ ] **Step 5: 컴파일 확인**

Unity로 전환해 자동 컴파일을 기다린다.

**기대 결과:** Console 에러 0건. `DG.Tweening` 참조가 남아 있으면 "The type or namespace name 'DG' could not be found"가 뜨므로 해당 파일에서 지운다.

- [ ] **Step 6: 커밋**

```bash
git add Runtime/Utility
git commit -m "feat: Utility 어셈블리 추가

DOTween 의존을 UniTask 기반 Tweening 헬퍼로 대체하고,
게임 전용이던 CalcScore는 제외했다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: Core 어셈블리

**Files:**
- Create: `<GameKit>/Runtime/Core/BaseManager.cs`
- Create: `<GameKit>/Runtime/Core/ManagerInitTracker.cs`
- Create: `<GameKit>/Runtime/Core/ITelemetry.cs`
- Create: `<GameKit>/Runtime/Core/ConsoleTelemetry.cs`
- Create: `<GameKit>/Runtime/Core/PlayerPrefsStore.cs`

**Consumes:** `LLogger`, `EnumConverter` (Task 2)

**Produces:**
- `BaseManager(ManagerInitTracker)` — 모든 매니저의 베이스. `CompleteInit()`, `CheckedManagers(params Type[])`
- `ManagerInitTracker` — `MarkReady(Type)`, `IsReady(Type)`, `WaitUntilReady(params Type[])`
- `ITelemetry` — `Log`, `LogError`, `SetCustomKey`, `LogEvent` 2종
- `PlayerPrefsStore` — `SetKeys(string[])` 후 enum 키로 접근

- [ ] **Step 1: `ManagerInitTracker.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// 매니저 준비 상태 추적기. 키로 Type을 쓴다.
    /// enum을 쓰면 프로젝트마다 그 enum이 달라져 패키지가 참조할 수 없고,
    /// "매니저를 추가했는데 enum에 안 넣어서 조용히 어긋나는" 실수도 생긴다.
    /// </summary>
    public class ManagerInitTracker
    {
        private readonly HashSet<Type> _ready = new HashSet<Type>();

        public void MarkReady(Type type) => _ready.Add(type);

        public bool IsReady(Type type) => _ready.Contains(type);

        /// <summary>넘긴 타입이 <b>전부</b> 준비될 때까지 기다린다.</summary>
        public UniTask WaitUntilReady(params Type[] types)
            => UniTask.WaitUntil(() => types.All(IsReady));
    }
}
```

- [ ] **Step 2: `BaseManager.cs`**

```csharp
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// 모든 매니저의 베이스.
    /// <para>
    /// IInitializable을 구현하는 이유: VContainer는 RegisterEntryPoint로 등록된 타입 중
    /// 엔트리포인트 마커 인터페이스를 구현한 것만 즉시 생성(resolve)한다. 이게 없으면
    /// 등록만 되고 생성자가 영영 호출되지 않는다. 실제 초기화는 생성자에서 끝나므로
    /// Initialize는 빈 구현으로 둔다.
    /// </para>
    /// </summary>
    public abstract class BaseManager : IInitializable
    {
        private readonly ManagerInitTracker m_tracker;

        protected BaseManager(ManagerInitTracker tracker)
        {
            m_tracker = tracker;
        }

        public virtual void Initialize() { }

        /// <summary>
        /// 이 매니저를 준비 완료로 표시한다. 키는 GetType()으로 자동 결정되므로
        /// 잘못된 값을 넘길 여지가 없다.
        /// </summary>
        protected void CompleteInit() => m_tracker.MarkReady(GetType());

        /// <summary>넘긴 매니저들이 전부 준비될 때까지 기다린다.</summary>
        protected UniTask CheckedManagers(params Type[] types) => m_tracker.WaitUntilReady(types);

        protected void Logging(string log) => LLogger.Log(log, color: Colors.Yellow, skipFrames: 2);
        protected void Warning(string log) => LLogger.Log(log, level: LLogger.LogLevel.Warning, skipFrames: 2);
        protected void Error(string log) => LLogger.Log(log, level: LLogger.LogLevel.Error, skipFrames: 2);
    }
}
```

원본과 달리 `abstract`로 선언한다. `BaseManager` 자체를 직접 인스턴스화할 일이 없다.

- [ ] **Step 3: `ITelemetry.cs`**

```csharp
using System;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// 로그·이벤트 수집 계약. 패키지는 Firebase를 참조할 수 없으므로 인터페이스만 두고,
    /// 프로젝트가 FirebaseTelemetry 같은 구현체를 만들어 DI에서 교체한다.
    /// <para>
    /// LogModeStart/LogGameOver 같은 게임별 이벤트 어휘는 여기 두지 않는다.
    /// 프로젝트가 LogEvent 위에 얹는다.
    /// </para>
    /// </summary>
    public interface ITelemetry
    {
        void Log(string message);
        void LogError(Exception e);
        void SetCustomKey(string key, string value);
        void LogEvent(string eventName);
        void LogEvent(string eventName, string paramName, string paramValue);
    }
}
```

- [ ] **Step 4: `ConsoleTelemetry.cs`**

```csharp
using System;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// 기본 구현. 콘솔로만 출력한다. 프로젝트가 실제 백엔드를 붙이기 전까지
    /// 패키지 코드가 자유롭게 로그를 남길 수 있게 해준다.
    /// </summary>
    public sealed class ConsoleTelemetry : ITelemetry
    {
        public void Log(string message) => LLogger.Log($"[telemetry] {message}");

        public void LogError(Exception e) => LLogger.Log($"[telemetry] {e}", level: LLogger.LogLevel.Error);

        public void SetCustomKey(string key, string value) => LLogger.Log($"[telemetry] {key}={value}");

        public void LogEvent(string eventName) => LLogger.Log($"[telemetry] event: {eventName}");

        public void LogEvent(string eventName, string paramName, string paramValue)
            => LLogger.Log($"[telemetry] event: {eventName} ({paramName}={paramValue})");
    }
}
```

- [ ] **Step 5: `PlayerPrefsStore.cs`**

```csharp
using System;
using UnityEngine;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// enum 키 기반 PlayerPrefs 접근. 패키지가 프로젝트의 SaveFieldType enum을 참조할 수
    /// 없으므로, 문자열 키 배열을 프로젝트가 주입하고 여기서는 enum을 인덱스로만 쓴다.
    /// 키 배열은 Editor의 SaveFieldDataGenerator가 enum에서 생성한다.
    /// </summary>
    public static class PlayerPrefsStore
    {
        private static string[] _keys = Array.Empty<string>();

        /// <summary>부팅 시 한 번 호출한다. 생성된 SaveFieldData.Fields를 넘기면 된다.</summary>
        public static void SetKeys(string[] keys) => _keys = keys ?? Array.Empty<string>();

        private static bool TryKey<TField>(TField field, out string key) where TField : Enum
        {
            int index = EnumConverter.Enum32ToInt(field);

            if (index < 0 || index >= _keys.Length)
            {
                LLogger.Log(
                    $"PlayerPrefsStore: '{field}'에 해당하는 키가 없다. SetKeys를 호출했는지 확인할 것.",
                    level: LLogger.LogLevel.Error);
                key = null;
                return false;
            }

            key = _keys[index];
            return true;
        }

        public static int GetInt<TField>(TField field, int defaultValue = 0) where TField : Enum
            => TryKey(field, out var k) ? PlayerPrefs.GetInt(k, defaultValue) : defaultValue;

        public static void SetInt<TField>(TField field, int value) where TField : Enum
        {
            if (TryKey(field, out var k)) PlayerPrefs.SetInt(k, value);
        }

        public static bool GetBool<TField>(TField field, bool defaultValue) where TField : Enum
            => GetInt(field, defaultValue ? 1 : 0) > 0;

        public static void SetBool<TField>(TField field, bool value) where TField : Enum
            => SetInt(field, value ? 1 : 0);

        public static string GetString<TField>(TField field, string defaultValue = "") where TField : Enum
            => TryKey(field, out var k) ? PlayerPrefs.GetString(k, defaultValue) : defaultValue;

        public static void SetString<TField>(TField field, string value) where TField : Enum
        {
            if (TryKey(field, out var k)) PlayerPrefs.SetString(k, value);
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
```

- [ ] **Step 6: 컴파일 확인**

**기대 결과:** 에러 0건. `VContainer.Unity` 참조가 안 잡히면 Task 1의 Core asmdef에 `"VContainer"`가 들어 있는지, 소비 프로젝트 `manifest.json`에 VContainer가 있는지 확인한다.

- [ ] **Step 7: 커밋**

```bash
git add Runtime/Core
git commit -m "feat: Core 어셈블리 추가

매니저 준비 게이트를 enum 대신 Type 키로 바꿔, 잘못된 값을 넘기는
실수가 구조적으로 불가능하게 했다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: Resource 어셈블리 — Addressable 기반

**Files:**
- Create: `<GameKit>/Runtime/Resource/IAssetResource.cs`
- Create: `<GameKit>/Runtime/Resource/AssetReferenceBase.cs`
- Create: `<GameKit>/Runtime/Resource/InstantiateObject.cs`
- Create: `<GameKit>/Runtime/Resource/AddressableManager.cs`
- 원본: `<CB>/Core/Core_Resource/`

**Consumes:** `BaseManager`, `ManagerInitTracker` (Task 3), `LLogger` (Task 2)

**Produces:**
- `IAssetResource` — `Index`, `isValid`, `runtimeKeyIsValid`, `isInstance`, `LoadAssetHandle()`, `InstantiateAsync<T>(Transform)`, `ReleaseAsset()`, `LabelIndex`
- `AssetReferenceBase<TKey, TLabel, TAsset>` — `List<AssetResource> assetDatas`
- `AddressableManager(ManagerInitTracker, IObjectResolver)` — `Load<T>`, `Instantiate<T>`, `PreloadAssets`, `AssetReleaseForLabel`, `InstantiateRelease`, `LoadResourceData<T>`

- [ ] **Step 1: `IAssetResource.cs` — 라벨을 int로 바꾼다**

원본은 `ContainLabel ContainLabel { get; }`이었다. 패키지는 프로젝트 enum을 모르므로 int로 받는다.

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// 에셋 참조 한 건. 제네릭 타입 파라미터가 인터페이스에 새지 않도록
    /// 키와 라벨을 int 인덱스로 노출한다.
    /// </summary>
    public interface IAssetResource
    {
        int Index { get; }
        int LabelIndex { get; }

        bool isValid { get; }
        bool runtimeKeyIsValid { get; }
        bool isInstance { get; }
        GameObject instance { get; }

        AsyncOperationHandle LoadAssetHandle();
        UniTask<T> InstantiateAsync<T>(Transform parent);
        void ReleaseAsset();
    }
}
```

- [ ] **Step 2: `AssetReferenceBase.cs` — 제네릭 3개로**

```csharp
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// 에셋 참조 테이블의 베이스. 프로젝트가 키 enum·라벨 enum·에셋 타입을 지정해 상속한다.
    /// 예: public class PrefabAssetReference : AssetReferenceBase&lt;PrefabData, ContainLabel, GameObject&gt; { }
    /// </summary>
    public abstract class AssetReferenceBase<TKey, TLabel, TAsset> : ScriptableObject
        where TKey : Enum
        where TLabel : Enum
        where TAsset : UnityEngine.Object
    {
        [Serializable]
        public class AssetResource : IAssetResource
        {
            public TKey id;
            public AssetReferenceT<TAsset> data;
            public TLabel containLabel;

            public int Index => EnumConverter.Enum32ToInt(id);
            public int LabelIndex => EnumConverter.Enum32ToInt(containLabel);

            public GameObject instance { get; private set; }
            public bool isInstance => instance != null;

            public bool isValid
                => data.OperationHandle.IsValid()
                && data.OperationHandle.Status == AsyncOperationStatus.Succeeded;

            public bool runtimeKeyIsValid => data.RuntimeKeyIsValid();

            public async UniTask<T1> InstantiateAsync<T1>(Transform parent)
            {
                var handle = data.InstantiateAsync(parent);
                await handle.ToUniTask();
                instance = handle.Result;

                if (typeof(T1) == typeof(GameObject))
                    return (T1)(object)instance;

                return instance.GetComponent<T1>();
            }

            public AsyncOperationHandle LoadAssetHandle()
                => isValid ? data.OperationHandle : data.LoadAssetAsync();

            public void ReleaseAsset()
            {
                if (isInstance)
                    data.ReleaseInstance(instance);

                instance = null;
            }
        }

        public List<AssetResource> assetDatas;

        /// <summary>인터페이스 목록으로 노출한다. 매니저는 이걸로만 접근한다.</summary>
        public IEnumerable<IAssetResource> Resources => assetDatas;
    }
}
```

- [ ] **Step 3: `InstantiateObject.cs`**

`<CB>/Core/Core_Resource/Addressable/InstantiateObject.cs`를 복사하고 네임스페이스를 씌운다. 내용 변경 없음(16줄).

- [ ] **Step 4: `AddressableManager.cs`**

`<CB>/Core/Core_Resource/AddressableManager.cs`를 복사하고 다음을 바꾼다.

1. 네임스페이스를 씌운다
2. `Dictionary<ContainLabel, List<AsyncOperationHandle>>` → `Dictionary<int, List<AsyncOperationHandle>>`
3. `AssetReleaseForLabel(ContainLabel label)` → `AssetReleaseForLabel(int labelIndex)`
4. `PreloadAssets(ContainLabel label, IAssetResource[] assets)` → `PreloadAssets(int labelIndex, IAssetResource[] assets)`
5. `_loadHandles[assetRef.ContainLabel]` → `_loadHandles[assetRef.LabelIndex]`
6. `CompleteInit(ManagerType.Addressable)` → `CompleteInit()`
7. `Load<T>` 안의 `_loadHandles[assetRef.LabelIndex].Add(newHandle)` 앞에 키 존재 확인을 넣는다 — 원본은 `PreloadAssets`가 먼저 불리지 않으면 `KeyNotFoundException`이 난다:

```csharp
if (!_loadHandles.TryGetValue(assetRef.LabelIndex, out var handles))
{
    handles = new List<AsyncOperationHandle>();
    _loadHandles[assetRef.LabelIndex] = handles;
}
handles.Add(newHandle);
```

생성자는 다음 형태가 된다:

```csharp
private readonly IObjectResolver m_resolver;

public AddressableManager(ManagerInitTracker tracker, IObjectResolver resolver) : base(tracker)
{
    m_resolver = resolver;
    SetAddressable().Forget();
}
```

`Instantiate<T>` 안의 `m_resolver.InjectGameObject(go);`는 반드시 유지한다. Addressables는 오브젝트를 활성 상태로 만들기 때문에 이 호출이 없으면 `[Inject]`가 전혀 걸리지 않는다.

- [ ] **Step 5: 컴파일 확인**

**기대 결과:** 에러 0건. `ContainLabel` 잔여 참조가 있으면 에러로 잡힌다.

- [ ] **Step 6: 커밋**

```bash
git add Runtime/Resource
git commit -m "feat: Addressable 기반 계층 추가

프로젝트 enum이 인터페이스에 새지 않도록 키와 라벨을 int 인덱스로
노출하고, PreloadAssets 없이 Load를 부르면 터지던 문제도 고쳤다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: Resource 어셈블리 — 매니저 베이스 4종

**Files:**
- Create: `<GameKit>/Runtime/Resource/ReferenceManager.cs`
- Create: `<GameKit>/Runtime/Resource/PrefabManagerBase.cs`
- Create: `<GameKit>/Runtime/Resource/SoundManagerBase.cs`
- Create: `<GameKit>/Runtime/Resource/TextDataManagerBase.cs`
- Create: `<GameKit>/Runtime/Resource/GameTextTableBase.cs`

**Consumes:** `AddressableManager`, `IAssetResource`, `AssetReferenceBase` (Task 4), `Tweening` (Task 2)

**Produces:**
- `ReferenceManager<TKey, TLabel>` — `LoadAsset<T>(TKey)`, `InstantiateObject<T>(TKey, Transform, bool)`, `PreloadAssets(TLabel)`
- `PrefabManagerBase<TKey, TLabel>` — `MainCanvas`, `MainCamera`, `InstantiateStaticUI<T>`, `InstantiateDynamicUI<T>`, `TryGetInstance<T>`
- `SoundManagerBase<TKey, TLabel>` — `BgmMuted`, `SfxMuted`, `PlayBgm(TKey)`, `PlaySfx(TKey, CancellationToken)`
- `TextDataManagerBase<TKey>` — `LanguageIndex`, `GetText(TKey)`
- `GameTextTableBase<TKey>` — `List<GameText> textData`

- [ ] **Step 1: `ReferenceManager.cs`**

원본 `<CB>/Core/ReferenceManager.cs`를 기반으로 하되, **쓰이지 않던 타입 파라미터 `T`를 실제 의미가 있는 것으로 교체한다.**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// 에셋 테이블을 들고 로드·인스턴스화를 중개하는 매니저 베이스.
    /// 원본의 타입 파라미터는 어디에도 쓰이지 않는 유령이었고(싱글톤 패턴의 잔재),
    /// 여기서는 키 enum과 라벨 enum으로 교체했다.
    /// </summary>
    public abstract class ReferenceManager<TKey, TLabel> : BaseManager
        where TKey : Enum
        where TLabel : Enum
    {
        protected readonly Dictionary<int, IAssetResource> _assetMap = new Dictionary<int, IAssetResource>();
        protected IEnumerable<IAssetResource> _assetDatas = new List<IAssetResource>();
        protected readonly AddressableManager m_addressableManager;

        protected ReferenceManager(ManagerInitTracker tracker, AddressableManager addressableManager)
            : base(tracker)
        {
            m_addressableManager = addressableManager;
            Init().Forget();
        }

        /// <summary>파생 클래스가 에셋 테이블을 읽고 CompleteInit()을 부르는 자리.</summary>
        protected abstract UniTask Init();

        /// <summary>
        /// 파생 클래스가 자신의 SO에서 _assetDatas를 채운 뒤 호출한다.
        /// 프리로드할 라벨을 명시적으로 받는다 — default(TLabel)은 플래그 enum에서 0(=없음)이라
        /// 아무것도 프리로드하지 않는다.
        /// </summary>
        protected virtual async UniTask LoadAssetReference(TLabel preloadLabel)
        {
            AssetReferenceMapping();
            await PreloadAssets(preloadLabel);
        }

        protected void AssetReferenceMapping()
        {
            foreach (var obj in _assetDatas)
            {
                if (!_assetMap.ContainsKey(obj.Index))
                    _assetMap.Add(obj.Index, obj);
            }
        }

        public async UniTask PreloadAssets(TLabel label)
        {
            int labelIndex = EnumConverter.Enum32ToInt(label);
            var assets = new List<IAssetResource>();

            foreach (var obj in _assetDatas)
            {
                if ((obj.LabelIndex & labelIndex) > 0)
                    assets.Add(obj);
            }

            await m_addressableManager.PreloadAssets(labelIndex, assets.ToArray());
        }

        public async UniTask<TAsset> LoadAsset<TAsset>(TKey key, CancellationToken ct = default)
            where TAsset : UnityEngine.Object
        {
            if (!_assetMap.TryGetValue(EnumConverter.Enum32ToInt(key), out var obj))
            {
                Warning($"에셋 참조를 찾지 못했다: {key}");
                return null;
            }

            return await m_addressableManager.Load<TAsset>(obj, ct);
        }

        protected async UniTask<TComponent> InstantiateObject<TComponent>(
            TKey key, Transform parent = null, bool isProtected = false)
        {
            if (!_assetMap.TryGetValue(EnumConverter.Enum32ToInt(key), out var obj))
            {
                Warning($"에셋 참조를 찾지 못했다: {key}");
                return default;
            }

            return await m_addressableManager.Instantiate<TComponent>(obj, parent, isProtected);
        }
    }
}
```

**주의 — 생성자에서 abstract 메서드를 호출한다.** `ReferenceManager`의 생성자가 `Init().Forget()`을
부르는데 `Init()`은 abstract다. 원본 설계를 그대로 가져온 것이고 현재는 동작한다 — 파생 클래스의
`Init()`이 base 생성자가 이미 설정한 `m_addressableManager`만 쓰고 자기 필드는 건드리지 않기
때문이다. 파생 클래스에 초기화가 필요한 필드를 추가한다면 `Init()`에서 그 필드를 쓰지 말 것.
(C#은 base 생성자가 파생 필드 초기화보다 먼저 돈다.)

- [ ] **Step 2: `PrefabManagerBase.cs`**

원본 `<CB>/Core/PrefabManager.cs`에서 프리팹 키 상수(`PrefabData.StaticCanvas` 등)를 뺀 형태다. 캔버스와 카메라를 어떤 키로 만들지는 프로젝트가 정한다.

```csharp
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// 프리팹 인스턴스화 + 공용 캔버스·카메라 관리.
    /// 캔버스와 카메라를 어떤 키로 생성할지는 프로젝트가 InitCanvases에서 정한다.
    /// </summary>
    public abstract class PrefabManagerBase<TKey, TLabel> : ReferenceManager<TKey, TLabel>
        where TKey : Enum
        where TLabel : Enum
    {
        private ISafeAreaFitter _staticCanvas;
        private ISafeAreaFitter _dynamicCanvas;
        private Camera _mainCamera;

        protected PrefabManagerBase(ManagerInitTracker tracker, AddressableManager addressableManager)
            : base(tracker, addressableManager) { }

        public RectTransform MainCanvas => _staticCanvas?.MyRT;
        public Camera MainCamera => _mainCamera;

        /// <summary>
        /// 프로젝트가 자신의 키로 카메라와 캔버스 둘을 만들어 넘긴다.
        /// 예: await SetupAsync(PrefabData.MainCamera, PrefabData.StaticCanvas, PrefabData.DynamicCanvas);
        /// </summary>
        protected async UniTask SetupAsync(TKey cameraKey, TKey staticCanvasKey, TKey dynamicCanvasKey)
        {
            _mainCamera = await InstantiateObject<Camera>(cameraKey, null, true);
            _staticCanvas = await InstantiateObject<ISafeAreaFitter>(staticCanvasKey, null, true);
            _dynamicCanvas = await InstantiateObject<ISafeAreaFitter>(dynamicCanvasKey, null, true);

            _staticCanvas.InitSafeArea();
            _dynamicCanvas.InitSafeArea();
            _staticCanvas.MyCanvas.worldCamera = _mainCamera;
            _dynamicCanvas.MyCanvas.worldCamera = _mainCamera;
        }

        public bool TryGetInstance<TComponent>(TKey key, out TComponent instance)
        {
            instance = default;

            if (!_assetMap.TryGetValue(EnumConverter.Enum32ToInt(key), out var obj))
                return false;

            if (obj.instance == null)
                return false;

            instance = obj.instance.GetComponent<TComponent>();
            return instance != null;
        }

        public UniTask<TComponent> InstantiatePrefab<TComponent>(
            TKey key, Transform parent = null, bool isProtected = false)
            => InstantiateObject<TComponent>(key, parent, isProtected);

        public UniTask<TComponent> InstantiateStaticUI<TComponent>(
            TKey key, Transform parent = null, bool isProtected = false)
            => InstantiateUI<TComponent>(key, parent ?? _staticCanvas.Root, isProtected);

        public UniTask<TComponent> InstantiateDynamicUI<TComponent>(
            TKey key, Transform parent = null, bool isProtected = false)
            => InstantiateUI<TComponent>(key, parent ?? _dynamicCanvas.Root, isProtected);

        private async UniTask<TComponent> InstantiateUI<TComponent>(TKey key, Transform parent, bool isProtected)
        {
            if (!_assetMap.TryGetValue(EnumConverter.Enum32ToInt(key), out var obj))
            {
                Warning($"에셋 참조를 찾지 못했다: {key}");
                return default;
            }

            if (obj.isInstance)
                return obj.instance.GetComponent<TComponent>();

            return await m_addressableManager.Instantiate<TComponent>(obj, parent, isProtected);
        }
    }
}
```

`ISafeAreaFitter`는 Task 7에서 UI 어셈블리에 만든다. 그런데 Resource가 UI를 참조하면 순환이 된다. **따라서 `ISafeAreaFitter`는 Core 어셈블리에 둔다.** Task 3에 추가로 만든다:

`<GameKit>/Runtime/Core/ISafeAreaFitter.cs`:

```csharp
using UnityEngine;

namespace LayonCraft.GameKit
{
    public interface ISafeAreaFitter
    {
        RectTransform MyRT { get; }
        Canvas MyCanvas { get; }
        Transform Root { get; }
        void InitSafeArea();
    }
}
```

Core asmdef에 `"Unity.ugui"` 참조를 추가해야 `Canvas`/`RectTransform`을 쓸 수 있다.

- [ ] **Step 3: `SoundManagerBase.cs`**

원본 `<CB>/Core/SoundManager.cs`에서 DOTween과 `UserSettings` 의존을 걷어낸 형태다.

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// BGM/SFX 재생과 볼륨 관리.
    /// 음소거 상태는 이 클래스가 소유하고, 어디에 저장할지는 프로젝트가 정한다.
    /// (원본은 UserSettings를 직접 읽었으나 그건 프로젝트 소유라 패키지가 참조할 수 없다.)
    /// </summary>
    public abstract class SoundManagerBase<TKey, TLabel> : ReferenceManager<TKey, TLabel>
        where TKey : Enum
        where TLabel : Enum
    {
        private const float FADE_DURATION = 0.5f;

        private AudioSource _bgmAudio;
        private AudioSource _sfxAudio;
        private bool _bgmMuted;
        private bool _sfxMuted;

        protected SoundManagerBase(ManagerInitTracker tracker, AddressableManager addressableManager)
            : base(tracker, addressableManager) { }

        public bool BgmMuted
        {
            get => _bgmMuted;
            set
            {
                _bgmMuted = value;
                if (_bgmAudio != null) _bgmAudio.mute = value;
            }
        }

        public bool SfxMuted
        {
            get => _sfxMuted;
            set
            {
                _sfxMuted = value;
                if (_sfxAudio != null) _sfxAudio.mute = value;
            }
        }

        /// <summary>파생 클래스가 Init()에서 호출해 오디오 소스를 만든다.</summary>
        protected void CreateAudioSources()
        {
            _bgmAudio = CreateSource("BGM", loop: true);
            _sfxAudio = CreateSource("SFX", loop: false);
            _bgmAudio.mute = _bgmMuted;
            _sfxAudio.mute = _sfxMuted;
        }

        private static AudioSource CreateSource(string name, bool loop)
        {
            var go = new GameObject(name);
            UnityEngine.Object.DontDestroyOnLoad(go);

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.volume = 1f;
            return src;
        }

        /// <summary>BGM을 교체한다. 기본값(default)을 넘기면 정지만 한다.</summary>
        public async UniTask PlayBgm(TKey key)
        {
            if (_bgmAudio == null)
                return;

            await FadeAsync(_bgmAudio, 0f);

            var clip = await LoadAsset<AudioClip>(key);
            if (clip == null)
            {
                _bgmAudio.Stop();
                return;
            }

            _bgmAudio.clip = clip;
            _bgmAudio.Play();

            await FadeAsync(_bgmAudio, 1f);
        }

        public async UniTask PlaySfx(TKey key, CancellationToken ct = default)
        {
            if (_sfxAudio == null)
                return;

            var clip = await LoadAsset<AudioClip>(key, ct);
            if (clip != null)
                _sfxAudio.PlayOneShot(clip);
        }

        private UniTask FadeAsync(AudioSource src, float target)
            => Tweening.LerpAsync(src.volume, target, FADE_DURATION, v => src.volume = v);
    }
}
```

- [ ] **Step 4: `GameTextTableBase.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// 텍스트 테이블. 각 항목이 언어별 문자열 배열을 갖는다.
    /// 예: public class GameTextTable : GameTextTableBase&lt;GameTextData&gt; { }
    /// </summary>
    public abstract class GameTextTableBase<TKey> : ScriptableObject where TKey : Enum
    {
        [Serializable]
        public class GameText
        {
            public TKey id;
            public string[] text;

            public int Index => EnumConverter.Enum32ToInt(id);
        }

        public List<GameText> textData;
    }
}
```

- [ ] **Step 5: `TextDataManagerBase.cs`**

```csharp
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// 텍스트 조회. 언어 enum은 프로젝트 소유이므로 여기서는 정수 인덱스만 안다.
    /// 프로젝트가 LanguageIndex에 자신의 enum을 변환해 넣는다.
    /// </summary>
    public abstract class TextDataManagerBase<TKey> : BaseManager where TKey : Enum
    {
        private readonly Dictionary<int, string[]> _map = new Dictionary<int, string[]>();

        protected readonly AddressableManager m_addressableManager;

        protected TextDataManagerBase(ManagerInitTracker tracker, AddressableManager addressableManager)
            : base(tracker)
        {
            m_addressableManager = addressableManager;
        }

        /// <summary>현재 언어의 인덱스. 프로젝트가 자신의 LanguageType enum을 변환해 넣는다.</summary>
        public int LanguageIndex { get; set; }

        /// <summary>파생 클래스가 테이블을 읽은 뒤 호출한다.</summary>
        protected void BuildMap<TTable>(TTable table) where TTable : GameTextTableBase<TKey>
        {
            _map.Clear();

            foreach (var entry in table.textData)
            {
                if (!_map.ContainsKey(entry.Index))
                    _map.Add(entry.Index, entry.text);
            }
        }

        public string GetText(TKey key)
        {
            if (!_map.TryGetValue(EnumConverter.Enum32ToInt(key), out var texts))
            {
                Warning($"텍스트를 찾지 못했다: {key}");
                return string.Empty;
            }

            if (LanguageIndex < 0 || LanguageIndex >= texts.Length)
            {
                Warning($"언어 인덱스 {LanguageIndex}가 범위를 벗어났다: {key}");
                return string.Empty;
            }

            return texts[LanguageIndex];
        }
    }
}
```

- [ ] **Step 6: 컴파일 확인 후 커밋**

**기대 결과:** 에러 0건. Core asmdef에 `Unity.ugui`를 추가하지 않으면 `ISafeAreaFitter`에서 `Canvas`를 못 찾는다.

```bash
git add Runtime/Core Runtime/Resource
git commit -m "feat: 매니저 베이스 4종 추가

ReferenceManager의 쓰이지 않던 유령 타입 파라미터를 키·라벨 enum으로
교체하고, SoundManagerBase는 음소거 상태를 자기가 소유하도록 뒤집었다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 6: Input 어셈블리

**Files:**
- Create: `<GameKit>/Runtime/Input/GameKitInput.inputactions`
- Create: `<GameKit>/Runtime/Input/GameKitInput.cs` (생성물)
- Create: `<GameKit>/Runtime/Input/InputManager.cs`
- 원본: `<CB>/Core/Input/`, `<CB>/Core/InputManager.cs`

**Consumes:** `BaseManager` (Task 3)

**Produces:** `InputManager` — `SubscribeToInputHandler`, `UnsubscribeToInputHandler`, `PushBackHandler(Action)`, `PopBackHandler(Action)`, `PushInputBlock()`, `PopInputBlock()`

- [ ] **Step 1: 액션 에셋 복사**

`<CB>/Core/Input/PlayerInput.inputactions`를 `<GameKit>/Runtime/Input/GameKitInput.inputactions`로 복사한다.

Unity에서 그 에셋을 선택하고 인스펙터에서:
- `Generate C# Class` 체크
- `C# Class Name`을 `GameKitInput`으로
- `C# Class Namespace`를 `LayonCraft.GameKit`으로
- `C# Class File`을 `Runtime/Input/GameKitInput.cs`로
- Apply

액션맵은 `Player { Point, Click, Exit }`로 이미 범용적이라 그대로 쓴다.

- [ ] **Step 2: `InputType` enum을 패키지에 정의**

`<GameKit>/Runtime/Input/InputType.cs`:

```csharp
namespace LayonCraft.GameKit
{
    /// <summary>패키지 액션맵의 액션 식별자.</summary>
    public enum InputType
    {
        Point,
        Click,
    }
}
```

`Game_Exit`는 넣지 않는다. 백키는 `InputManager`가 내부에서만 구독하고 외부에는 스택 API로만 노출한다. 원본에서 이 값이 공개돼 있어 `GameManager`와 팝업이 동시 구독하는 문제가 있었다.

- [ ] **Step 3: `InputManager.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

namespace LayonCraft.GameKit
{
    /// <summary>
    /// 입력 중개. 게임플레이 입력은 직접 구독시키고, 백키는 스택으로 관리해
    /// 최상단 핸들러만 실행되게 한다.
    /// </summary>
    public class InputManager : BaseManager
    {
        private readonly GameKitInput _input;
        private readonly List<Action> _backHandlers = new List<Action>();
        private int _blockDepth;

        public InputManager(ManagerInitTracker tracker) : base(tracker)
        {
            _input = new GameKitInput();
            _input.Player.Enable();
            _input.Player.Exit.performed += OnBackKeyPerformed;

            CompleteInit();
        }

        #region 백키 스택

        /// <summary>핸들러를 스택 최상단에 올린다. 이미 있으면 최상단으로 끌어올린다.</summary>
        public void PushBackHandler(Action handler)
        {
            if (handler == null) return;

            _backHandlers.Remove(handler);
            _backHandlers.Add(handler);
        }

        /// <summary>핸들러를 제거한다. 최상단이 아니어도, 없어도 안전하다.</summary>
        public void PopBackHandler(Action handler)
        {
            if (handler == null) return;

            _backHandlers.Remove(handler);
        }

        private void OnBackKeyPerformed(CallbackContext context)
        {
            if (_backHandlers.Count == 0) return;

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
            if (_blockDepth > 0) _blockDepth--;
            ApplyInputBlock();
        }

        private void ApplyInputBlock()
        {
            if (_blockDepth > 0)
            {
                _input.Player.Click.Disable();
                _input.Player.Point.Disable();
            }
            else
            {
                _input.Player.Click.Enable();
                _input.Player.Point.Enable();
            }
        }

        #endregion

        public void SubscribeToInputHandler(
            InputType type,
            Action<CallbackContext> start = null,
            Action<CallbackContext> perform = null,
            Action<CallbackContext> cancel = null)
        {
            var action = Resolve(type);
            if (action == null) return;

            if (start != null) action.started += start;
            if (perform != null) action.performed += perform;
            if (cancel != null) action.canceled += cancel;
        }

        public void UnsubscribeToInputHandler(
            InputType type,
            Action<CallbackContext> start = null,
            Action<CallbackContext> perform = null,
            Action<CallbackContext> cancel = null)
        {
            var action = Resolve(type);
            if (action == null) return;

            if (start != null) action.started -= start;
            if (perform != null) action.performed -= perform;
            if (cancel != null) action.canceled -= cancel;
        }

        private InputAction Resolve(InputType type) => type switch
        {
            InputType.Point => _input.Player.Point,
            InputType.Click => _input.Player.Click,
            _ => null,
        };
    }
}
```

- [ ] **Step 4: 컴파일 확인 후 커밋**

**기대 결과:** 에러 0건. `GameKitInput`을 못 찾으면 Step 1의 생성 설정이 적용되지 않은 것이다.

```bash
git add Runtime/Input
git commit -m "feat: Input 어셈블리 추가

백키를 스택으로 관리하고 Game_Exit를 공개 API에서 제거해,
여러 곳이 동시 구독해 순서에 의존하던 문제를 원천 차단했다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 7: UI 어셈블리

**Files:**
- Create: `<GameKit>/Runtime/UI/IBaseUI.cs`
- Create: `<GameKit>/Runtime/UI/BaseUI.cs`
- Create: `<GameKit>/Runtime/UI/CloseBaseUI.cs`
- Create: `<GameKit>/Runtime/UI/SafeAreaFitter.cs`
- Create: `<GameKit>/Runtime/UI/SlideToggle.cs`

**Consumes:** `InputManager` (Task 6), `ISafeAreaFitter` (Task 5 Step 2), `Tweening` (Task 2)

**Produces:** `BaseUI`, `CloseBaseUI` (백키·입력차단 자동 배선), `SafeAreaFitter`, `SlideToggle`

- [ ] **Step 1: `IBaseUI.cs`**

`<CB>/Share/Interface/IBaseUI.cs`를 복사하고 네임스페이스를 씌운다(6줄).

- [ ] **Step 2: `BaseUI.cs`**

```csharp
using UnityEngine;

namespace LayonCraft.GameKit
{
    /// <summary>의존성 없는 UI 베이스. 게임 진행을 막지 않는 UI가 상속한다.</summary>
    public class BaseUI : MonoBehaviour, IBaseUI
    {
        public virtual void Init() => gameObject.SetActive(true);

        public virtual void Close() => gameObject.SetActive(false);
    }
}
```

- [ ] **Step 3: `CloseBaseUI.cs`**

`<CB>/UI/CloseBaseUI.cs`를 복사하고 네임스페이스를 씌운다. 내용은 그대로 쓴다 — 이미 `_blocking` 플래그로 push/pop 균형이 맞춰져 있고 `OnDestroyed()`가 abstract다.

- [ ] **Step 4: `SafeAreaFitter.cs`**

`<CB>/UI/SafeAreaFitter.cs`를 복사하고 네임스페이스를 씌운다(45줄). `ISafeAreaFitter`는 Core에 있으므로 UI asmdef가 Core를 참조하면 된다(이미 참조한다).

- [ ] **Step 5: `SlideToggle.cs` — DOTween 제거**

`<CB>/ToolKit/Component/SlideToggle.cs`를 복사하고 네임스페이스를 씌운 뒤, 런타임 애니메이션을 `Tweening`으로 바꾼다.

`using DG.Tweening;`을 지우고, `_ease` 필드(`Ease` 타입)도 지운다. 런타임 이동 부분을 교체한다:

```csharp
// 기존:
//   _handle.rectTransform.DOLocalMoveX(targetX, _duration).SetEase(_ease);
// 변경:
float fromX = _handle.rectTransform.localPosition.x;
Tweening.LerpAsync(fromX, targetX, _duration,
    x =>
    {
        var p = _handle.rectTransform.localPosition;
        p.x = x;
        _handle.rectTransform.localPosition = p;
    },
    Tweening.EaseOutCubic).Forget();
```

에디터 프리뷰 쪽은 이미 `1f - Mathf.Pow(1f - t, 3f)`로 같은 곡선을 계산하고 있으므로, 그 부분을 `Tweening.EaseOutCubic(t)` 호출로 바꿔 중복을 없앤다.

`using Cysharp.Threading.Tasks;`를 추가해야 `.Forget()`을 쓸 수 있다.

- [ ] **Step 6: 컴파일 확인 후 커밋**

```bash
git add Runtime/UI
git commit -m "feat: UI 어셈블리 추가

SlideToggle의 런타임/에디터 애니메이션이 같은 곡선을 두 번 구현하던 걸
Tweening.EaseOutCubic으로 통일했다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 8: Editor — 설정 SO와 단순 도구 3종

**Files:**
- Create: `<GameKit>/Editor/GameKitEditorSettings.cs`
- Create: `<GameKit>/Editor/PlayerPrefsEditor.cs`
- Create: `<GameKit>/Editor/UIPrefabPostprocessor.cs`
- Create: `<GameKit>/Editor/SaveFieldDataGenerator.cs`

**Produces:** `GameKitEditorSettings.GetOrCreate()` — 이후 Editor 태스크들이 설정을 읽는 통로

- [ ] **Step 1: `GameKitEditorSettings.cs`**

```csharp
using UnityEditor;
using UnityEngine;

namespace LayonCraft.GameKit.Editor
{
    /// <summary>
    /// 패키지 Editor 도구들이 읽는 프로젝트별 설정.
    /// Assets/Editor/GameKitEditorSettings.asset 한 개를 두고 값을 채운다.
    /// <para>
    /// 키스토어 경로는 여기 두지 않는다. 이 에셋은 git에 커밋되므로 서명 키 위치가
    /// 저장소에 남는다. AutoKeystoreFile이 EditorPrefs에서 읽는다.
    /// </para>
    /// </summary>
    public class GameKitEditorSettings : ScriptableObject
    {
        private const string AssetPath = "Assets/Editor/GameKitEditorSettings.asset";

        [Header("SaveFieldData 생성")]
        [Tooltip("SaveField enum의 어셈블리 한정 타입명. 비우면 생성 명령이 동작하지 않는다.")]
        public string saveFieldEnumTypeName = "SaveFieldType, Assembly-CSharp";

        [Tooltip("생성된 파일을 쓸 경로.")]
        public string saveFieldOutputPath = "Assets/Scripts/Share/SaveFieldData.cs";

        [Header("UI 프리팹 후처리")]
        [Tooltip("이 경로 아래 프리팹이 임포트되면 후처리한다. 비우면 비활성화된다.")]
        public string uiPrefabPath = "Assets/AddressableAssets/Prefabs/UI/";

        [Header("빌드")]
        public int versionMajor = 1;
        public int versionMinor = 0;
        public int versionPatch = 0;

        [Tooltip("빌드 산출물을 둘 폴더.")]
        public string buildOutputPath = "Builds";

        [Tooltip("WebGL 빌드에 삽입할 GA4 측정 ID. 비우면 삽입하지 않는다.")]
        public string webglGtagMeasurementId = "";

        private static GameKitEditorSettings _cached;

        /// <summary>설정 에셋을 읽는다. 없으면 만든다.</summary>
        public static GameKitEditorSettings GetOrCreate()
        {
            if (_cached != null)
                return _cached;

            _cached = AssetDatabase.LoadAssetAtPath<GameKitEditorSettings>(AssetPath);
            if (_cached != null)
                return _cached;

            System.IO.Directory.CreateDirectory("Assets/Editor");

            _cached = CreateInstance<GameKitEditorSettings>();
            AssetDatabase.CreateAsset(_cached, AssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[GameKit] 설정 에셋을 만들었다: {AssetPath}");
            return _cached;
        }

        [MenuItem("Tools/GameKit/설정 열기")]
        private static void Open() => Selection.activeObject = GetOrCreate();
    }
}
```

- [ ] **Step 2: `SaveFieldDataGenerator.cs` — 설정 기반으로**

```csharp
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LayonCraft.GameKit.Editor
{
    /// <summary>
    /// SaveField enum에서 문자열 키 배열을 생성한다.
    /// 패키지는 프로젝트 enum을 참조할 수 없으므로 타입명을 설정에서 읽어 리플렉션으로 찾는다.
    /// </summary>
    public static class SaveFieldDataGenerator
    {
        [MenuItem("Tools/GameKit/SaveFieldData 생성")]
        public static void Generate()
        {
            var settings = GameKitEditorSettings.GetOrCreate();

            if (string.IsNullOrWhiteSpace(settings.saveFieldEnumTypeName))
            {
                Debug.LogError("[GameKit] 설정의 saveFieldEnumTypeName이 비어 있다.");
                return;
            }

            var enumType = Type.GetType(settings.saveFieldEnumTypeName);
            if (enumType == null || !enumType.IsEnum)
            {
                Debug.LogError($"[GameKit] enum 타입을 찾지 못했다: {settings.saveFieldEnumTypeName}");
                return;
            }

            string[] names = Enum.GetNames(enumType);
            string values = string.Join(",\n", names.Select(n => $"        \"{n}\""));

            string source = $@"// 이 파일은 Tools/GameKit/SaveFieldData 생성 으로 만들어진다. 직접 수정하지 말 것.
public static class SaveFieldData
{{
    public static readonly string[] Fields =
    {{
{values}
    }};
}}
";

            string outputPath = settings.saveFieldOutputPath;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, source, new UTF8Encoding(false));

            AssetDatabase.ImportAsset(outputPath);
            AssetDatabase.Refresh();

            Debug.Log($"[GameKit] {outputPath} 생성 완료 ({names.Length}개 필드)");
        }
    }
}
```

- [ ] **Step 3: `UIPrefabPostprocessor.cs` — 설정 기반으로**

`<CB>/Editor/UIPrefabPostprocessor.cs`를 복사하고, 하드코딩된 `UI_PATH` 상수를 설정에서 읽도록 바꾼다:

```csharp
static void OnPostprocessAllAssets(
    string[] importedAssets, string[] deletedAssets,
    string[] movedAssets, string[] movedFromAssetPaths)
{
    var settings = GameKitEditorSettings.GetOrCreate();
    if (string.IsNullOrWhiteSpace(settings.uiPrefabPath))
        return;

    foreach (var path in importedAssets)
    {
        if (!path.StartsWith(settings.uiPrefabPath))
            continue;

        // 이하 원본 로직 그대로
    }
}
```

- [ ] **Step 4: `PlayerPrefsEditor.cs`**

`<CB>/Editor/PlayerPrefsEditor.cs`를 복사하고 네임스페이스를 `LayonCraft.GameKit.Editor`로 바꾼다. 메뉴 경로를 `Tools/GameKit/PlayerPrefs/`로 바꾼다. 나머지는 그대로 쓴다 — `PlayerPrefs.DeleteAll` 같은 범용 동작이라 프로젝트 의존이 없다.

- [ ] **Step 5: 검증**

Unity에서:
1. `Tools/GameKit/설정 열기` → `Assets/Editor/GameKitEditorSettings.asset`이 만들어지고 인스펙터에 뜬다
2. 소비 프로젝트에 `SaveFieldType` enum을 하나 만들고 `Tools/GameKit/SaveFieldData 생성` 실행 → 파일이 생성된다
3. `Tools/GameKit/PlayerPrefs/...` 메뉴가 보인다

- [ ] **Step 6: 커밋**

```bash
git add Editor
git commit -m "feat: Editor 설정 SO와 단순 도구 3종 추가

하드코딩된 프로젝트 경로를 GameKitEditorSettings로 빼고,
SaveFieldDataGenerator는 enum 타입을 리플렉션으로 찾도록 바꿨다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 9: Editor — 제네릭 인스펙터 2종

**Files:**
- Create: `<GameKit>/Editor/AssetReferenceBaseEditor.cs`
- Create: `<GameKit>/Editor/GameTextTableEditor.cs`

- [ ] **Step 1: `AssetReferenceBaseEditor.cs`**

`<CB>/Editor/AssetReferenceBaseEditor.cs`(310줄)를 복사하고 네임스페이스를 씌운다.

`[CustomEditor(typeof(AssetReferenceBase<,>), true)]`를 타입 파라미터 3개에 맞춰 바꾼다:

```csharp
[CustomEditor(typeof(AssetReferenceBase<,,>), true)]
```

파일 안에서 `containLabel` 필드를 다루는 부분이 있다면 그대로 둔다 — `SerializedProperty`로 접근하므로 타입이 바뀌어도 동작한다.

- [ ] **Step 2: `GameTextTableEditor.cs`**

`<CB>/Editor/GameTextEditor.cs`(137줄)를 복사하고 네임스페이스를 씌운 뒤, 대상 타입을 열린 제네릭으로 바꾼다:

```csharp
// 기존: [CustomEditor(typeof(GameTextSO))]
[CustomEditor(typeof(GameTextTableBase<>), true)]
public class GameTextTableEditor : UnityEditor.Editor
```

**원본은 `LanguageType` enum을 4곳에서 참조한다** — 필드 선언, `EditorPrefs`에서 복원, 탭 이름
목록(`Enum.GetNames(typeof(LanguageType))`), 선택 시 캐스팅. 패키지는 프로젝트의 언어 enum을
모르므로 전부 정수 인덱스 기반으로 바꾼다.

```csharp
private const string LanguageIndexPrefKey = "GameKit.GameTextTableEditor.LanguageIndex";

private int _languageIndex;

private void OnEnable()
{
    _languageIndex = EditorPrefs.GetInt(LanguageIndexPrefKey, 0);
}

/// <summary>언어 개수는 첫 항목의 text 배열 길이로 추정한다.</summary>
private int GetLanguageCount()
{
    var list = serializedObject.FindProperty("textData");
    if (list == null || list.arraySize == 0)
        return 1;

    var first = list.GetArrayElementAtIndex(0).FindPropertyRelative("text");
    return first == null ? 1 : Mathf.Max(1, first.arraySize);
}

private void DrawLanguageSelector()
{
    int count = GetLanguageCount();

    var names = new string[count];
    for (int i = 0; i < count; i++)
        names[i] = $"언어 {i}";

    int newIndex = GUILayout.Toolbar(Mathf.Clamp(_languageIndex, 0, count - 1), names);

    if (newIndex != _languageIndex)
    {
        _languageIndex = newIndex;
        EditorPrefs.SetInt(LanguageIndexPrefKey, newIndex);
    }
}
```

탭 이름이 "언어 0/1"로 나오는 게 아쉽지만, 패키지가 프로젝트 enum 이름을 알 방법이 없다.
필요하면 `GameKitEditorSettings`에 언어 이름 배열을 추가해 읽게 할 수 있다.

- [ ] **Step 3: 검증**

소비 프로젝트에서 `PrefabAssetReference`와 `GameTextTable` 에셋을 하나씩 만들고 인스펙터를 연다.

**기대 결과:** 커스텀 인스펙터가 뜬다(기본 인스펙터가 아니라 리스트 UI가 보인다). 안 뜨면 `[CustomEditor]`의 제네릭 인자 개수가 안 맞는 것이다.

- [ ] **Step 4: 커밋**

```bash
git add Editor
git commit -m "feat: 제네릭 인스펙터 2종 추가

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 10: Editor — 빌드 도구 2종

**Files:**
- Create: `<GameKit>/Editor/BuildProcessor.cs`
- Create: `<GameKit>/Editor/AutoKeystoreFile.cs`

- [ ] **Step 1: `AutoKeystoreFile.cs` — EditorPrefs로**

`<CB>/Editor/AutoKeystoreFile.cs`를 복사하고, 하드코딩된 경로를 EditorPrefs로 바꾼다:

```csharp
private const string KeystoreInfoPathKey = "LayonCraft.GameKit.KeystoreInfoPath";

/// <summary>
/// 키스토어 정보 JSON의 경로. EditorPrefs에 둔다 — 설정 SO는 git에 커밋되므로
/// 서명 키 위치가 저장소에 남으면 안 된다.
/// </summary>
public static string KeystoreInfoPath
{
    get => EditorPrefs.GetString(KeystoreInfoPathKey, string.Empty);
    set => EditorPrefs.SetString(KeystoreInfoPathKey, value);
}

[MenuItem("Tools/GameKit/키스토어 경로 지정")]
private static void PickKeystoreInfo()
{
    string picked = EditorUtility.OpenFilePanel("키스토어 정보 JSON 선택", "", "json");
    if (!string.IsNullOrEmpty(picked))
    {
        KeystoreInfoPath = picked;
        Debug.Log($"[GameKit] 키스토어 정보 경로를 저장했다: {picked}");
    }
}
```

원본에서 `DEFAULT_KEYSTORE_INFO_PATH`를 쓰던 자리를 `KeystoreInfoPath`로 바꾸고, 비어 있으면 조용히 넘어가도록 가드를 넣는다:

```csharp
if (string.IsNullOrEmpty(KeystoreInfoPath) || !File.Exists(KeystoreInfoPath))
{
    Debug.LogWarning("[GameKit] 키스토어 정보 경로가 설정되지 않았다. Tools/GameKit/키스토어 경로 지정 을 실행할 것.");
    return;
}
```

- [ ] **Step 2: `BuildProcessor.cs` — 설정 기반으로**

`<CB>/Editor/BuildProcessor.cs`(268줄)를 복사하고 네임스페이스를 씌운 뒤 다음을 바꾼다.

1. 버전 상수를 설정에서 읽는다:

```csharp
// 기존: private static int _major = 1; ...
private static GameKitEditorSettings Settings => GameKitEditorSettings.GetOrCreate();

private static string VersionString
    => $"{Settings.versionMajor}.{Settings.versionMinor}.{Settings.versionPatch}";
```

2. 빌드 출력 경로를 설정에서 읽는다: `Settings.buildOutputPath`

3. WebGL gtag 삽입을 조건부로 바꾼다:

```csharp
private static void InjectGtag(string buildPath)
{
    string measurementId = Settings.webglGtagMeasurementId;

    if (string.IsNullOrWhiteSpace(measurementId))
    {
        Debug.Log("[GameKit] gtag 측정 ID가 비어 있어 삽입을 건너뛴다.");
        return;
    }

    // 원본 로직에서 하드코딩된 측정 ID를 measurementId로 치환
}
```

- [ ] **Step 3: 검증**

Unity에서 `Tools/GameKit/` 메뉴에 빌드 항목과 키스토어 경로 지정이 보인다. 키스토어 경로를 지정하지 않은 상태에서 빌드 관련 메뉴를 눌러도 예외 없이 경고만 뜬다.

- [ ] **Step 4: 커밋**

```bash
git add Editor
git commit -m "feat: 빌드 도구 2종 추가

키스토어 경로는 EditorPrefs에 둬 저장소에 남지 않게 하고,
버전·빌드 경로·gtag 측정 ID는 설정 SO에서 읽는다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 11: 뼈대 생성 명령

**Files:**
- Create: `<GameKit>/Editor/ProjectScaffolder.cs`

**Produces:** `Tools/GameKit/새 프로젝트 뼈대 생성` 메뉴

- [ ] **Step 1: `ProjectScaffolder.cs`**

```csharp
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LayonCraft.GameKit.Editor
{
    /// <summary>
    /// 새 프로젝트에서 반복되는 보일러플레이트를 생성한다.
    /// 생성물은 컴파일되는 스텁이며, 게임에 맞는 값은 직접 채운다.
    /// </summary>
    public static class ProjectScaffolder
    {
        private const string Root = "Assets/Scripts";

        [MenuItem("Tools/GameKit/새 프로젝트 뼈대 생성")]
        public static void Scaffold()
        {
            if (Directory.Exists($"{Root}/GameKitGenerated"))
            {
                if (!EditorUtility.DisplayDialog(
                        "뼈대 생성",
                        "이미 생성된 뼈대가 있다. 덮어쓸까?",
                        "덮어쓴다", "취소"))
                    return;
            }

            string dir = $"{Root}/GameKitGenerated";
            Directory.CreateDirectory(dir);

            Write($"{dir}/GameKeys.cs", GameKeysSource());
            Write($"{dir}/GameAssets.cs", GameAssetsSource());
            Write($"{dir}/GameManagers.cs", GameManagersSource());
            Write($"{dir}/GameLifetimeScope.cs", LifetimeScopeSource());

            AssetDatabase.Refresh();
            Debug.Log($"[GameKit] 뼈대를 생성했다: {dir}");
        }

        private static void Write(string path, string source)
            => File.WriteAllText(path, source, new UTF8Encoding(false));

        private static string GameKeysSource() => @"// GameKit 뼈대 생성물. 게임에 맞게 값을 채울 것.
using System;

public enum PrefabData
{
    MainCamera,
    StaticCanvas,
    DynamicCanvas,
}

[Flags]
public enum ContainLabel
{
    None = 0,
    Common = 1 << 0,
}

public enum SoundData
{
    None,
}

public enum GameTextData
{
    None,
}

public enum SaveFieldType
{
    IsBGMOn,
    IsSFXOn,
}

public enum LanguageType
{
    Korean,
    English,
}
";

        private static string GameAssetsSource() => @"// GameKit 뼈대 생성물.
using LayonCraft.GameKit;
using UnityEngine;

[CreateAssetMenu(fileName = ""PrefabAssetReference"", menuName = ""SO/PrefabAssetReference"")]
public class PrefabAssetReference : AssetReferenceBase<PrefabData, ContainLabel, GameObject> { }

[CreateAssetMenu(fileName = ""SoundAssetReference"", menuName = ""SO/SoundAssetReference"")]
public class SoundAssetReference : AssetReferenceBase<SoundData, ContainLabel, AudioClip> { }

[CreateAssetMenu(fileName = ""GameTextTable"", menuName = ""SO/GameTextTable"")]
public class GameTextTable : GameTextTableBase<GameTextData> { }
";

        private static string GameManagersSource() => @"// GameKit 뼈대 생성물.
using Cysharp.Threading.Tasks;
using LayonCraft.GameKit;

public class PrefabManager : PrefabManagerBase<PrefabData, ContainLabel>
{
    public PrefabManager(ManagerInitTracker tracker, AddressableManager addressable)
        : base(tracker, addressable) { }

    protected override async UniTask Init()
    {
        var table = await m_addressableManager.LoadResourceData<PrefabAssetReference>(nameof(PrefabAssetReference));
        _assetDatas = table.Resources;

        await LoadAssetReference(ContainLabel.Common);
        await SetupAsync(PrefabData.MainCamera, PrefabData.StaticCanvas, PrefabData.DynamicCanvas);

        CompleteInit();
    }
}

public class SoundManager : SoundManagerBase<SoundData, ContainLabel>
{
    public SoundManager(ManagerInitTracker tracker, AddressableManager addressable)
        : base(tracker, addressable) { }

    protected override async UniTask Init()
    {
        var table = await m_addressableManager.LoadResourceData<SoundAssetReference>(nameof(SoundAssetReference));
        _assetDatas = table.Resources;

        CreateAudioSources();
        await LoadAssetReference(ContainLabel.Common);

        CompleteInit();
    }
}

public class TextDataManager : TextDataManagerBase<GameTextData>
{
    public TextDataManager(ManagerInitTracker tracker, AddressableManager addressable)
        : base(tracker, addressable) { }

    public async UniTask LoadAsync()
    {
        var table = await m_addressableManager.LoadResourceData<GameTextTable>(nameof(GameTextTable));
        BuildMap(table);

        CompleteInit();
    }
}
";

        private static string LifetimeScopeSource() => @"// GameKit 뼈대 생성물.
using LayonCraft.GameKit;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // 생성된 키 배열을 PlayerPrefsStore에 주입한다.
        // SaveFieldData는 Tools/GameKit/SaveFieldData 생성 으로 만든다.
        // PlayerPrefsStore.SetKeys(SaveFieldData.Fields);

        builder.RegisterEntryPoint<ManagerInitTracker>(Lifetime.Singleton).AsSelf();
        builder.Register<ITelemetry, ConsoleTelemetry>(Lifetime.Singleton);

        builder.RegisterEntryPoint<AddressableManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<InputManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<PrefabManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<SoundManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<TextDataManager>(Lifetime.Singleton).AsSelf();
    }
}
";
    }
}
```

`ManagerInitTracker`는 `IInitializable`을 구현하지 않으므로 `RegisterEntryPoint`로 등록해도 즉시 생성되지 않는다. 그러나 다른 매니저들이 생성자에서 요구하므로 컨테이너가 그때 만든다. 문제없다.

- [ ] **Step 2: 검증**

소비 프로젝트에서 `Tools/GameKit/새 프로젝트 뼈대 생성` 실행.

**기대 결과:** `Assets/Scripts/GameKitGenerated/`에 파일 4개가 생기고 컴파일 에러가 없다.

- [ ] **Step 3: 커밋**

```bash
git add Editor
git commit -m "feat: 새 프로젝트 뼈대 생성 명령 추가

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 12: 최소 소비자 프로젝트로 종단 검증

**Files:** 없음(검증만)

이 태스크가 스펙의 완료 기준이다.

- [ ] **Step 1: 씬 준비**

소비 프로젝트에서 빈 씬을 만들고 GameObject 하나에 `GameLifetimeScope`를 붙인다.

- [ ] **Step 2: 에셋 준비**

1. `Assets/Create/SO/PrefabAssetReference`로 에셋 생성
2. Addressables Groups 창을 열어 그 에셋을 Addressable로 등록하고 주소를 `Assets/AddressableAssets/ScriptableObject/PrefabAssetReference.asset`으로 맞춘다
   (`AddressableManager.LoadResourceData<T>`가 이 경로 규칙을 쓴다 — 원본 코드를 그대로 옮겼으므로 동일하다)
3. 카메라·캔버스 프리팹 3개를 만들어 Addressable로 등록하고, `PrefabAssetReference`의 리스트에 `MainCamera`/`StaticCanvas`/`DynamicCanvas` 항목으로 연결한다
4. 캔버스 프리팹 둘에는 `SafeAreaFitter`를 붙인다
5. `SoundAssetReference`, `GameTextTable`도 같은 방식으로 만들어 등록한다(항목은 비어 있어도 된다)

- [ ] **Step 3: 플레이 모드 확인**

**기대 결과:**
1. Console에 매니저 생성자 로그가 찍힌다
2. `NullReferenceException`이 없다
3. 카메라와 캔버스 2개가 씬에 생성된다
4. `PrefabManager`의 `CompleteInit()`이 불려 게이트가 열린다

확인용으로 씬에 임시 MonoBehaviour를 하나 두고 다음을 실행해도 좋다:

```csharp
public class BootCheck : MonoBehaviour
{
    private PrefabManager m_prefab;
    private InputManager m_input;

    [Inject]
    public void Construct(PrefabManager prefab, InputManager input)
    {
        m_prefab = prefab;
        m_input = input;
    }

    private void Start()
    {
        LLogger.Log($"캔버스: {(m_prefab.MainCanvas != null ? "OK" : "없음")}");

        m_input.PushBackHandler(() => LLogger.Log("아래 핸들러"));
        m_input.PushBackHandler(() => LLogger.Log("위 핸들러"));
        // 백키를 누르면 "위 핸들러"만 찍혀야 한다.
    }
}
```

이 스크립트는 씬에 직접 두므로 `GameLifetimeScope`의 `autoInjectGameObjects`에 등록해야 주입된다.

- [ ] **Step 4: 백키 스택 확인**

Esc(또는 Android 뒤로가기)를 누른다.

**기대 결과:** "위 핸들러"만 찍힌다. 둘 다 찍히면 스택 구현이 잘못된 것이다.

- [ ] **Step 5: README 작성 후 커밋**

`<GameKit>/README.md`에 다음을 기록한다.

- 설치 방법(git URL, UniTask·VContainer를 소비 프로젝트가 직접 넣어야 함)
- Day 1 절차
- **유저 데이터 동기화 레시피** — 스펙의 해당 절을 그대로 옮긴다:
  - 클라우드 문서를 실제로 읽은 경우에만 서는 플래그로 저장을 가드해, 로컬 폴백 데이터가 클라우드를 덮어쓰지 못하게 한다
  - 인증 완료를 `UniTaskCompletionSource`로 신호화한다. 콜백 체인은 완료를 관측할 수단이 없어 폴링과 임의의 타임아웃을 부르는데, 모든 종료 지점에서 신호를 완료시키면 사라진다
  - 최종 플레이 시각을 로컬에도 기록하고, 로드 시 원격과 비교해 최신본을 채택한다

```bash
git add README.md
git commit -m "docs: 설치 절차와 유저 데이터 동기화 레시피 추가

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
git tag v0.1.0
```

---

## 알려진 미해결 사항

계획 실행 중 판단이 필요한 지점들이다. 지금 결정하지 않아도 되지만 알고 시작하는 게 낫다.

- **`ContainLabel` 타입 파라미터가 과할 수 있다.** 프리로드 그룹을 늘 하나만 쓴다면 제네릭 한 겹이 낭비다. Task 12에서 실제 사용감을 본 뒤, 부담스러우면 패키지 고정 enum으로 되돌린다. 되돌리는 작업은 `ReferenceManager`·`AssetReferenceBase`·`AddressableManager` 세 파일에 국한된다.
- **`AddressableManager.LoadResourceData<T>`가 경로 규칙을 하드코딩한다.** 원본이 `Assets/AddressableAssets/ScriptableObject/{name}.asset`을 쓴다. 프로젝트마다 다를 수 있으므로 나중에 설정으로 뺄 여지가 있다.
- **`SoundManagerBase`의 음소거 동기화 방향.** 패키지가 상태를 소유하고 프로젝트가 영속화한다. Color-Brick에서는 반대로 `UserSettings`가 진실의 원천이었으므로, 프로젝트 하위 클래스를 쓸 때 어느 쪽이 먼저인지 헷갈리지 않게 주석을 달아둘 것.
