# VContainer Full Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the `SingletonInstance<T>`/`ReferenceManager<T>` + `.Instance` singleton pattern with VContainer DI across every manager and every runtime-spawned gameplay/UI object, and retire the reflection-based `Bootstrap.cs` loader.

**Architecture:** `GameLifetimeScope` stays the single registration point for the 7 managers + `GameManager`. Managers convert from `.Instance` access to `[Inject]` method injection (MonoBehaviours can't take constructor injection). `AddressableManager.Instantiate<T>` — the single choke point every runtime prefab spawn already funnels through — gains `IObjectResolver` and calls `resolver.InjectGameObject(go)` right after instantiation, so every dynamically spawned object (`RoundManager`, `Board`, `Brick`, all UI popups) is auto-injected with no per-call-site changes needed.

**Tech Stack:** Unity, C#, VContainer, UniTask (Cysharp), Addressables.

## Global Constraints

- No automated test suite exists in this project. "Testing" a task means: the project compiles with no errors, and a manual Play-mode check in `Color_Brick.unity` shows the touched feature still works with no new console errors/exceptions.
- `PlayGamesPlatform.Instance` (`FirebaseManager.cs`) is a third-party SDK singleton — out of scope, do not touch.
- `EditBrickColor.unity`/`BrickColorEditorManager` and `Test.unity` are out of scope.
- Convert bottom-up by dependency so no task leaves a compile error from a half-converted neighbor: `AddressableManager` → `PrefabManager`/`ReferenceManager<T>` → `FirebaseManager` → `SoundManager`/`TextDataManager`/`InputManager`/`AdmobManager` → `GameManager` → `RoundManager`/`Board`/`Brick` → UI layer → cleanup.
- Every manager currently declares `[ManagerOrder(N)]` and implements `IManager` — both exist only to feed `Bootstrap.cs`'s reflection scan. Strip both from each manager's class declaration as part of that manager's own task (not saved for the end) so no file needs touching twice.

---

### Task 1: `ManagerBehaviour` base class + convert `AddressableManager`

**Files:**
- Create: `Assets/Scripts/Share/ManagerBehaviour.cs`
- Modify: `Assets/Scripts/Core/Core_Resource/AddressableManager.cs`

**Interfaces:**
- Produces: `ManagerBehaviour` (abstract `MonoBehaviour` subclass with `Logging`/`Warning`/`Error` protected helpers) — every other manager in later tasks extends this instead of `SingletonInstance<T>`.
- Produces: `AddressableManager` gains a public `[Inject] Construct(IObjectResolver resolver)` method; `Instantiate<T>` now injects the spawned GameObject.

- [ ] **Step 1: Create `ManagerBehaviour`**

`SingletonInstance<T>`'s `Logging`/`Warning`/`Error` helpers are pure logging convenience, unrelated to the singleton mechanics being removed. Give managers a lightweight non-singleton home for them:

```csharp
using UnityEngine;

public abstract class ManagerBehaviour : MonoBehaviour
{
    protected void Logging(string log)
    {
        LLogger.Log(log, color: Colors.Yellow, skipFrames: 2);
    }

    protected void Warning(string log)
    {
        LLogger.Log(log, level: LLogger.LogLevel.Warning, skipFrames: 2);
    }

    protected void Error(string log)
    {
        LLogger.Log(log, level: LLogger.LogLevel.Error, skipFrames: 2);
    }
}
```

- [ ] **Step 2: Convert `AddressableManager` to extend `ManagerBehaviour` and inject `IObjectResolver`**

Change the class declaration and add the resolver field (full file, since the diff touches the top and the `Instantiate` method):

```csharp
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using VContainer.Unity;

public class AddressableManager : ManagerBehaviour
{
    private IObjectResolver _resolver;
    private Dictionary<ContainLabel, List<AsyncOperationHandle>> _loadHandles = new Dictionary<ContainLabel, List<AsyncOperationHandle>>();
    private List<GameObject> _instantiateHandles = new List<GameObject>();

    [Inject]
    public void Construct(IObjectResolver resolver)
    {
        _resolver = resolver;
    }

    // ... SetAddressable, LoadRemoteAddressable, UpdateLoadGauage, CompletionAddressableLoad,
    // AssetReleaseForLabel, InstantiateRelease, PreloadAssets, Load<T> stay exactly as they are today.

    public async UniTask<T> Instantiate<T>(IAssetResource assetResource, Transform parent, bool isProtected)
    {
        var go = await assetResource.InstantiateAsync<GameObject>(parent);
        _resolver.InjectGameObject(go);

        var obj = go.AddComponent<InstantiateObject>();
        if (isProtected == false)
            _instantiateHandles.Add(go);

        obj.SetAssetReference(assetResource);

        return go.GetComponent<T>();
    }

    public async UniTask<T> LoadResourceData<T>(string name)
    {
        return await Addressables.LoadAssetAsync<T>($"Assets/AddressableAssets/ScriptableObject/{name}.asset");
    }
}
```

Delete the old `public override void Init() { base.Init(); }` — it did nothing beyond the singleton dedup logic that no longer exists.

- [ ] **Step 3: Compile and fix any errors in this file**

Unity Editor → wait for script compilation → Console shows no errors referencing `AddressableManager.cs`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Share/ManagerBehaviour.cs Assets/Scripts/Core/Core_Resource/AddressableManager.cs
git commit -m "vcontainer: convert AddressableManager off singleton pattern"
```

---

### Task 2: Convert `ReferenceManager<T>` + `PrefabManager`

**Files:**
- Modify: `Assets/Scripts/Core/ReferenceManager.cs`
- Modify: `Assets/Scripts/Core/PrefabManager.cs`

**Interfaces:**
- Consumes: `ManagerBehaviour` (Task 1), `AddressableManager` (Task 1, now injectable).
- Produces: `ReferenceManager<T>` exposes `protected AddressableManager _addressableManager` (populated via `[Inject]`) for `PrefabManager`/`SoundManager`/`TextDataManager` (later tasks) to use directly.

- [ ] **Step 1: Convert `ReferenceManager<T>`**

```csharp
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer;

public class ReferenceManager<T> : ManagerBehaviour
    where T : MonoBehaviour
{
    protected AddressableManager _addressableManager;
    protected Dictionary<int, IAssetResource> _assetMap = new Dictionary<int, IAssetResource>();
    protected IEnumerable<IAssetResource> _assetDatas = new List<IAssetResource>();

    [Inject]
    public void Construct(AddressableManager addressableManager)
    {
        _addressableManager = addressableManager;
    }

    async public virtual UniTask LoadAssetReference()
    {
        AssetReferenceMapping();
        await PreloadAssets(ContainLabel.Common);
    }

    protected void AssetReferenceMapping()
    {
        foreach (var obj in _assetDatas)
        {
            if (!_assetMap.ContainsKey(obj.Index))
            {
                _assetMap.Add(obj.Index, obj);
            }
        }
    }

    public async UniTask PreloadAssets(ContainLabel label)
    {
        List<IAssetResource> assets = new List<IAssetResource>();

        foreach (var obj in _assetDatas)
        {
            if ((obj.ContainLabel & label) > 0)
            {
                assets.Add(obj);
            }
        }
        await _addressableManager.PreloadAssets(label, assets.ToArray());
    }

    public async UniTask<TI> LoadAsset<TI>(int index, CancellationToken ct = new CancellationToken()) where TI : UnityEngine.Object
    {
        if (_assetMap.TryGetValue(index, out var obj) == false)
        {
            return default;
        }

        return await _addressableManager.Load<TI>(obj, ct);
    }

    protected async UniTask<TI> InstantiateObject<TI>(int index, Transform parent = null, bool isProtected = false)
    {
        if (_assetMap.TryGetValue(index, out var obj) == false)
        {
            return default;
        }

        if (parent == null)
            parent = this.transform;

        return await _addressableManager.Instantiate<TI>(obj, parent, isProtected);
    }
}
```

Note: `Init()` override is gone — nothing in the base class needs it now (removes the `SingletonInstance<T>` generic dedup logic entirely, since VContainer's `RegisterComponentOnNewGameObject` guarantees exactly one instance already).

- [ ] **Step 2: Convert `PrefabManager`**

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PrefabManager : ReferenceManager<PrefabManager>
{
    private ISafeAreaFitter _staticCanvas;
    private ISafeAreaFitter _dynamicCanvas;
    private Camera _mainCamera;
    public RectTransform MainCanvas => _staticCanvas.MyRT;
    public Camera MainCamera => _mainCamera;

    async public override UniTask LoadAssetReference()
    {
        var assets = await _addressableManager.LoadResourceData<PrefabAssetReference>(nameof(PrefabAssetReference));
        _assetDatas = assets.assetDatas;
        await base.LoadAssetReference();
    }

    async public UniTask InitLoadObjects()
    {
        _staticCanvas = await InstantiateObject<ISafeAreaFitter>(PrefabData.StaticCanvas, this.transform, true);
        _dynamicCanvas = await InstantiateObject<ISafeAreaFitter>(PrefabData.DynamicCanvas, this.transform, true);
        _staticCanvas.InitSafeArea();
        _dynamicCanvas.InitSafeArea();
        _staticCanvas.MyCanvas.worldCamera = _mainCamera;
        _dynamicCanvas.MyCanvas.worldCamera = _mainCamera;
    }

    public bool TryGetInstance<TI>(PrefabData type, out TI instance)
    {
        instance = default;
        if (_assetMap.TryGetValue(EnumConverter.Enum32ToInt(type), out var obj) == false)
            return false;

        if (obj.instance == null)
            return false;

        instance = obj.instance.GetComponent<TI>();
        return instance != null;
    }

    public async UniTask<TI> InstantiateObject<TI>(PrefabData type, Transform parent = null, bool isProtected = false)
    {
        return await InstantiateObject<TI>(EnumConverter.Enum32ToInt(type), parent, isProtected);
    }

    async public UniTask<TI> InstantiateDynamicUI<TI>(PrefabData type, Transform parent = null, bool isProtected = false)
    {
        if (parent == null)
            parent = _dynamicCanvas.Root;

        return await InstantiateUI<TI>(type, parent, isProtected);
    }

    async public UniTask<TI> InstantiateStaticUI<TI>(PrefabData type, Transform parent = null, bool isProtected = false)
    {
        if (parent == null)
            parent = _staticCanvas.Root;

        return await InstantiateUI<TI>(type, parent, isProtected);
    }

    async private UniTask<TI> InstantiateUI<TI>(PrefabData type, Transform parent, bool isProtected)
    {
        if (_assetMap.TryGetValue(EnumConverter.Enum32ToInt(type), out var obj) == false)
        {
            Logging($"Not Find AssetReference! {type}");
            return default;
        }

        if (obj.isInstance)
        {
            Logging($"Current Use Instance! {type}");
            return obj.instance.GetComponent<TI>();
        }

        return await _addressableManager.Instantiate<TI>(obj, parent, isProtected);
    }
}
```

Dropped: `: IManager` interface and the (already-empty) `Init()` override.

- [ ] **Step 3: Remove `[ManagerOrder(4)]` from `PrefabManager`'s class declaration** (already reflected in the code above — just confirm the attribute line is gone).

- [ ] **Step 4: Compile and fix errors**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/ReferenceManager.cs Assets/Scripts/Core/PrefabManager.cs
git commit -m "vcontainer: convert ReferenceManager and PrefabManager off singleton pattern"
```

---

### Task 3: Convert `FirebaseManager`

**Files:**
- Modify: `Assets/Scripts/Core/FirebaseManager.cs`

**Interfaces:**
- Consumes: `ManagerBehaviour` (Task 1), `PrefabManager` (Task 2).
- Produces: `FirebaseManager` injectable via `[Inject]` in later tasks (`SoundManager`, `AdmobManager`, `GameManager`, `RoundManager`, `Board`, most UI).

- [ ] **Step 1: Update the class declaration and add injection**

```csharp
public class FirebaseManager : ManagerBehaviour
{
    private PrefabManager _prefabManager;

    [Inject]
    public void Construct(PrefabManager prefabManager)
    {
        _prefabManager = prefabManager;
    }
    // ... rest of the fields/properties unchanged
```

Remove `[ManagerOrder(1)]` and `: IManager`.

- [ ] **Step 2: Replace `Init()` override with `Awake()`**

`public override void Init() { base.Init(); InitializeFirebase(); }` becomes:

```csharp
private void Awake()
{
    InitializeFirebase();
}
```

(VContainer injects `[Inject]` methods before `Awake` fires on container-created components, so `_prefabManager` is populated in time.)

- [ ] **Step 3: Replace the one internal `.Instance` call site**

Line ~576, inside `ShowForceUpdatePopupAsync`:

```csharp
// before
var popup = await PrefabManager.Instance.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI);
// after
var popup = await _prefabManager.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI);
```

- [ ] **Step 4: Leave every `PlayGamesPlatform.Instance` call untouched** — third-party SDK singleton, out of scope per Global Constraints.

- [ ] **Step 5: Compile and fix errors**

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Core/FirebaseManager.cs
git commit -m "vcontainer: convert FirebaseManager off singleton pattern"
```

