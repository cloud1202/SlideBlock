# VContainer Full Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Note for this project:** the user is implementing this plan by hand in the Unity Editor, not delegating it to an agent. Steps are still written bite-sized and in order so they can be followed one at a time; "run the tests" steps are replaced with "Play the scene and check X" since this project has no automated test suite.

**Goal:** Replace the `SingletonInstance<T>`/`ReferenceManager<T>` + `.Instance` singleton pattern with VContainer dependency injection everywhere, and retire the reflection-based `Bootstrap.cs` loader in favor of `GameLifetimeScope`.

**Architecture:** Every manager (`AddressableManager`, `PrefabManager`, `FirebaseManager`, `SoundManager`, `TextDataManager`, `InputManager`, `AdmobManager`, `GameManager`) becomes a **plain C# class — no `MonoBehaviour`**, registered with real constructor injection. Managers with async startup work implement `IAsyncStartable` and are registered via `RegisterEntryPoint<T>()`, which VContainer drives automatically. Because entry-point *dispatch* order isn't something this plan relies on, every manager that depends on another manager's readiness explicitly awaits a shared `ManagerInitTracker` — each manager marks its own flag done when its init finishes, and anything that needs another manager's data waits on that manager's flag first. This makes startup ordering explicit and self-documenting instead of implicit in registration order. The two Unity lifecycle messages that still require an actual `MonoBehaviour` (`OnApplicationPause`/`OnApplicationQuit`) are captured by a single dedicated `ApplicationLifecycleBridge` component that forwards them to `GameManager` through a small interface — it is the *only* `MonoBehaviour` left in the manager layer. Gameplay/UI objects (`RoundManager`, `Board`, `Brick`, all UI) stay `MonoBehaviour`s as before (they need a scene/prefab presence) and get managers through method injection; they are spawned through `PrefabManager`, which threads `IObjectResolver` all the way down to `AddressableManager.Instantiate<T>`, using VContainer's `resolver.Instantiate(prefab, parent)` (not raw `Addressables.InstantiateAsync`) so every spawned object is injected *before* its own `Awake()` runs, same as any container-registered object.

**Tech Stack:** Unity, VContainer, UniTask (Cysharp), Addressables.

## Global Constraints

- No automated test suite exists in this project — every "verify" step means: open `Color_Brick.unity` in the Unity Editor, press Play, and check the specified behavior/console output. Do this after every task before moving to the next.
- **Never remove a manager's `SingletonInstance<T>`/`ReferenceManager<T>` inheritance or delete those base-class files until grep confirms zero remaining `TypeName.Instance` references anywhere in `Assets/Scripts`.** Task 10 is the only task that deletes the old base classes, after every consumer has been converted.
- `PlayGamesPlatform.Instance` (`FirebaseManager.cs`) is a third-party SDK singleton — never touch it.
- `EditBrickColor.unity` / `BrickColorEditorManager` and `Test.unity` are out of scope.
- **Manager layer = POCO, gameplay/UI layer = MonoBehaviour.** Managers use real constructor injection (`public ManagerType(Dep dep) { ... }`). `RoundManager`, `Board`, `Brick`, and every UI class stay `MonoBehaviour` and use method injection (`[Inject] public void Construct(...)`) — Unity doesn't support constructor injection on a `MonoBehaviour`.
- `ManagerInitTracker` is the one shared coordination point for "is manager X done initializing yet" — a manager that needs another manager's *data* (not just its object reference) during its own startup must `await _tracker.WaitUntilReady(ManagerType.X)` before touching that data, regardless of what order VContainer happens to invoke entry points in.

---

### Task 1: Delete `Bootstrap.cs`