---

### Task 4: Convert `SoundManager`

**Files:**
- Modify: `Assets/Scripts/Core/SoundManager.cs`
- Modify: `Assets/Scripts/Core/Sound/SoundEmitter.cs`

**Interfaces:**
- Consumes: `ReferenceManager<T>` (Task 2, provides `_addressableManager`), `FirebaseManager` (Task 3).
- Produces: `SoundManager` injectable via `[Inject]` in `RoundManager`, `Board`, `GameOverUI`, `GameLobbyUI`, `InGameUI`, `SoundSettingUI`, `SoundEmitter`.

- [ ] **Step 1: Update class declaration and injection**

```csharp
public class SoundManager : ReferenceManager<SoundManager>
{
    private FirebaseManager _firebaseManager;

    [Inject]
    public void Construct(FirebaseManager firebaseManager)
    {
        _firebaseManager = firebaseManager;
    }
    // fields (_bgmAudio, _sfxAudio, volume properties) unchanged
```

Remove `[ManagerOrder(6)]` and `: IManager`.

- [ ] **Step 2: Replace `Init()` override with `Awake()`**

```csharp
private void Awake()
{
    CreateBGMAudio();
    CreateSFXAudio();

    LoadSaveFieldData().Forget();
    SoundVolumPer = 0.5f;
}
```

- [ ] **Step 3: Replace every `FirebaseManager.Instance`/`AddressableManager.Instance` call in this file with the injected fields**

```csharp
// IsBGMOn getter/setter, IsSFXOn getter/setter, LoadSaveFieldData:
// FirebaseManager.Instance.IsBGMOn -> _firebaseManager.IsBGMOn
// FirebaseManager.Instance.IsSFXOn -> _firebaseManager.IsSFXOn

// LoadAssetReference:
// AddressableManager.Instance.LoadResourceData<SoundAssetReference>(...) -> _addressableManager.LoadResourceData<SoundAssetReference>(...)
```

- [ ] **Step 4: Update `SoundEmitter.cs`**

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using static SoundManager;

[RequireComponent(typeof(AudioSource))]
public class SoundEmitter : MonoBehaviour
{
    [SerializeField] private SoundData _soundType;
    private AudioSource _audioSource;
    private float _initVolum;
    private SoundManager _soundManager;

    [Inject]
    public void Construct(SoundManager soundManager)
    {
        _soundManager = soundManager;
    }

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _initVolum = _audioSource.volume;
        SetAudioClip().Forget();
        _soundManager.SubscribeToSoundHandler(UpdateVolum);
    }

    async private UniTask SetAudioClip()
    {
    }

    private void OnDestroy()
    {
        _soundManager?.UnsubscribeToSoundHandler(UpdateVolum);
    }

    private void UpdateVolum(float volumPer)
    {
        _audioSource.volume = _initVolum * volumPer;
    }

    public void PlaySound()
    {
        _audioSource.Play();
    }

    public void FadeSound(float value, float duration)
    {
    }
}
```

The `SoundManager.IsCreatedInstance()` guard is gone — there's no static singleton left to guard against, and `?.` handles the case where injection never ran.

- [ ] **Step 5: Compile and fix errors**

- [ ] **Step 6: Play `Color_Brick.unity`, confirm BGM/SFX still play and the mute toggles in the sound settings popup still work**

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Core/SoundManager.cs Assets/Scripts/Core/Sound/SoundEmitter.cs
git commit -m "vcontainer: convert SoundManager and SoundEmitter off singleton pattern"
```

---

### Task 5: Convert `TextDataManager`

**Files:**
- Modify: `Assets/Scripts/Core/TextDataManager.cs`

**Interfaces:**
- Consumes: `SingletonInstance<T>` removed → extends `ManagerBehaviour` directly (it doesn't share `ReferenceManager<T>`'s asset-map logic, just needs `AddressableManager`).
- Produces: `TextDataManager` injectable via `[Inject]` in `TextHandler`, `PopupQuestionUI`, `PopupNoticeUI`, `GameManager`.