`GameLifetimeScope` already registers the exact 7 managers + `GameManager` that `Bootstrap.cs` reflects over and spawns. Having both active is a live race today. Removing `Bootstrap.cs` first eliminates that race before any injection wiring is added, and it never becomes relevant again once managers stop being `MonoBehaviour`s at all (there'd be nothing left for its reflection scan to find).

**Files:**
- Delete: `Assets/Scripts/Bootstrap.cs`
- Delete: `Assets/Scripts/Bootstrap.cs.meta`

**Interfaces:** None — this task only removes a file.

- [ ] **Step 1: Confirm nothing else references `Bootstrap`**

Search the project for `Bootstrap` (the class, not `GameManager.Bootstrap()` — an unrelated method replaced in Task 7). If a scene has a GameObject with a `Bootstrap` component attached, remove that component too.

- [ ] **Step 2: Delete the file**

```bash
git rm Assets/Scripts/Bootstrap.cs Assets/Scripts/Bootstrap.cs.meta
```

- [ ] **Step 3: Verify in Editor**

Play `Color_Brick.unity`. Expected: the game still boots — lobby appears, no missing-manager errors. (`GameLifetimeScope` alone creates every manager already.)

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "Remove redundant Bootstrap.cs reflection spawner"
```

---

### Task 2: Shared infrastructure — `ManagerInitTracker` + application-lifecycle bridge

Every later task depends on these two pieces existing first: the readiness tracker every manager reports to, and the bridge that keeps `OnApplicationPause`/`OnApplicationQuit` working once `GameManager` is no longer a `MonoBehaviour`.

**Files:**
- Create: `Assets/Scripts/Core/ManagerInitTracker.cs`
- Create: `Assets/Scripts/Core/Interface/IApplicationLifecycleListener.cs`
- Create: `Assets/Scripts/ApplicationLifecycleBridge.cs`
- Modify: `Assets/Scripts/GameLifetimeScope.cs`
- Modify: `Assets/Scripts/Core/Attribute/ManagerOrderAttribute.cs` (repurposed — see Step 1)

**Interfaces:**
- Produces: `ManagerType` enum, `ManagerInitTracker` (`MarkReady`, `IsReady`, `WaitUntilReady`, `WaitUntilAllReady`) — every manager task below depends on this.
- Produces: `IApplicationLifecycleListener` (`OnApplicationPause(bool)`, `OnApplicationQuit()`) — `GameManager` (Task 7) implements it; `ApplicationLifecycleBridge` forwards Unity messages to whatever implements it.

- [ ] **Step 1: Add a `ManagerType` enum, one entry per manager**

`Assets/Scripts/Core/Attribute/ManagerOrderAttribute.cs` no longer has a job once `Bootstrap.cs` is gone (it only ever fed that reflection scan) — repurpose the file to hold the new enum instead of leaving dead code around, and delete the attribute class itself now (grep already confirms in Task 1 that nothing but `Bootstrap.cs` used the ordering *values*; the class declarations that carry `[ManagerOrder(N)]` get cleaned up per-manager in their own tasks below):

```csharp
// Assets/Scripts/Core/Attribute/ManagerOrderAttribute.cs — replace entire contents with:
public enum ManagerType
{
    Addressable,
    Prefab,
    Firebase,
    Sound,
    TextData,
    Input,
    Admob
}
```

(Consider renaming the file to `ManagerType.cs` to match — if you do, update the `.meta` GUID mapping by renaming rather than delete+recreate, so any existing references keep resolving. Simpler: just leave the filename as-is; the compiler doesn't care that the filename no longer matches the type name.)

- [ ] **Step 2: Create `ManagerInitTracker`**

```csharp
// Assets/Scripts/Core/ManagerInitTracker.cs
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

public class ManagerInitTracker
{
    private readonly HashSet<ManagerType> _ready = new HashSet<ManagerType>();

    public void MarkReady(ManagerType type) => _ready.Add(type);

    public bool IsReady(ManagerType type) => _ready.Contains(type);

    public UniTask WaitUntilReady(ManagerType type)
        => UniTask.WaitUntil(() => IsReady(type));

    public UniTask WaitUntilAllReady(params ManagerType[] types)
        => UniTask.WaitUntil(() => types.All(IsReady));
}
```

- [ ] **Step 3: Create `IApplicationLifecycleListener`**

```csharp
// Assets/Scripts/Core/Interface/IApplicationLifecycleListener.cs
public interface IApplicationLifecycleListener
{
    void OnApplicationPause(bool pause);
    void OnApplicationQuit();
}
```

- [ ] **Step 4: Create `ApplicationLifecycleBridge`**

```csharp
// Assets/Scripts/ApplicationLifecycleBridge.cs
using UnityEngine;
using VContainer;

public class ApplicationLifecycleBridge : MonoBehaviour
{
    private IApplicationLifecycleListener _listener;

    [Inject]
    public void Construct(IApplicationLifecycleListener listener)
    {
        _listener = listener;
    }

    private void OnApplicationPause(bool pause) => _listener.OnApplicationPause(pause);
    private void OnApplicationQuit() => _listener.OnApplicationQuit();
}
```

- [ ] **Step 5: Register both in `GameLifetimeScope`**

`GameLifetimeScope.cs` won't compile clean until Task 7 also converts `GameManager` (nothing implements `IApplicationLifecycleListener` yet) — that's expected and fixed in Task 7. Add these two lines now so the wiring exists:

```csharp
builder.Register<ManagerInitTracker>(Lifetime.Singleton);
builder.RegisterComponentOnNewGameObject<ApplicationLifecycleBridge>(Lifetime.Singleton, nameof(ApplicationLifecycleBridge));
```

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Core/ManagerInitTracker.cs Assets/Scripts/Core/Interface/IApplicationLifecycleListener.cs Assets/Scripts/ApplicationLifecycleBridge.cs Assets/Scripts/Core/Attribute/ManagerOrderAttribute.cs Assets/Scripts/GameLifetimeScope.cs
git commit -m "Add ManagerInitTracker and application-lifecycle bridge for POCO managers"
```

(This task doesn't need its own Editor verification step — nothing consumes these types yet. Task 7's verification is the first real check.)

---

### Task 3: `AddressableManager` → POCO entry point

Leaf manager, converts first. No manager dependencies.

**Files:**
- Modify: `Assets/Scripts/Core/Core_Resource/AddressableManager.cs`

**Interfaces:**
- Consumes: `ManagerInitTracker` (Task 2).
- Produces: `AddressableManager` resolvable as a constructor dependency everywhere else; `ManagerType.Addressable` flag.

- [ ] **Step 1: Convert the class**

```csharp
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer.Unity;

public class AddressableManager : IAsyncStartable
{
    private readonly ManagerInitTracker _tracker;
    private Dictionary<ContainLabel, List<AsyncOperationHandle>> _loadHandles = new Dictionary<ContainLabel, List<AsyncOperationHandle>>();
    private List<GameObject> _instantiateHandles = new List<GameObject>();

    public AddressableManager(ManagerInitTracker tracker)
    {
        _tracker = tracker;
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        await SetAddressable();
        _tracker.MarkReady(ManagerType.Addressable);
    }

    public async UniTask SetAddressable()
    {
        await Addressables.InitializeAsync();
        await LoadRemoteAddressable();
    }

    // LoadRemoteAddressable, UpdateLoadGauage, CompletionAddressableLoad, AssetReleaseForLabel,
    // InstantiateRelease, PreloadAssets, Load<T>, LoadResourceData<T> stay exactly as they are today.

    private void Logging(string log) => LLogger.Log(log, color: Colors.Yellow, skipFrames: 2);
    private void Warning(string log) => LLogger.Log(log, level: LLogger.LogLevel.Warning, skipFrames: 2);
    private void Error(string log) => LLogger.Log(log, level: LLogger.LogLevel.Error, skipFrames: 2);
}
```

Remove `[ManagerOrder(N)]` if present, `: SingletonInstance<AddressableManager>`, and the old `public override void Init() { base.Init(); }` (nothing left to override). The `Logging`/`Warning`/`Error` helpers move in as private methods directly on the class — with no more shared `ManagerBehaviour`/`SingletonInstance<T>` base across every manager, each manager just keeps its own trivial copies (3 one-line methods each isn't worth a shared abstract base once there's no `MonoBehaviour` machinery to justify one).

**Note:** `Instantiate<T>` keeps its current signature for now (`IAssetResource assetResource, Transform parent, bool isProtected`) — Task 4 changes *how* it creates the GameObject, not this task.

- [ ] **Step 2: Update `GameLifetimeScope.cs`**

```csharp
// Before
builder.RegisterComponentOnNewGameObject<AddressableManager>(Lifetime.Singleton, nameof(AddressableManager));
// After
builder.RegisterEntryPoint<AddressableManager>(Lifetime.Singleton).AsSelf();
```

- [ ] **Step 3: Compile and fix errors**

Expect other manager files (`ReferenceManager.cs`, `PrefabManager.cs`, `SoundManager.cs`, `TextDataManager.cs`) to still fail to compile — they still say `: SingletonInstance<AddressableManager>`-style things and call `AddressableManager.Instance`. That's fine; Tasks 4–6 fix them next. If you want a clean compile after every single task, convert Tasks 3–6 as one combined commit instead of stopping here — see the note at the top of Task 4.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Core/Core_Resource/AddressableManager.cs Assets/Scripts/GameLifetimeScope.cs
git commit -m "Convert AddressableManager to a POCO VContainer entry point"
```

---

### Task 4: `ReferenceManager<T>` → `AssetReferenceManager` (non-generic) + `PrefabManager` → POCO

**Note on compile state:** Tasks 3–6 touch a tightly-coupled cluster (`AddressableManager` ← `AssetReferenceManager` ← `PrefabManager`/`SoundManager`/`TextDataManager`, plus `FirebaseManager`, `InputManager`, `AdmobManager`). If keeping the project compiling after every single task matters more to you than small commits, do Tasks 3–7 in one sitting and commit once at the end of Task 7. Otherwise expect red squiggles between now and the end of Task 7 — harmless since nothing is running in Play mode mid-refactor.

`ReferenceManager<T>`'s generic type parameter only ever existed to support `SingletonInstance<T>`'s static `Instance` accessor. With no singleton left, the generic serves no purpose — rename it to a plain, non-generic `AssetReferenceManager`.

**Files:**
- Rename + modify: `Assets/Scripts/Core/ReferenceManager.cs` → `Assets/Scripts/Core/AssetReferenceManager.cs`
- Modify: `Assets/Scripts/Core/Core_Resource/IAssetResource.cs`
- Modify: `Assets/Scripts/Core/Core_Resource/AssetReferenceBase.cs`
- Modify: `Assets/Scripts/Core/Core_Resource/AddressableManager.cs`
- Modify: `Assets/Scripts/Core/PrefabManager.cs`

**Interfaces:**
- Consumes: `AddressableManager` (Task 3), `ManagerInitTracker` (Task 2), `IObjectResolver` (VContainer built-in, no registration needed).
- Produces: `AssetReferenceManager` base constructor `(AddressableManager, IObjectResolver)`; `PrefabManager` resolvable as a constructor dependency; `ManagerType.Prefab` flag. Any object instantiated via `PrefabManager`'s helpers is now injected before its own `Awake()` — Tasks 8/9 rely on this.

- [ ] **Step 1: Rename and convert `ReferenceManager<T>` → `AssetReferenceManager`**

```csharp
// Assets/Scripts/Core/AssetReferenceManager.cs
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using VContainer.Unity;

public abstract class AssetReferenceManager
{
    protected readonly AddressableManager _addressableManager;
    protected readonly VContainer.IObjectResolver _resolver;
    protected Dictionary<int, IAssetResource> _assetMap = new Dictionary<int, IAssetResource>();
    protected IEnumerable<IAssetResource> _assetDatas = new List<IAssetResource>();

    protected AssetReferenceManager(AddressableManager addressableManager, VContainer.IObjectResolver resolver)
    {
        _addressableManager = addressableManager;
        _resolver = resolver;
    }

    public virtual async UniTask LoadAssetReference()
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

    protected async UniTask<TI> InstantiateObject<TI>(int index, UnityEngine.Transform parent = null, bool isProtected = false)
    {
        if (_assetMap.TryGetValue(index, out var obj) == false)
        {
            return default;
        }

        return await _addressableManager.Instantiate<TI>(obj, parent, isProtected, _resolver);
    }

    private void Logging(string log) => LLogger.Log(log, color: Colors.Yellow, skipFrames: 2);
    private void Warning(string log) => LLogger.Log(log, level: LLogger.LogLevel.Warning, skipFrames: 2);
}
```

Note `InstantiateObject` now passes `_resolver` through to `AddressableManager.Instantiate` — wired up in Step 3 below. Delete the old `Assets/Scripts/Core/ReferenceManager.cs` file after creating the new one (or edit it in place and rename via your IDE/`git mv` so history follows):

```bash
git mv Assets/Scripts/Core/ReferenceManager.cs Assets/Scripts/Core/AssetReferenceManager.cs
```

then apply the content above.

- [ ] **Step 2: Switch runtime instantiation to `resolver.Instantiate` in `IAssetResource`/`AssetReferenceBase`**

`Assets/Scripts/Core/Core_Resource/IAssetResource.cs` — change the interface method to take a resolver:

```csharp
// Before
UniTask<T> InstantiateAsync<T>(Transform parent);
// After
UniTask<T> InstantiateAsync<T>(Transform parent, VContainer.IObjectResolver resolver);
```

`Assets/Scripts/Core/Core_Resource/AssetReferenceBase.cs` — reimplement to load-then-inject instead of Addressables' own instantiate:

```csharp
// Before
public async UniTask<T1> InstantiateAsync<T1>(Transform parent)
{
    var handle = data.InstantiateAsync(parent);
    await handle.ToUniTask();
    instance = handle.Result;

    if (typeof(T1) == typeof(GameObject))
        return (T1)(object)instance;
    return instance.GetComponent<T1>();
}

// After
public async UniTask<T1> InstantiateAsync<T1>(Transform parent, VContainer.IObjectResolver resolver)
{
    if (isValid == false)
    {
        var loadHandle = data.LoadAssetAsync();
        await loadHandle.ToUniTask();
    }

    var prefab = data.Asset as GameObject;
    instance = resolver.Instantiate(prefab, parent);

    if (typeof(T1) == typeof(GameObject))
        return (T1)(object)instance;
    return instance.GetComponent<T1>();
}
```

**Also fix `ReleaseAsset()` in the same file** — it currently calls `data.ReleaseInstance(instance)`, which only works for instances Addressables itself instantiated. Since instances are now created via `resolver.Instantiate` (plain `Object.Instantiate` under the hood):

```csharp
// Before
public void ReleaseAsset()
{
    if (isInstance)
        data.ReleaseInstance(instance);
    instance = null;
}

// After
public void ReleaseAsset()
{
    if (isInstance)
        UnityEngine.Object.Destroy(instance);
    instance = null;
}
```

- [ ] **Step 3: Thread the resolver through `AddressableManager.Instantiate`**

`Assets/Scripts/Core/Core_Resource/AddressableManager.cs`:

```csharp
// Before
public async UniTask<T> Instantiate<T>(IAssetResource assetResource, Transform parent, bool isProtected)
{
    var go = await assetResource.InstantiateAsync<GameObject>(parent);
    var obj = go.AddComponent<InstantiateObject>();
    if (isProtected == false)
        _instantiateHandles.Add(go);
    obj.SetAssetReference(assetResource);
    return go.GetComponent<T>();
}

// After
public async UniTask<T> Instantiate<T>(IAssetResource assetResource, Transform parent, bool isProtected, VContainer.IObjectResolver resolver)
{
    var go = await assetResource.InstantiateAsync<GameObject>(parent, resolver);
    var obj = go.AddComponent<InstantiateObject>();
    if (isProtected == false)
        _instantiateHandles.Add(go);
    obj.SetAssetReference(assetResource);
    return go.GetComponent<T>();
}
```

- [ ] **Step 4: Convert `PrefabManager`**

```csharp
// Assets/Scripts/Core/PrefabManager.cs
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class PrefabManager : AssetReferenceManager, IAsyncStartable
{
    private readonly ManagerInitTracker _tracker;
    private ISafeAreaFitter _staticCanvas;
    private ISafeAreaFitter _dynamicCanvas;
    private Camera _mainCamera;
    public RectTransform MainCanvas => _staticCanvas.MyRT;
    public Camera MainCamera => _mainCamera;

    public PrefabManager(AddressableManager addressableManager, IObjectResolver resolver, ManagerInitTracker tracker)
        : base(addressableManager, resolver)
    {
        _tracker = tracker;
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        await _tracker.WaitUntilReady(ManagerType.Addressable);
        await LoadAssetReference();
        await InitLoadObjects();
        _tracker.MarkReady(ManagerType.Prefab);
    }

    public override async UniTask LoadAssetReference()
    {
        var assets = await _addressableManager.LoadResourceData<PrefabAssetReference>(nameof(PrefabAssetReference));
        _assetDatas = assets.assetDatas;
        await base.LoadAssetReference();
    }

    public async UniTask InitLoadObjects()
    {
        _staticCanvas = await InstantiateObject<ISafeAreaFitter>(PrefabData.StaticCanvas, null, true);
        _dynamicCanvas = await InstantiateObject<ISafeAreaFitter>(PrefabData.DynamicCanvas, null, true);
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

    public async UniTask<TI> InstantiateDynamicUI<TI>(PrefabData type, Transform parent = null, bool isProtected = false)
    {
        if (parent == null)
            parent = _dynamicCanvas.Root;

        return await InstantiateUI<TI>(type, parent, isProtected);
    }

    public async UniTask<TI> InstantiateStaticUI<TI>(PrefabData type, Transform parent = null, bool isProtected = false)
    {
        if (parent == null)
            parent = _staticCanvas.Root;

        return await InstantiateUI<TI>(type, parent, isProtected);
    }

    private async UniTask<TI> InstantiateUI<TI>(PrefabData type, Transform parent, bool isProtected)
    {
        if (_assetMap.TryGetValue(EnumConverter.Enum32ToInt(type), out var obj) == false)
        {
            return default;
        }

        if (obj.isInstance)
        {
            return obj.instance.GetComponent<TI>();
        }

        return await _addressableManager.Instantiate<TI>(obj, parent, isProtected, _resolver);
    }
}
```

`this.transform` from the old `InitLoadObjects` (`InstantiateObject<ISafeAreaFitter>(PrefabData.StaticCanvas, this.transform, true)`) becomes `null` above — `PrefabManager` has no transform of its own anymore since it isn't a `Component`. `null` parent just means "instantiate at scene root," which is fine for the static/dynamic UI root canvases (they were only parented under the manager for hierarchy tidiness, not for any functional reason).

Remove `[ManagerOrder(4)]`/`: IManager` from the class declaration (already reflected above).

- [ ] **Step 5: Update `GameLifetimeScope.cs`**

```csharp
// Before
builder.RegisterComponentOnNewGameObject<PrefabManager>(Lifetime.Singleton, nameof(PrefabManager));
// After
builder.RegisterEntryPoint<PrefabManager>(Lifetime.Singleton).AsSelf();
```

- [ ] **Step 6: Compile — expect remaining errors in `SoundManager.cs`/`TextDataManager.cs` until Task 5; that's fine per the note at the top of this task.**

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Core/AssetReferenceManager.cs Assets/Scripts/Core/ReferenceManager.cs Assets/Scripts/Core/Core_Resource/IAssetResource.cs Assets/Scripts/Core/Core_Resource/AssetReferenceBase.cs Assets/Scripts/Core/Core_Resource/AddressableManager.cs Assets/Scripts/Core/PrefabManager.cs Assets/Scripts/GameLifetimeScope.cs
git commit -m "Convert AssetReferenceManager/PrefabManager to POCO, switch instantiation to IObjectResolver"
```

---

### Task 5: `FirebaseManager` → POCO

**Files:**
- Modify: `Assets/Scripts/Core/FirebaseManager.cs`

**Interfaces:**
- Consumes: `PrefabManager` (Task 4), `ManagerInitTracker` (Task 2).
- Produces: `FirebaseManager` resolvable as a constructor dependency; `ManagerType.Firebase` flag.

- [ ] **Step 1: Update the class declaration, add constructor, convert `Init()` to `StartAsync`**

```csharp
using VContainer.Unity;

public class FirebaseManager : IAsyncStartable
{
    private readonly PrefabManager _prefabManager;
    private readonly ManagerInitTracker _tracker;

    public FirebaseManager(PrefabManager prefabManager, ManagerInitTracker tracker)
    {
        _prefabManager = prefabManager;
        _tracker = tracker;
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        InitializeFirebase();
        await UniTask.WaitUntil(() => IsInitialized, cancellationToken: cancellation);
        _tracker.MarkReady(ManagerType.Firebase);
    }
    // ... all fields/properties (IsInitialized, IsUpdate, IsLoadData, UserId, etc.) unchanged
    // ... InitializeFirebase() and every other method body unchanged
```

Remove `[ManagerOrder(1)]` and `: SingletonInstance<FirebaseManager>, IManager`. Delete the old `public override void Init() { base.Init(); InitializeFirebase(); }`.

- [ ] **Step 2: Replace the one internal `.Instance` call site**

Inside `ShowForceUpdatePopupAsync` (originally around line 576):

```csharp
// before
var popup = await PrefabManager.Instance.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI);
// after
var popup = await _prefabManager.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI);
```

- [ ] **Step 3: Leave every `PlayGamesPlatform.Instance` call untouched** — third-party SDK, out of scope.

- [ ] **Step 4: Add `using Cysharp.Threading.Tasks;` and `using System.Threading;` if not already present** (needed for `UniTask`/`CancellationToken` in `StartAsync`).

- [ ] **Step 5: Update `GameLifetimeScope.cs`**

```csharp
// Before
builder.RegisterComponentOnNewGameObject<FirebaseManager>(Lifetime.Singleton, nameof(FirebaseManager));
// After
builder.RegisterEntryPoint<FirebaseManager>(Lifetime.Singleton).AsSelf();
```

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Core/FirebaseManager.cs Assets/Scripts/GameLifetimeScope.cs
git commit -m "Convert FirebaseManager to a POCO VContainer entry point"
```

---

### Task 6: `SoundManager`, `TextDataManager`, `InputManager`, `AdmobManager` → POCO

**Files:**
- Modify: `Assets/Scripts/Core/SoundManager.cs`
- Modify: `Assets/Scripts/Core/Sound/SoundEmitter.cs`
- Modify: `Assets/Scripts/Core/TextDataManager.cs`
- Modify: `Assets/Scripts/Core/InputManager.cs`
- Modify: `Assets/Scripts/Core/AdmobManager.cs`
- Modify: `Assets/Scripts/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `AddressableManager`/`AssetReferenceManager` (Task 4), `FirebaseManager` (Task 5), `ManagerInitTracker` (Task 2).
- Produces: all four managers resolvable as constructor dependencies; `ManagerType.Sound`/`TextData`/`Admob` flags. `InputManager` needs no flag — see Step 3.

- [ ] **Step 1: Convert `SoundManager`**

```csharp
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using VContainer;
using VContainer.Unity;

public class SoundManager : AssetReferenceManager, IAsyncStartable
{
    private const float DEFAULT_SOUND_VOLUM = 0.5f;
    private readonly FirebaseManager _firebaseManager;
    private readonly ManagerInitTracker _tracker;

    private UnityAction<float> _updateSoundVolumEvent = null;
    private UnityAction<float> _updateBGMVolumEvent = null;
    private UnityAction<float> _updateSFXVolumEvent = null;

    private AudioSource _bgmAudio;
    private AudioSource _sfxAudio;

    // SoundVolumPer / BGMVolumPer / SFXVolumPer properties unchanged

    public bool IsBGMOn
    {
        get { return _firebaseManager.IsBGMOn; }
        set
        {
            if (_firebaseManager.IsBGMOn == value) return;
            _firebaseManager.IsBGMOn = value;
            _bgmAudio.mute = !value;
        }
    }

    public bool IsSFXOn
    {
        get { return _firebaseManager.IsSFXOn; }
        set
        {
            if (_firebaseManager.IsSFXOn == value) return;
            _firebaseManager.IsSFXOn = value;
            _sfxAudio.mute = !value;
        }
    }

    public SoundManager(AddressableManager addressableManager, IObjectResolver resolver, FirebaseManager firebaseManager, ManagerInitTracker tracker)
        : base(addressableManager, resolver)
    {
        _firebaseManager = firebaseManager;
        _tracker = tracker;
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        await _tracker.WaitUntilAllReady(ManagerType.Addressable, ManagerType.Firebase);

        CreateBGMAudio();
        CreateSFXAudio();
        await LoadSaveFieldData();
        SoundVolumPer = 0.5f;

        await LoadAssetReference();
        _tracker.MarkReady(ManagerType.Sound);
    }

    private async UniTask LoadSaveFieldData()
    {
        await UniTask.WaitUntil(() => _firebaseManager.IsLoadData);
        _bgmAudio.mute = !_firebaseManager.IsBGMOn;
        _sfxAudio.mute = !_firebaseManager.IsSFXOn;
    }

    public override async UniTask LoadAssetReference()
    {
        var assets = await _addressableManager.LoadResourceData<SoundAssetReference>(nameof(SoundAssetReference));
        _assetDatas = assets.assetDatas;
        await base.LoadAssetReference();
    }

    private void CreateBGMAudio()
    {
        var go = new GameObject("BGM");
        _bgmAudio = go.AddComponent<AudioSource>();
        _bgmAudio.playOnAwake = false;
        _bgmAudio.loop = true;
        _bgmAudio.volume = 1.0f;
        Object.DontDestroyOnLoad(go);
    }

    private void CreateSFXAudio()
    {
        var go = new GameObject("SFX");
        _sfxAudio = go.AddComponent<AudioSource>();
        _sfxAudio.playOnAwake = false;
        _sfxAudio.volume = 1.0f;
        Object.DontDestroyOnLoad(go);
    }

    // UpdateVolum, UpdateBGMVolum, UpdateSFXVolum, PlayBGM, PlaySFX, FadeBGM,
    // SubscribeToSoundHandler, UnsubscribeToSoundHandler stay exactly as they are today.
}
```

`CreateBGMAudio`/`CreateSFXAudio` drop `go.transform.SetParent(this.transform)` (no `transform` on a POCO) and add `Object.DontDestroyOnLoad(go)` instead, so they survive scene loads the same way they effectively did before (parented under a `DontDestroyOnLoad`'d manager GameObject). They'll show up at the root of the hierarchy instead of nested under `SoundManager(Singleton)` — cosmetic only.

Remove `[ManagerOrder(6)]` and `: ReferenceManager<SoundManager>, IManager` → `: AssetReferenceManager, IAsyncStartable`.

- [ ] **Step 2: Update `SoundEmitter.cs`**

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

`SoundEmitter` stays a `MonoBehaviour` (it's a component on sound-playing prefabs), so it still uses `[Inject]` method injection like the rest of the gameplay/UI layer — `SoundManager` being a POCO now doesn't change how *it* gets consumed by `MonoBehaviour`s. `_soundManager` here is injected via whichever path creates the `SoundEmitter` (either `resolver.Instantiate` if it's inside a spawned prefab, or `RegisterComponentInHierarchy` if it's placed directly in the scene — same open question as before; check where it actually lives).

Its old `SoundManager.IsCreatedInstance()` guard in `OnDestroy` is gone — `_soundManager` is simply null if injection never ran, and `?.` covers that.

- [ ] **Step 3: Convert `InputManager`**

`InputManager`'s init (`new PlayerInput(); UseInputHandler = true;`) has no manager dependencies and does no I/O — it can run synchronously in the constructor. No `IAsyncStartable`, no tracker flag needed: by the time anything else's constructor receives an `InputManager` reference, VContainer guarantees the constructor above has already fully run.

```csharp
using System;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

public class InputManager
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

    public InputManager()
    {
        _inputHandler = new PlayerInput();
        UseInputHandler = true;
    }

    // SubscribeToInputHandler / UnsubscribeToInputHandler unchanged
}
```

Remove `[ManagerOrder(3)]` and `: SingletonInstance<InputManager>, IManager`.

- [ ] **Step 4: Convert `TextDataManager`**

```csharp
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using VContainer.Unity;
using static GameTextSO;

public class TextDataManager : IAsyncStartable
{
    private readonly AddressableManager _addressableManager;
    private readonly ManagerInitTracker _tracker;
    private GameTextSO _gameText;
    private Dictionary<int, GameText> _gameTextMap = new Dictionary<int, GameText>();

    public TextDataManager(AddressableManager addressableManager, ManagerInitTracker tracker)
    {
        _addressableManager = addressableManager;
        _tracker = tracker;
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        await _tracker.WaitUntilReady(ManagerType.Addressable);
        _gameText = await _addressableManager.LoadResourceData<GameTextSO>(nameof(GameTextSO));
        AssetReferenceMapping();
        _tracker.MarkReady(ManagerType.TextData);
    }

    private void AssetReferenceMapping()
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

Remove `[ManagerOrder(5)]` and `: SingletonInstance<TextDataManager>, IManager`.

- [ ] **Step 5: Convert `AdmobManager`**

```csharp
#if UNITY_ANDROID || UNITY_EDITOR
using Cysharp.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using System;
using System.Collections.Generic;
using System.Threading;
using VContainer.Unity;

public class AdmobManager : IAsyncStartable, IDisposable
{
    private readonly FirebaseManager _firebaseManager;
    private readonly ManagerInitTracker _tracker;
    public bool IsPrivacyOptionsRequire = ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

    private BannerView bannerView;

    public AdmobManager(FirebaseManager firebaseManager, ManagerInitTracker tracker)
    {
        _firebaseManager = firebaseManager;
        _tracker = tracker;
    }

    public UniTask StartAsync(CancellationToken cancellation)
    {
        Logging("Admob 초기화");
        RequestConsent();
        _tracker.MarkReady(ManagerType.Admob);
        return UniTask.CompletedTask;
    }

    public void Dispose()
    {
        bannerView?.Destroy();
    }

    // InitializeAndLoadAds, CreateBanner, CreateInterstitial, RequestConsent,
    // OnConsentInfoUpdated, OnClickPrivacyOptionsButton, OnConsentFormDismissed stay unchanged,
    // except every `FirebaseManager.Instance` becomes `_firebaseManager`:
    //   CreateBanner():        FirebaseManager.Instance.Log(...) -> _firebaseManager.Log(...)
    //   CreateInterstitial():  FirebaseManager.Instance.Log(...) -> _firebaseManager.Log(...)

    private void Logging(string log) => LLogger.Log(log, color: Colors.Yellow, skipFrames: 2);
}
#endif
```

Remove `[ManagerOrder(2)]` and `: SingletonInstance<AdmobManager>, IManager`. The old `public void OnDestroy()` (a Unity message that only fired because this used to be a `MonoBehaviour`) becomes `Dispose()`, called by VContainer when the container is disposed (scene unload / app quit) since it's registered as an entry point.

- [ ] **Step 6: Update `GameLifetimeScope.cs`**

```csharp
// Before
builder.RegisterComponentOnNewGameObject<SoundManager>(Lifetime.Singleton, nameof(SoundManager));
builder.RegisterComponentOnNewGameObject<TextDataManager>(Lifetime.Singleton, nameof(TextDataManager));
builder.RegisterComponentOnNewGameObject<InputManager>(Lifetime.Singleton, nameof(InputManager));
builder.RegisterComponentOnNewGameObject<AdmobManager>(Lifetime.Singleton, nameof(AdmobManager));

// After
builder.RegisterEntryPoint<SoundManager>(Lifetime.Singleton).AsSelf();
builder.RegisterEntryPoint<TextDataManager>(Lifetime.Singleton).AsSelf();
builder.Register<InputManager>(Lifetime.Singleton);
builder.RegisterEntryPoint<AdmobManager>(Lifetime.Singleton).AsSelf();
```

- [ ] **Step 7: Compile and fix errors**

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Core/SoundManager.cs Assets/Scripts/Core/Sound/SoundEmitter.cs Assets/Scripts/Core/TextDataManager.cs Assets/Scripts/Core/InputManager.cs Assets/Scripts/Core/AdmobManager.cs Assets/Scripts/GameLifetimeScope.cs
git commit -m "Convert SoundManager, TextDataManager, InputManager, AdmobManager to POCO"
```

(No Editor verification step here yet — `GameManager` in Task 7 is what actually drives the boot sequence end to end. Compiling clean is the bar for this task.)

---

### Task 7: `GameManager` → POCO entry point, finish `GameLifetimeScope`

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Modify: `Assets/Scripts/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `FirebaseManager` (Task 5), `PrefabManager` (Task 4), `InputManager` (Task 6), `ManagerInitTracker` (Task 2), `IApplicationLifecycleListener` (Task 2 — implemented here).
- Produces: `GameManager` drives the boot sequence; `ApplicationLifecycleBridge` (Task 2) now has an implementer to resolve.

`GameManager` no longer needs `AddressableManager`, `SoundManager`, or `TextDataManager` directly — it only waits on their tracker flags. It keeps `PrefabManager` (spawns lobby/loading UI, popups) and `InputManager` (subscribes the exit key) as direct dependencies, plus `FirebaseManager` for its own game-state logic (`HighScore`, `IsSymbolOn`, logging).

- [ ] **Step 1: Convert the class**

```csharp
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

public class GameManager : IAsyncStartable, IDisposable, IApplicationLifecycleListener
{
    private readonly FirebaseManager _firebaseManager;
    private readonly PrefabManager _prefabManager;
    private readonly InputManager _inputManager;
    private readonly ManagerInitTracker _tracker;

    public int HighScore
    {
        get => _firebaseManager.ClassicScore;
        set
        {
            if (_firebaseManager.ClassicScore == value)
                return;
            _firebaseManager.ClassicScore = value;
        }
    }

    public bool IsSymbolOn
    {
        get => _firebaseManager.IsSymbolOn;
        set
        {
            if (_firebaseManager.IsSymbolOn == value)
                return;
            _firebaseManager.IsSymbolOn = value;
            _roundManager?.ChangeSymbolState();
        }
    }

    public LanguageType Language = LanguageType.English;
    private IRound _roundManager;
    private IBaseUI _lobbyUI;
    private IBaseUI _loadingUI;

    public float catureEnterTime { get; set; }

    public GameManager(FirebaseManager firebaseManager, PrefabManager prefabManager, InputManager inputManager, ManagerInitTracker tracker)
    {
        _firebaseManager = firebaseManager;
        _prefabManager = prefabManager;
        _inputManager = inputManager;
        _tracker = tracker;
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        ResolutionScreen.InitResolution();
        _inputManager.SubscribeToInputHandler(InputType.Game_Exit, OnClickExit);

        await _tracker.WaitUntilAllReady(
            ManagerType.Addressable, ManagerType.Prefab, ManagerType.Firebase,
            ManagerType.Sound, ManagerType.TextData);

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

    public async UniTask StartRound()
    {
        if (_roundManager == null)
        {
            _roundManager = await _prefabManager.InstantiateObject<IRound>(PrefabData.RoundManager);
            await _roundManager.Init();
        }
        _lobbyUI.Close();
        _roundManager.EnterRound();
    }

    public void ExitRound()
    {
        if (_roundManager == null)
            return;

        _roundManager.ExitRound();
        _roundManager = null;
        _lobbyUI.Init();
    }

    private void OnClickExit(InputAction.CallbackContext callback)
    {
        ShowExitToast().Forget();
    }

    private async UniTask ShowExitToast()
    {
#if UNITY_ANDROID || UNITY_EDITOR
        if (_prefabManager.TryGetInstance<IPopupQuestion>(PrefabData.PopupQuestionUI, out IPopupQuestion popup))
            return;
        popup = await _prefabManager.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI);

        popup.SetNoticeContent(GameTextData.POPUP_EXIT_GAME);
        popup.RegistQuestionAction(QuitGame);
#endif
    }

    private void QuitGame()
    {
        ExitRound();
        Application.Quit();
    }

    public void OnApplicationPause(bool pause)
    {
        if (!pause)
            return;

        PlayerPrefs.Save();

        if (_roundManager != null)
            _firebaseManager.LogModePause("Classic", Time.realtimeSinceStartup - catureEnterTime, _roundManager.CurrentScore);

        _firebaseManager.Log("App paused");
    }

    public void OnApplicationQuit()
    {
        PlayerPrefs.Save();
        _firebaseManager.LogEvent("app_quit", "real_time", Time.realtimeSinceStartup.ToString());
    }

    public void Dispose()
    {
    }
}
```

Dropped entirely: `: SingletonInstance<GameManager>, IManager` → `: IAsyncStartable, IDisposable, IApplicationLifecycleListener`. `OnApplicationPause(bool pause)`/`OnApplicationQuit()` are no longer Unity messages here — they're plain interface methods called by `ApplicationLifecycleBridge` (Task 2).

- [ ] **Step 2: Finish `GameLifetimeScope.cs`**

```csharp
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<ManagerInitTracker>(Lifetime.Singleton);
        builder.Register<InputManager>(Lifetime.Singleton);

        builder.RegisterEntryPoint<AddressableManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<PrefabManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<FirebaseManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<SoundManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<TextDataManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<AdmobManager>(Lifetime.Singleton).AsSelf();

        builder.RegisterEntryPoint<GameManager>(Lifetime.Singleton)
            .AsSelf()
            .As<IApplicationLifecycleListener>();

        builder.RegisterComponentOnNewGameObject<ApplicationLifecycleBridge>(Lifetime.Singleton, nameof(ApplicationLifecycleBridge));
    }
}
```

- [ ] **Step 3: Compile and fix errors**

- [ ] **Step 4: Play `Color_Brick.unity` from a clean stopped state**

This is the first real end-to-end check of the whole POCO manager graph. Confirm: no console errors during boot, every manager's `StartAsync` runs (add a temporary breakpoint or log if you want to watch the tracker flags flip in order), lobby UI appears, force-update check passes through, loading screen closes after ~2s.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/GameManager.cs Assets/Scripts/GameLifetimeScope.cs
git commit -m "Convert GameManager to a POCO entry point driven by ManagerInitTracker"
```

---

### Task 8: `RoundManager` → `Board` → `Brick`

Unaffected by the manager POCO conversion — these stay `MonoBehaviour`s (scene/prefab presence) and use `[Inject]` method injection exactly as they would regardless of whether the managers behind them are POCOs or components. They're spawned via `PrefabManager` → `AddressableManager.Instantiate<T>` → `resolver.Instantiate(prefab, parent)` (Task 4), so injection runs before their own `Awake()`.

**Files:**
- Modify: `Assets/Scripts/Game/RoundManager.cs`
- Modify: `Assets/Scripts/Game/Board.cs`

**Interfaces:**
- Consumes: `PrefabManager`, `GameManager`, `FirebaseManager`, `InputManager`, `SoundManager` (all prior tasks).
- Note: `Brick.cs` has no `.Instance` calls (only a commented-out line) — no changes needed.

- [ ] **Step 1: Update `RoundManager.cs`**

```csharp
using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
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

    public async UniTask Init()
    {
        await LoadRoundObjects();
    }

    private async UniTask LoadRoundObjects()
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

- [ ] **Step 2: Update `Board.cs`**

Add fields + injection near the top of the class:

```csharp
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
git commit -m "Convert RoundManager and Board to VContainer injection"
```

---

### Task 9: UI layer sweep

Same mechanical pattern as Task 8 — every file below stays a `MonoBehaviour`, gains `[Inject] Construct(...)`, replaces `.Instance` with the injected field.

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
- Consumes: `SoundManager`, `PrefabManager`, `FirebaseManager`, `AdmobManager`, `InputManager`, `TextDataManager` (all prior tasks).

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

**Important:** check whether `SoundSettingUI` sits on a GameObject placed directly in `Color_Brick.unity` rather than inside a prefab spawned via `PrefabManager`. If so, add `builder.RegisterComponentInHierarchy<SoundSettingUI>();` to `GameLifetimeScope` — otherwise it never gets injected at all (only objects the container creates or explicitly registers as hierarchy components receive injection; a scene-placed object the container doesn't know about is invisible to it).

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

**Important:** like `SoundSettingUI`, check whether any `TextHandler` instance sits directly in the scene rather than inside a spawned prefab. If so, register it the same way: `builder.RegisterComponentInHierarchy<TextHandler>();`.

- [ ] **Step 11: Add `using VContainer;` to every file touched in this task that doesn't already have it**

- [ ] **Step 12: Compile and fix errors**

- [ ] **Step 13: Play `Color_Brick.unity` end to end** — lobby (BGM, leaderboard button, legal popup), start a round, in-round menu (symbol toggle, sound settings, inquiry form), game over screen (score, combo, retry/home), force-update popup if applicable. Confirm no console errors.

- [ ] **Step 14: Commit**

```bash
git add Assets/Scripts/UI/GameLobbyUI.cs Assets/Scripts/UI/GameOverUI.cs Assets/Scripts/UI/InGameUI.cs Assets/Scripts/UI/MenuUI.cs Assets/Scripts/UI/InquriyUI.cs Assets/Scripts/UI/PopupQuestionUI.cs Assets/Scripts/UI/PopupNoticeUI.cs Assets/Scripts/UI/SoundSettingUI.cs Assets/Scripts/UI/IngameScoreUI.cs Assets/Scripts/UI/TextHandler.cs
git commit -m "Convert UI layer to VContainer injection"
```

---

### Task 10: Final cleanup — delete dead singleton code, full regression pass

**Files:**
- Delete: `Assets/Scripts/Share/SingletonInstance.cs` (and `.meta`)
- Delete: `Assets/Scripts/Core/Interface/IManager.cs` (and `.meta`)
- Delete: `Assets/Scripts/Core/ReferenceManager.cs.meta` if it's still lingering after Task 4's rename

**Interfaces:**
- Consumes: every prior task must already have removed `IManager`/`[ManagerOrder(N)]`/`SingletonInstance<T>`/`ReferenceManager<T>` references from its own files.

- [ ] **Step 1: Confirm zero remaining references before deleting**

```bash
grep -rn "IManager\|SingletonInstance\|ReferenceManager<" Assets/Scripts --include=*.cs
```

Expected: no matches. If anything shows up, finish converting that file before proceeding.

- [ ] **Step 2: Delete `SingletonInstance.cs` and `IManager.cs`**

- [ ] **Step 3: Compile and fix errors**

- [ ] **Step 4: Full manual verification — walk the whole golden path**

Open `Color_Brick.unity` fresh (not already in Play mode), enter Play mode. Confirm the Console shows no errors during boot. Then: lobby → start a Classic round → play a few moves (touch/drag input, brick matching, combo, score) → trigger game over (clear the board or let it fill) → game over screen (score/combo display, high score confetti + interstitial ad path if applicable) → retry → home → exit round → background the app (pause) → quit.

- [ ] **Step 5: If anything regressed, `git bisect` across this branch's commits (Tasks 1–9) to isolate which task introduced it, then fix forward with a new commit — do not amend earlier commits.**

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Remove dead SingletonInstance/IManager code after full VContainer migration"
```