- [ ] **Step 1: Update class declaration and injection**

```csharp
public class TextDataManager : ManagerBehaviour
{
    private AddressableManager _addressableManager;
    private GameTextSO _gameText;

    protected Dictionary<int, GameText> _gameTextMap = new Dictionary<int, GameText>();

    [Inject]
    public void Construct(AddressableManager addressableManager)
    {
        _addressableManager = addressableManager;
    }

    async public virtual UniTask LoadAssetReference()
    {
        _gameText = await _addressableManager.LoadResourceData<GameTextSO>(nameof(GameTextSO));
        AssetReferenceMapping();
    }

    protected void AssetReferenceMapping()
    {
        foreach (var text in _gameText.textData)
        {
            if (!_gameTextMap.ContainsKey(text.Index))
            {
                _gameTextMap.Add(text.Index, text);
            }
        }
    }

    public string GetGameText(GameTextData data)
    {
        var index = EnumConverter.Enum32ToInt(data);
        if (_gameTextMap.TryGetValue(index, out GameText gt) == false)
        {
            LLogger.Log($"Not Found Game Text : {data}");
            return string.Empty;
        }
        return "";
    }
}
```

Remove `[ManagerOrder(5)]` and `: IManager`; the empty `Init()` override is gone entirely (nothing left to run at startup for this manager).

- [ ] **Step 2: Compile and fix errors**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Core/TextDataManager.cs
git commit -m "vcontainer: convert TextDataManager off singleton pattern"
```

---

### Task 6: Convert `InputManager`

**Files:**
- Modify: `Assets/Scripts/Core/InputManager.cs`

**Interfaces:**
- Produces: `InputManager` injectable via `[Inject]` in `GameManager`, `Board`, `MenuUI`, `PopupQuestionUI`.

- [ ] **Step 1: Update class declaration and `Init()` → `Awake()`**

```csharp
public class InputManager : ManagerBehaviour
{
    private PlayerInput _inputHandler;

    public bool UseInputHandler
    {
        set
        {
            if (value)
                _inputHandler.Player.Enable();
            else
                _inputHandler.Player.Disable();
        }
    }

    private void Awake()
    {
        _inputHandler = new PlayerInput();
        UseInputHandler = true;
    }

    // SubscribeToInputHandler / UnsubscribeToInputHandler unchanged
}
```

Remove `[ManagerOrder(3)]` and `: IManager`.

- [ ] **Step 2: Compile and fix errors**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Core/InputManager.cs
git commit -m "vcontainer: convert InputManager off singleton pattern"
```

---

### Task 7: Convert `AdmobManager`

**Files:**
- Modify: `Assets/Scripts/Core/AdmobManager.cs`

**Interfaces:**
- Consumes: `FirebaseManager` (Task 3).
- Produces: `AdmobManager` injectable via `[Inject]` in `GameOverUI`.

- [ ] **Step 1: Update class declaration, injection, and `Init()` → `Awake()`**

```csharp
#if UNITY_ANDROID || UNITY_EDITOR
public class AdmobManager : ManagerBehaviour
{
    private FirebaseManager _firebaseManager;
    public bool IsPrivacyOptionsRequire = ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

    private BannerView bannerView;

    [Inject]
    public void Construct(FirebaseManager firebaseManager)
    {
        _firebaseManager = firebaseManager;
    }

    private void Awake()
    {
        Logging("Admob 초기화");
        RequestConsent();
    }
    // ...
```

Remove `[ManagerOrder(2)]` and `: IManager`.

- [ ] **Step 2: Replace both `FirebaseManager.Instance` call sites (`CreateBanner`, `CreateInterstitial`) with `_firebaseManager`**

- [ ] **Step 3: Compile and fix errors**

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Core/AdmobManager.cs
git commit -m "vcontainer: convert AdmobManager off singleton pattern"
```

---

### Task 8: Convert `GameManager` + fix its `GameLifetimeScope` registration

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Modify: `Assets/Scripts/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `FirebaseManager`, `InputManager`, `AddressableManager`, `PrefabManager`, `SoundManager`, `TextDataManager` (all prior tasks).
- Produces: `GameManager` injectable via `[Inject]` in `RoundManager`, `Board`.

`GameManager` currently stays a `MonoBehaviour` (it needs `OnApplicationPause`/`OnApplicationQuit`), but `GameLifetimeScope` registers it with `RegisterEntryPoint<GameManager>(...).AsSelf()` — that API expects to construct a plain C# object via reflection, which doesn't work for a `MonoBehaviour`. This task fixes that alongside the injection conversion.

- [ ] **Step 1: Update `GameLifetimeScope.cs`**

```csharp
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentOnNewGameObject<AddressableManager>(Lifetime.Singleton, nameof(AddressableManager));
        builder.RegisterComponentOnNewGameObject<PrefabManager>(Lifetime.Singleton, nameof(PrefabManager));
        builder.RegisterComponentOnNewGameObject<SoundManager>(Lifetime.Singleton, nameof(SoundManager));
        builder.RegisterComponentOnNewGameObject<TextDataManager>(Lifetime.Singleton, nameof(TextDataManager));
        builder.RegisterComponentOnNewGameObject<InputManager>(Lifetime.Singleton, nameof(InputManager));
        builder.RegisterComponentOnNewGameObject<FirebaseManager>(Lifetime.Singleton, nameof(FirebaseManager));
        builder.RegisterComponentOnNewGameObject<AdmobManager>(Lifetime.Singleton, nameof(AdmobManager));

        builder.RegisterComponentOnNewGameObject<GameManager>(Lifetime.Singleton, nameof(GameManager))
            .AsSelf()
            .AsImplementedInterfaces();
    }
}
```

`.AsImplementedInterfaces()` is what makes VContainer discover and drive `IAsyncStartable`/`IDisposable` on `GameManager` automatically.

- [ ] **Step 2: Update `GameManager.cs` class declaration and injection**

```csharp
public class GameManager : MonoBehaviour, IManager, IAsyncStartable, IDisposable
{
    private FirebaseManager _firebaseManager;
    private InputManager _inputManager;
    private AddressableManager _addressableManager;
    private PrefabManager _prefabManager;
    private SoundManager _soundManager;
    private TextDataManager _textDataManager;

    [Inject]
    public void Construct(
        FirebaseManager firebaseManager,
        InputManager inputManager,
        AddressableManager addressableManager,
        PrefabManager prefabManager,
        SoundManager soundManager,
        TextDataManager textDataManager)
    {
        _firebaseManager = firebaseManager;
        _inputManager = inputManager;
        _addressableManager = addressableManager;
        _prefabManager = prefabManager;
        _soundManager = soundManager;
        _textDataManager = textDataManager;
    }

    // HighScore / IsSymbolOn properties: replace FirebaseManager.Instance with _firebaseManager
    // Language, _roundManager, _lobbyUI, _loadingUI, catureEnterTime fields unchanged
```

Drop `: IManager` here too, matching every other manager — final declaration: `public class GameManager : MonoBehaviour, IAsyncStartable, IDisposable`.

Add `using VContainer;` to this file's `using` block for `[Inject]` (it currently only has `using VContainer.Unity;`).

- [ ] **Step 3: Replace `Bootstrap()` with `StartAsync`, fill in `Dispose`**

```csharp
async public UniTask StartAsync(CancellationToken cancellation = default)
{
    ResolutionScreen.InitResolution();
    _inputManager.SubscribeToInputHandler(InputType.Game_Exit, OnClickExit);
    await UniTask.WaitUntil(() => _firebaseManager.IsInitialized, cancellationToken: cancellation);
    _firebaseManager.Log("AddressableManager Init");
    await _addressableManager.SetAddressable();
    _firebaseManager.Log("PrefabManager Init");
    await _prefabManager.LoadAssetReference();
    _firebaseManager.Log("SoundManager Init");
    await _soundManager.LoadAssetReference();
    _firebaseManager.Log("TextDataManager Init");
    await _textDataManager.LoadAssetReference();
    _firebaseManager.Log("PrefabManager Load");
    await _prefabManager.InitLoadObjects();
    _firebaseManager.Log("Force Update Check");
    await _firebaseManager.CheckForForceUpdateAsync();
    await UniTask.WaitUntil(() => _firebaseManager?.IsUpdate ?? false, cancellationToken: cancellation);
    _lobbyUI = await _prefabManager.InstantiateStaticUI<IBaseUI>(PrefabData.LobbyUI);
    _loadingUI = await _prefabManager.InstantiateStaticUI<IBaseUI>(PrefabData.LoadingUI);
    _lobbyUI.Init();
    _loadingUI.Init();
    await UniTask.WaitUntil(() => _firebaseManager.IsLoadData, cancellationToken: cancellation);
    await UniTask.WaitForSeconds(2f, cancellationToken: cancellation);
    _loadingUI.Close();
}

public void Dispose()
{
    // No unmanaged resources or subscriptions owned directly by GameManager need explicit teardown today;
    // OnApplicationQuit already persists state. Left intentionally empty rather than throwing.
}
```

- [ ] **Step 4: Replace every remaining `.Instance` call in the rest of the file** (`StartRound`, `ExitRound`, `ShowExitToast`, `OnApplicationPause`, `OnApplicationQuit`) with the injected fields (`_prefabManager`, `_firebaseManager`).

- [ ] **Step 5: Add `using System;` and `using System.Threading;` if not already present** (needed for `IDisposable`, `CancellationToken`).

- [ ] **Step 6: Compile and fix errors**

- [ ] **Step 7: Play `Color_Brick.unity` from a clean stopped state — confirm the full boot sequence runs (lobby loads, no console errors) exactly as before**

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Core/GameManager.cs Assets/Scripts/GameLifetimeScope.cs
git commit -m "vcontainer: convert GameManager to a properly-registered VContainer entry point"
```

---

### Task 9: Convert `RoundManager`, `Board`, `Brick`

**Files:**
- Modify: `Assets/Scripts/Game/RoundManager.cs`
- Modify: `Assets/Scripts/Game/Board.cs`

**Interfaces:**
- Consumes: `PrefabManager`, `GameManager`, `FirebaseManager`, `InputManager`, `SoundManager` (all prior tasks). Spawned via `PrefabManager.InstantiateObject<RoundObject>` → `AddressableManager.Instantiate<T>`, which now auto-injects (Task 1).
- Note: `Brick.cs` has no `.Instance` calls (only a commented-out `GameManager.Instance` line) — no changes needed; it will still be auto-injected harmlessly if VContainer's `InjectGameObject` finds no `[Inject]` methods on it.

- [ ] **Step 1: Update `RoundManager.cs`**

```csharp
using Cysharp.Threading.Tasks;
using System;
using VContainer;

public class RoundManager : MonoBehaviour, IRound
{
    public int CurrentScore => _scoreValue;
    public event Action OnUpdateSymbolState;
    private const float COMBO_DELAY = 5f;
    private RoundObject _board;
    private IScore _ingameUI;
    private IScore _gameOver;
    private PrefabManager _prefabManager;
    private FirebaseManager _firebaseManager;
    private GameManager _gameManager;

    private int _scoreValue = 0;
    private int _comboValue = 0;
    private int _maxCombo = 0;
    private TimerModule _timer;

    [Inject]
    public void Construct(PrefabManager prefabManager, FirebaseManager firebaseManager, GameManager gameManager)
    {
        _prefabManager = prefabManager;
        _firebaseManager = firebaseManager;
        _gameManager = gameManager;
    }

    async public UniTask Init()
    {
        await LoadRoundObjects();
    }

    async private UniTask LoadRoundObjects()
    {
        _ingameUI = await _prefabManager.InstantiateStaticUI<IScore>(PrefabData.InGameUI);
        _gameOver = await _prefabManager.InstantiateDynamicUI<IScore>(PrefabData.GameOverUI);
        _board = await _prefabManager.InstantiateObject<RoundObject>(PrefabData.Board, this.transform);
        _board.SetRoundManager(this);

        _timer = Timer.CreateTimer(COMBO_DELAY, ResetCombo);
    }

    public void ChangeSymbolState()
    {
        OnUpdateSymbolState();
    }

    public void EnterRound()
    {
        _gameManager.catureEnterTime = Time.realtimeSinceStartup;
        _firebaseManager.LogModeStart("Classic");
        gameObject.SetActive(true);
        _scoreValue = 0;
        _comboValue = 0;
        _maxCombo = 0;
        _ingameUI.Init();
        _board.Init();
    }

    public void EndRound()
    {
        _firebaseManager.SetCustomKey("mode", "Classic");
        _firebaseManager.LogGameOver("Classic", _scoreValue, _maxCombo);
        _ingameUI.Close();
        gameObject.SetActive(false);
        _gameOver.Init();
        _gameOver.SetScore(_scoreValue);
        _gameOver.UpdateCombo(_maxCombo);
    }

    public void ExitRound()
    {
        _firebaseManager.LogModeQuit("Classic", Time.realtimeSinceStartup - _gameManager.catureEnterTime, _scoreValue);
        _ingameUI.Close();
        _gameOver.Close();
        Destroy(gameObject);
    }

    public void DestroyMatchBricks(int addScore, Vector2 boundCenter)
    {
        UpdateCombo(addScore > 0, boundCenter);

        _scoreValue += Utility.CalcScore(addScore, _comboValue);
        _firebaseManager.SetCustomKey("score", _scoreValue.ToString());
        _ingameUI.SetScore(_scoreValue);
    }

    private void UpdateCombo(bool isCombo, Vector2 boundCenter)
    {
        if (isCombo == false)
            return;

        Utility.AsyncDurationVibrateObject(_prefabManager.MainCamera.transform, new System.Threading.CancellationTokenSource()).Forget();
        _comboValue++;
        _ingameUI.UpdateCombo(_comboValue, boundCenter);
        _timer.Start();
        _maxCombo = Mathf.Max(_comboValue, _maxCombo);
    }

    private void ResetCombo()
    {
        _comboValue = 0;
        _ingameUI.UpdateCombo(_comboValue, Vector2.zero);
        _timer.Reset();
    }
}
```

(Add `using UnityEngine;` if the original file relied on an implicit global import — check the existing `using` block; it already has it.)

- [ ] **Step 2: Update `Board.cs`**

Add fields + injection, and replace the 6 `.Instance` call sites (`Awake`, `OnDestroy`, `InitBrick`, `SlideBrick`, `DestroyMatches`):

```csharp
// add near the top of the class, alongside the other private fields:
private InputManager _inputManager;
private SoundManager _soundManager;
private PrefabManager _prefabManager;

[Inject]
public void Construct(InputManager inputManager, SoundManager soundManager, PrefabManager prefabManager)
{
    _inputManager = inputManager;
    _soundManager = soundManager;
    _prefabManager = prefabManager;
}
```

```csharp
// Awake()
private void Awake()
{
    _inputManager.SubscribeToInputHandler(InputType.Player_Touch, OnTouchPoint, cancel: OnEndTouchPoint);
    _inputManager.SubscribeToInputHandler(InputType.Player_Point, perform: OnDragPoint);

    _bricks.Clear();
}

// OnDestroy()
private void OnDestroy()
{
    ResetToken();
    _inputManager.UnsubscribeToInputHandler(InputType.Player_Touch, OnTouchPoint, cancel: OnEndTouchPoint);
    _inputManager.UnsubscribeToInputHandler(InputType.Player_Point, perform: OnDragPoint);
}
```

```csharp
// InitBrick(): PrefabManager.Instance.InstantiateObject<Brick>(...) -> _prefabManager.InstantiateObject<Brick>(...)
// SlideBrick(): SoundManager.Instance.PlaySFX(SoundData.Slide) -> _soundManager.PlaySFX(SoundData.Slide)
// DestroyMatches(): SoundManager.Instance.PlaySFX(SoundData.Match) -> _soundManager.PlaySFX(SoundData.Match)
```

Add `using VContainer;` to `Board.cs`'s using block.

- [ ] **Step 3: Compile and fix errors**

- [ ] **Step 4: Play `Color_Brick.unity`, start a round, confirm input (touch/drag), sound (slide/match SFX), scoring, and combo vibration all still work, then trigger game over**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Game/RoundManager.cs Assets/Scripts/Game/Board.cs
git commit -m "vcontainer: convert RoundManager and Board off singleton pattern"
```

---

### Task 10: Convert the UI layer

**Files:**
- Modify: `Assets/Scripts/UI/GameLobbyUI.cs`
- Modify: `Assets/Scripts/UI/GameOverUI.cs`
- Modify: `Assets/Scripts/UI/InGameUI.cs`
- Modify: `Assets/Scripts/UI/MenuUI.cs`
- Modify: `Assets/Scripts/UI/InquriyUI.cs`
- Modify: `Assets/Scripts/UI/PopupQuestionUI.cs`
- Modify: `Assets/Scripts/UI/PopupNoticeUI.cs`
- Modify: `Assets/Scripts/UI/SoundSettingUI.cs`
- Modify: `Assets/Scripts/UI/IngameScoreUI.cs`
- Modify: `Assets/Scripts/UI/TextHandler.cs`

**Interfaces:**
- Consumes: `SoundManager`, `PrefabManager`, `FirebaseManager`, `AdmobManager`, `InputManager`, `TextDataManager` (all prior tasks). All these components are spawned via `PrefabManager`'s `InstantiateDynamicUI`/`InstantiateStaticUI` → `AddressableManager.Instantiate<T>`, so `[Inject]` methods on them are called automatically the same way as Task 9.

Same mechanical pattern for every file: add `using VContainer;`, add private fields + one `[Inject] Construct(...)` method, replace `X.Instance` with the injected field.

- [ ] **Step 1: `GameLobbyUI.cs`** — needs `SoundManager`, `PrefabManager`, `FirebaseManager`

```csharp
private SoundManager _soundManager;
private PrefabManager _prefabManager;
private FirebaseManager _firebaseManager;

[Inject]
public void Construct(SoundManager soundManager, PrefabManager prefabManager, FirebaseManager firebaseManager)
{
    _soundManager = soundManager;
    _prefabManager = prefabManager;
    _firebaseManager = firebaseManager;
}
```
Replace: `SoundManager.Instance.PlayBGM(SoundData.Lobby)` → `_soundManager.PlayBGM(SoundData.Lobby)`; `PrefabManager.Instance.InstantiateDynamicUI<IBaseUI>(PrefabData.LegalUI)` → `_prefabManager.InstantiateDynamicUI<IBaseUI>(PrefabData.LegalUI)`; `FirebaseManager.Instance.ShowLeaderboardUI()` → `_firebaseManager.ShowLeaderboardUI()`; `PrefabManager.Instance.MainCanvas` → `_prefabManager.MainCanvas`.

- [ ] **Step 2: `GameOverUI.cs`** — needs `SoundManager`, and `AdmobManager` guarded by the same `#if UNITY_ANDROID || UNITY_EDITOR` the call site already uses

```csharp
private SoundManager _soundManager;
#if UNITY_ANDROID || UNITY_EDITOR
private AdmobManager _admobManager;
#endif

[Inject]
public void Construct(
    SoundManager soundManager
#if UNITY_ANDROID || UNITY_EDITOR
    , AdmobManager admobManager
#endif
    )
{
    _soundManager = soundManager;
#if UNITY_ANDROID || UNITY_EDITOR
    _admobManager = admobManager;
#endif
}
```
Replace: `SoundManager.Instance.PlayBGM()` → `_soundManager.PlayBGM()`; `SoundManager.Instance.PlaySFX(SoundData.Confetti)` → `_soundManager.PlaySFX(SoundData.Confetti)`; `AdmobManager.Instance.CreateInterstitial(2f)` → `_admobManager.CreateInterstitial(2f)`.

- [ ] **Step 3: `InGameUI.cs`** — needs `SoundManager`, `PrefabManager`

```csharp
private SoundManager _soundManager;
private PrefabManager _prefabManager;

[Inject]
public void Construct(SoundManager soundManager, PrefabManager prefabManager)
{
    _soundManager = soundManager;
    _prefabManager = prefabManager;
}
```
Replace: `SoundManager.Instance.PlayBGM(SoundData.Ingame)` → `_soundManager.PlayBGM(SoundData.Ingame)`; both `PrefabManager.Instance.InstantiateDynamicUI<...>` calls → `_prefabManager.InstantiateDynamicUI<...>`.

- [ ] **Step 4: `MenuUI.cs`** — needs `InputManager`, `PrefabManager`

```csharp
private InputManager _inputManager;
private PrefabManager _prefabManager;

[Inject]
public void Construct(InputManager inputManager, PrefabManager prefabManager)
{
    _inputManager = inputManager;
    _prefabManager = prefabManager;
}
```
Replace: both `InputManager.Instance.UseInputHandler` reads/writes → `_inputManager.UseInputHandler`; `PrefabManager.Instance.InstantiateDynamicUI<IBaseUI>(PrefabData.InquriyUI, this.transform)` → `_prefabManager.InstantiateDynamicUI<IBaseUI>(PrefabData.InquriyUI, this.transform)`.

- [ ] **Step 5: `InquriyUI.cs`** — needs `FirebaseManager`, `PrefabManager`

```csharp
private FirebaseManager _firebaseManager;
private PrefabManager _prefabManager;

[Inject]
public void Construct(FirebaseManager firebaseManager, PrefabManager prefabManager)
{
    _firebaseManager = firebaseManager;
    _prefabManager = prefabManager;
}
```
Replace: `FirebaseManager.Instance.SendInquiryAsync(...)` → `_firebaseManager.SendInquiryAsync(...)`; `PrefabManager.Instance.InstantiateDynamicUI<IPopupNotice>(PrefabData.PopupNoticeUI)` → `_prefabManager.InstantiateDynamicUI<IPopupNotice>(PrefabData.PopupNoticeUI)`.

- [ ] **Step 6: `PopupQuestionUI.cs`** — needs `InputManager`, `TextDataManager`

```csharp
private InputManager _inputManager;
private TextDataManager _textDataManager;

[Inject]
public void Construct(InputManager inputManager, TextDataManager textDataManager)
{
    _inputManager = inputManager;
    _textDataManager = textDataManager;
}
```
Replace: both `InputManager.Instance.SubscribeToInputHandler`/`UnsubscribeToInputHandler` → injected field; `TextDataManager.Instance.GetGameText(content)` → `_textDataManager.GetGameText(content)`.

- [ ] **Step 7: `PopupNoticeUI.cs`** — needs `TextDataManager`

```csharp
private TextDataManager _textDataManager;

[Inject]
public void Construct(TextDataManager textDataManager)
{
    _textDataManager = textDataManager;
}
```
Replace: `TextDataManager.Instance.GetGameText(content)` → `_textDataManager.GetGameText(content)`.

- [ ] **Step 8: `SoundSettingUI.cs`** — needs `SoundManager`

```csharp
private SoundManager _soundManager;

[Inject]
public void Construct(SoundManager soundManager)
{
    _soundManager = soundManager;
}
```
Replace all 4 `SoundManager.Instance.*` reads/writes (`IsBGMOn` get/set, `IsSFXOn` get/set) → `_soundManager.*`.

- [ ] **Step 9: `IngameScoreUI.cs`** — needs `PrefabManager`

```csharp
private PrefabManager _prefabManager;

[Inject]
public void Construct(PrefabManager prefabManager)
{
    _prefabManager = prefabManager;
}
```
Replace: `PrefabManager.Instance.MainCanvas` → `_prefabManager.MainCanvas` (in `ChangeResolution`).

- [ ] **Step 10: `TextHandler.cs`** — needs `TextDataManager`

```csharp
using TMPro;
using UnityEngine;
using VContainer;

public class TextHandler : MonoBehaviour
{
    [SerializeField] private GameTextData _textData;
    [SerializeField] private TextMeshProUGUI _handler;
    private TextDataManager _textDataManager;

    [Inject]
    public void Construct(TextDataManager textDataManager)
    {
        _textDataManager = textDataManager;
    }

    private void Awake()
    {
        _handler.text = _textDataManager.GetGameText(_textData);
    }
}
```

- [ ] **Step 11: Add `using VContainer;` to every file touched in this task that doesn't already have it**

- [ ] **Step 12: Compile and fix errors**

- [ ] **Step 13: Play `Color_Brick.unity` end to end** — lobby (BGM, leaderboard button, legal popup), start a round, in-round menu (symbol toggle, sound settings, inquiry form), game over screen (score, combo, retry/home), force-update popup if applicable. Confirm no console errors.

- [ ] **Step 14: Commit**

```bash
git add Assets/Scripts/UI/GameLobbyUI.cs Assets/Scripts/UI/GameOverUI.cs Assets/Scripts/UI/InGameUI.cs Assets/Scripts/UI/MenuUI.cs Assets/Scripts/UI/InquriyUI.cs Assets/Scripts/UI/PopupQuestionUI.cs Assets/Scripts/UI/PopupNoticeUI.cs Assets/Scripts/UI/SoundSettingUI.cs Assets/Scripts/UI/IngameScoreUI.cs Assets/Scripts/UI/TextHandler.cs
git commit -m "vcontainer: convert UI layer off singleton pattern"
```

---

### Task 11: Delete `Bootstrap.cs` and remove dead singleton machinery

**Files:**
- Delete: `Assets/Scripts/Bootstrap.cs` (and its `.meta`)
- Delete: `Assets/Scripts/Core/Interface/IManager.cs` (and its `.meta`)
- Delete: `Assets/Scripts/Core/Attribute/ManagerOrderAttribute.cs` (and its `.meta`)
- Delete: `Assets/Scripts/Share/SingletonInstance.cs` (and its `.meta`)

**Interfaces:**
- Consumes: every manager task above must already have `IManager`/`[ManagerOrder(N)]` removed from its declaration (Tasks 1–8) — this task only removes the now-unreferenced definitions.

- [ ] **Step 1: Confirm zero remaining references before deleting**

```bash
grep -rn "IManager\|ManagerOrder\|SingletonInstance" Assets/Scripts --include=*.cs
```

Expected: no matches (every manager's declaration was already cleaned up in its own task). If anything shows up, finish converting that file before proceeding.

- [ ] **Step 2: Delete `Bootstrap.cs`**

Also find and remove the `GameObject` in `Color_Brick.unity` that carries the `Bootstrap` component (if any) — Unity will otherwise show a missing-script warning on that GameObject when the scene loads. Open the scene, search the Hierarchy for a `Bootstrap` object, delete it. `GameLifetimeScope`'s own GameObject (with `autoRun` enabled, which is the VContainer default) now does everything `Bootstrap.cs` used to do.

- [ ] **Step 3: Delete `IManager.cs`, `ManagerOrderAttribute.cs`, `SingletonInstance.cs`**

- [ ] **Step 4: Compile and fix errors**

- [ ] **Step 5: Commit**

```bash
git add -A Assets/Scripts/Bootstrap.cs* Assets/Scripts/Core/Interface/IManager.cs* Assets/Scripts/Core/Attribute/ManagerOrderAttribute.cs* Assets/Scripts/Share/SingletonInstance.cs* Assets/Scenes/Color_Brick.unity
git commit -m "vcontainer: remove Bootstrap.cs and dead singleton machinery"
```

---

### Task 12: Full manual verification

**Files:** none — this task is a Play-mode pass, no code changes expected.

- [ ] **Step 1: Open `Color_Brick.unity` fresh (not already in Play mode) and enter Play mode**

Confirm the Console shows no errors/exceptions during boot (Addressables init, all manager `Awake`/`[Inject]` firing, lobby UI appearing).

- [ ] **Step 2: Walk the full golden path**

Lobby → start a Classic round → play a few moves (touch/drag input, brick matching, combo, score) → trigger game over (clear the board or let it fill) → game over screen (score/combo display, high score confetti + interstitial ad path if applicable) → retry → home → exit round → app pause (background the app or use `OnApplicationPause` in Editor via `Simulate` if available) → quit.

- [ ] **Step 3: If anything regressed, use `git bisect` across this branch's commits (Tasks 1–11) to isolate which task introduced it, then fix forward with a new commit — do not amend earlier commits.**

- [ ] **Step 4: Push the branch (only if the user asks) or hand off for PR review**

No commit in this task unless a regression fix was made.
