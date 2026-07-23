# VContainer Full Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Note for this project:** the user is implementing this plan by hand in the Unity Editor, not delegating it to an agent. Steps are still written bite-sized and in order so they can be followed one at a time; "run the tests" steps are replaced with "Play the scene and check X" since this project has no automated test suite.

**Goal:** Replace the `SingletonInstance<T>`/`ReferenceManager<T>` + `.Instance` singleton pattern with VContainer dependency injection everywhere, and retire the reflection-based `Bootstrap.cs` loader in favor of `GameLifetimeScope`.

**Architecture:** Convert managers bottom-up by dependency (leaf managers first), but **keep each manager's own `SingletonInstance<T>` base in place until every external consumer of that manager's `.Instance` has been converted.** This is the key correctness rule of this plan: a manager's `.Instance` accessor cannot be removed until nothing in the whole codebase still calls it — so each task adds `[Inject]` wiring and fixes whichever call sites it can, without yet deleting the fallback that keeps not-yet-converted files compiling. Only the final task (Task 10) strips every singleton base at once, after a project-wide grep confirms zero remaining `.Instance` references. `PrefabManager` gains an `IObjectResolver` so every runtime-instantiated prefab (UI popups, `RoundManager`, `Board`, `Brick`) is auto-injected on spawn.

**Tech Stack:** Unity, VContainer, UniTask (Cysharp), Addressables.

## Global Constraints

- No automated test suite exists in this project — every "verify" step means: open `Color_Brick.unity` in the Unity Editor, press Play, and check the specified behavior/console output. Do this after every task before moving to the next.
- **Never remove a manager's `SingletonInstance<T>`/`ReferenceManager<T>` inheritance until grep confirms zero remaining `TypeName.Instance` references to it anywhere in `Assets/Scripts`.** Task 10 is the only task that does this, for every manager at once. Every earlier task must leave the project compiling — if a task's own file still needs `.Instance` for a manager that isn't fully converted yet on the consumer side, that's fine; the point is other files must never lose access to `.Instance` before they're individually updated.
- `PlayGamesPlatform.Instance` (`FirebaseManager.cs`) is a third-party SDK singleton — never touch it.
- `EditBrickColor.unity` / `BrickColorEditorManager` and `Test.unity` are out of scope.
- Injection pattern for MonoBehaviours: a public method tagged `[Inject]` (from `VContainer`), receiving dependencies as parameters and assigning them to private fields. Never constructor-inject a MonoBehaviour (Unity doesn't support it). VContainer calls every `[Inject]`-tagged method it finds across a type's whole inheritance chain, so a base class and a derived class can each have their own without conflict — just give them different method names if both classes declare one (e.g. base `Construct`, derived `ConstructFirebase`).

---

### Task 1: Delete `Bootstrap.cs`

`GameLifetimeScope` already registers the exact 7 managers + `GameManager` that `Bootstrap.cs` reflects over and spawns. Having both active is a live race: whichever `Awake()` runs first "wins" the singleton slot (`SingletonInstance<T>.Init()` destroys the loser), so it's possible for the VContainer-registered instance to be the one destroyed, leaving the container holding a dead reference while `.Instance` still resolves to the Bootstrap-spawned twin. Removing `Bootstrap.cs` first eliminates this race before any injection wiring is added.

**Files:**
- Delete: `Assets/Scripts/Bootstrap.cs`
- Delete: `Assets/Scripts/Bootstrap.cs.meta`

**Interfaces:** None — this task only removes a file.

- [ ] **Step 1: Confirm nothing else references `Bootstrap`**

Search the project for `Bootstrap` (the class, not `GameManager.Bootstrap()` — an unrelated method that gets replaced in Task 6, not here). If a scene has a GameObject with a `Bootstrap` component attached, remove that component from the scene too.

- [ ] **Step 2: Delete the file**

```bash
git rm Assets/Scripts/Bootstrap.cs Assets/Scripts/Bootstrap.cs.meta
```

- [ ] **Step 3: Verify in Editor**

Play `Color_Brick.unity`. Expected: the game still boots — lobby appears, no missing-manager errors in the console. (`GameLifetimeScope` alone now creates every manager.)

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "Remove redundant Bootstrap.cs reflection spawner"
```

---

### Task 2: `AddressableManager` → DI (+ its Core consumers)

`AddressableManager` is a leaf (nothing else has to happen first). Its consumers are all inside `Core/`: `ReferenceManager<T>` (base for `PrefabManager`/`SoundManager`), `PrefabManager`, `SoundManager`, `TextDataManager`. Converting all four together in one task means `AddressableManager`'s singleton base can be dropped immediately — nothing outside this task's files calls `AddressableManager.Instance`.

**Files:**
- Modify: `Assets/Scripts/Core/Core_Resource/AddressableManager.cs`
- Modify: `Assets/Scripts/Core/ReferenceManager.cs`
- Modify: `Assets/Scripts/Core/PrefabManager.cs`
- Modify: `Assets/Scripts/Core/SoundManager.cs`
- Modify: `Assets/Scripts/Core/TextDataManager.cs`

**Interfaces:**
- Produces: `ReferenceManager<T>` exposes `protected AddressableManager _addressableManager` to derived classes (`PrefabManager`, `SoundManager`).

- [ ] **Step 1: Drop `AddressableManager`'s singleton base**

`Assets/Scripts/Core/Core_Resource/AddressableManager.cs:8` — this is safe to do now because grep confirms every consumer is fixed within this same task:

```csharp
// Before
public class AddressableManager : SingletonInstance<AddressableManager>

// After
public class AddressableManager : MonoBehaviour
```

Delete the now-pointless override at lines 13-16 (there's no base `Init()` to call anymore):
```csharp
public override void Init()
{
    base.Init();
}
```

- [ ] **Step 2: Wire `ReferenceManager<T>` to receive `AddressableManager` via injection**

`Assets/Scripts/Core/ReferenceManager.cs` — add a field and an `[Inject]` method, and replace the 3 internal `AddressableManager.Instance` calls:

```csharp
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer;

public class ReferenceManager<T> : SingletonInstance<T>
    where T : MonoBehaviour
{
    protected AddressableManager _addressableManager;

    [Inject]
    public void Construct(AddressableManager addressableManager)
    {
        _addressableManager = addressableManager;
    }

    protected Dictionary<int, IAssetResource> _assetMap = new Dictionary<int, IAssetResource>();
    protected IEnumerable<IAssetResource> _assetDatas = new List<IAssetResource>();
    public override void Init()
    {
        base.Init();
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

Note: `SingletonInstance<T>` base stays for now — `PrefabManager.Instance`/`SoundManager.Instance` are still used by dozens of files not touched until later tasks. Only `AddressableManager`'s own base is dropped in this task.

- [ ] **Step 3: Fix `PrefabManager`'s own `AddressableManager.Instance` call**

`Assets/Scripts/Core/PrefabManager.cs:20` (inside `LoadAssetReference`):

```csharp
// Before
var assets = await AddressableManager.Instance.LoadResourceData<PrefabAssetReference>(nameof(PrefabAssetReference));

// After
var assets = await _addressableManager.LoadResourceData<PrefabAssetReference>(nameof(PrefabAssetReference));
```

`Assets/Scripts/Core/PrefabManager.cs:85` (inside `InstantiateUI`):

```csharp
// Before
return await AddressableManager.Instance.Instantiate<TI>(obj, parent, isProtected);

// After
return await _addressableManager.Instantiate<TI>(obj, parent, isProtected);
```

(`_addressableManager` is the inherited protected field from `ReferenceManager<T>` — no new field needed in `PrefabManager` itself.)

- [ ] **Step 4: Fix `SoundManager`'s own `AddressableManager.Instance` call**

`Assets/Scripts/Core/SoundManager.cs:109` (inside `LoadAssetReference`):

```csharp
// Before
var assets = await AddressableManager.Instance.LoadResourceData<SoundAssetReference>(nameof(SoundAssetReference));

// After
var assets = await _addressableManager.LoadResourceData<SoundAssetReference>(nameof(SoundAssetReference));
```

- [ ] **Step 5: Wire `TextDataManager`** (doesn't extend `ReferenceManager<T>`, needs its own injection)

`Assets/Scripts/Core/TextDataManager.cs`:

```csharp
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using static GameTextSO;

[ManagerOrder(5)]
public class TextDataManager : SingletonInstance<TextDataManager>, IManager
{
    private GameTextSO _gameText;
    private AddressableManager _addressableManager;

    protected Dictionary<int, GameText> _gameTextMap = new Dictionary<int, GameText>();

    [Inject]
    public void Construct(AddressableManager addressableManager)
    {
        _addressableManager = addressableManager;
    }

    public override void Init()
    {
        base.Init();
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

- [ ] **Step 6: Grep-confirm no other file still references `AddressableManager.Instance`**

```bash
grep -rn "AddressableManager.Instance" Assets/Scripts
```

Expected: no output. This is what makes dropping the singleton base in Step 1 safe.

- [ ] **Step 7: Verify in Editor**

Play `Color_Brick.unity`. Expected: game boots exactly as before (this task changes *how* managers get `AddressableManager`, not any behavior). Watch console for `NullReferenceException` on `_addressableManager` — if you see one, double-check the `[Inject]` method is `public` and tagged `[Inject]`, and `VContainer` is imported.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Core
git commit -m "Convert AddressableManager and its consumers to VContainer injection"
```

---

### Task 3: `PrefabManager` runtime instantiation → `IObjectResolver`

This is the task that makes injection automatic for everything spawned at runtime. `PrefabManager`'s *public API stays identical* — every caller of `PrefabManager.Instance.InstantiateObject/InstantiateStaticUI/InstantiateDynamicUI` keeps working unchanged; only the internal mechanism switches from Addressables' own address-based instantiate to load-then-`resolver.Instantiate`. `PrefabManager`'s own singleton base stays — its consumers aren't converted until Tasks 4-8.

**Files:**
- Modify: `Assets/Scripts/Core/Core_Resource/IAssetResource.cs`
- Modify: `Assets/Scripts/Core/Core_Resource/AssetReferenceBase.cs`
- Modify: `Assets/Scripts/Core/Core_Resource/AddressableManager.cs`
- Modify: `Assets/Scripts/Core/ReferenceManager.cs`
- Modify: `Assets/Scripts/Core/PrefabManager.cs`

**Interfaces:**
- Consumes: `IObjectResolver` (from `VContainer`, auto-available — no explicit registration needed).
- Produces: any object instantiated via `PrefabManager`'s helpers is now injected automatically; downstream tasks (7, 8, 9) rely on this.

- [ ] **Step 1: Change `IAssetResource.InstantiateAsync` to take a resolver**

`Assets/Scripts/Core/Core_Resource/IAssetResource.cs:15`

```csharp
// Before
public UniTask<T> InstantiateAsync<T>(Transform parent);

// After
public UniTask<T> InstantiateAsync<T>(Transform parent, IObjectResolver resolver);
```

Add `using VContainer;` to the top of the file.

- [ ] **Step 2: Reimplement `AssetResource.InstantiateAsync` to load-then-inject instead of Addressables' address-instantiate**

`Assets/Scripts/Core/Core_Resource/AssetReferenceBase.cs:28-37`

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
public async UniTask<T1> InstantiateAsync<T1>(Transform parent, IObjectResolver resolver)
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

Add `using VContainer;` to the top of the file.

**Also fix `ReleaseAsset()` in the same file** — it currently calls `data.ReleaseInstance(instance)`, which only releases instances created via Addressables' own `InstantiateAsync`. Since instances are now created via `resolver.Instantiate` (plain `Object.Instantiate` under the hood), release them the same way:

```csharp
// Before
public void ReleaseAsset()
{
    if(isInstance)
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

`Assets/Scripts/Core/Core_Resource/AddressableManager.cs:162-174`

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
public async UniTask<T> Instantiate<T>(IAssetResource assetResource, Transform parent, bool isProtected, IObjectResolver resolver)
{
    var go = await assetResource.InstantiateAsync<GameObject>(parent, resolver);
    var obj = go.AddComponent<InstantiateObject>();
    if (isProtected == false)
        _instantiateHandles.Add(go);

    obj.SetAssetReference(assetResource);

    return go.GetComponent<T>();
}
```

Add `using VContainer;` to the top of the file.

- [ ] **Step 4: Inject `IObjectResolver` into `ReferenceManager<T>` and pass it through `InstantiateObject`**

`Assets/Scripts/Core/ReferenceManager.cs` — extend the `Construct` method added in Task 2:

```csharp
// Before (from Task 2)
protected AddressableManager _addressableManager;

[Inject]
public void Construct(AddressableManager addressableManager)
{
    _addressableManager = addressableManager;
}

// After
protected AddressableManager _addressableManager;
protected IObjectResolver _resolver;

[Inject]
public void Construct(AddressableManager addressableManager, IObjectResolver resolver)
{
    _addressableManager = addressableManager;
    _resolver = resolver;
}
```

And update the call site at the bottom of `InstantiateObject<TI>`:

```csharp
// Before
return await _addressableManager.Instantiate<TI>(obj, parent, isProtected);

// After
return await _addressableManager.Instantiate<TI>(obj, parent, isProtected, _resolver);
```

- [ ] **Step 5: Fix `PrefabManager.InstantiateUI`'s direct call**

`Assets/Scripts/Core/PrefabManager.cs:85`

```csharp
// Before
return await _addressableManager.Instantiate<TI>(obj, parent, isProtected);

// After
return await _addressableManager.Instantiate<TI>(obj, parent, isProtected, _resolver);
```

(Both `_addressableManager` and `_resolver` are inherited protected fields from `ReferenceManager<T>` — no new fields needed in `PrefabManager`.)

- [ ] **Step 6: Verify in Editor**

Play `Color_Brick.unity`. Expected: lobby loads, `LegalUI` popup and every other Addressable-spawned prefab still instantiate correctly, no console errors about null prefabs or failed casts. This is the highest-risk task so far — if a UI popup fails to appear or throws, check that `data.Asset` isn't null (i.e., `LoadAssetAsync` actually completed before `resolver.Instantiate` runs).

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Core
git commit -m "Switch PrefabManager runtime instantiation to IObjectResolver"
```

---

### Task 4: `FirebaseManager` → inject `PrefabManager`

`FirebaseManager` only depends on one other manager: `PrefabManager`, used once in `ShowForceUpdatePopupAsync` to spawn the force-update popup. `FirebaseManager`'s own singleton base stays — it's still consumed via `.Instance` all over the codebase (`SoundManager`, `AdmobManager`, `GameManager`, `RoundManager`, `GameLobbyUI`), cleaned up in later tasks. This task only fixes `FirebaseManager`'s *own* internal dependency.

**Files:**
- Modify: `Assets/Scripts/Core/FirebaseManager.cs`

**Interfaces:**
- Consumes: `PrefabManager` (registered in `GameLifetimeScope` already).

- [ ] **Step 1: Add the injection**

Near the top of the `FirebaseManager` class body (after the existing field declarations, e.g. after `private FirebaseFirestore _firestore;`):

```csharp
private PrefabManager _prefabManager;

[Inject]
public void Construct(PrefabManager prefabManager)
{
    _prefabManager = prefabManager;
}
```

Add `using VContainer;` to the top of the file.

- [ ] **Step 2: Replace the one call site**

`Assets/Scripts/Core/FirebaseManager.cs:576` (inside `ShowForceUpdatePopupAsync`):

```csharp
// Before
var popup = await PrefabManager.Instance.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI);

// After
var popup = await _prefabManager.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI);
```

- [ ] **Step 3: Verify in Editor**

Play `Color_Brick.unity`. Force-update popups are hard to trigger on demand — instead confirm the game still boots normally and no `NullReferenceException` appears around Firebase initialization in the console (a broken injection would throw as soon as `Construct` runs, at container build time, not only when the popup path executes).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Core/FirebaseManager.cs
git commit -m "Inject PrefabManager into FirebaseManager"
```

---

### Task 5: `SoundManager`, `TextDataManager`, `InputManager`, `AdmobManager` → inject their own manager deps

`InputManager` has no manager dependencies (leaf) — nothing to change internally, it's included here just to confirm via the same verification pass. `TextDataManager` already got its `AddressableManager` injection in Task 2. `SoundManager` needs `FirebaseManager` (it already got `AddressableManager` in Task 2). `AdmobManager` needs `FirebaseManager`. All four managers keep their own singleton bases for now — their consumers aren't converted until Tasks 6-8.

**Files:**
- Modify: `Assets/Scripts/Core/SoundManager.cs`
- Modify: `Assets/Scripts/Core/AdmobManager.cs`

**Interfaces:**
- Consumes: `FirebaseManager` (registered in `GameLifetimeScope` already).

- [ ] **Step 1: Wire `SoundManager` to receive `FirebaseManager`**

`Assets/Scripts/Core/SoundManager.cs` — extend the class with a new field/method, and add `using VContainer;`:

```csharp
private FirebaseManager _firebaseManager;

[Inject]
public void ConstructFirebase(FirebaseManager firebaseManager)
{
    _firebaseManager = firebaseManager;
}
```

(Named `ConstructFirebase` rather than `Construct` to avoid confusion with the base class's own `Construct(AddressableManager, IObjectResolver)` from `ReferenceManager<T>` — VContainer calls every `[Inject]`-tagged method found across the type's inheritance chain, so both run regardless of name, but distinct names keep it readable.)

- [ ] **Step 2: Replace `SoundManager`'s 6 internal `FirebaseManager.Instance` uses**

`Assets/Scripts/Core/SoundManager.cs:63,66,69,76,79,82,102,103,104` — the `IsBGMOn`/`IsSFXOn` properties and `LoadSaveFieldData`:

```csharp
// Before
public bool IsBGMOn
{
    get { return FirebaseManager.Instance.IsBGMOn; }
    set
    {
        if (FirebaseManager.Instance.IsBGMOn == value)
            return;

        FirebaseManager.Instance.IsBGMOn = value;
        _bgmAudio.mute = !value;
    }
}

public bool IsSFXOn
{
    get { return FirebaseManager.Instance.IsSFXOn; }
    set
    {
        if (FirebaseManager.Instance.IsSFXOn == value)
            return;

        FirebaseManager.Instance.IsSFXOn = value;
        _sfxAudio.mute = !value;
    }
}

// After
public bool IsBGMOn
{
    get { return _firebaseManager.IsBGMOn; }
    set
    {
        if (_firebaseManager.IsBGMOn == value)
            return;

        _firebaseManager.IsBGMOn = value;
        _bgmAudio.mute = !value;
    }
}

public bool IsSFXOn
{
    get { return _firebaseManager.IsSFXOn; }
    set
    {
        if (_firebaseManager.IsSFXOn == value)
            return;

        _firebaseManager.IsSFXOn = value;
        _sfxAudio.mute = !value;
    }
}
```

```csharp
// Before
async private UniTask LoadSaveFieldData()
{
    await UniTask.WaitUntil(() => FirebaseManager.Instance.IsLoadData);
    _bgmAudio.mute = !FirebaseManager.Instance.IsBGMOn;
    _sfxAudio.mute = !FirebaseManager.Instance.IsSFXOn;
}

// After
async private UniTask LoadSaveFieldData()
{
    await UniTask.WaitUntil(() => _firebaseManager.IsLoadData);
    _bgmAudio.mute = !_firebaseManager.IsBGMOn;
    _sfxAudio.mute = !_firebaseManager.IsSFXOn;
}
```

- [ ] **Step 3: Wire `AdmobManager` to receive `FirebaseManager`**

`Assets/Scripts/Core/AdmobManager.cs` — add near the top of the class, plus `using VContainer;`:

```csharp
private FirebaseManager _firebaseManager;

[Inject]
public void Construct(FirebaseManager firebaseManager)
{
    _firebaseManager = firebaseManager;
}
```

- [ ] **Step 4: Replace `AdmobManager`'s 2 internal `FirebaseManager.Instance` uses**

`Assets/Scripts/Core/AdmobManager.cs:32,48`

```csharp
// Before (CreateBanner)
FirebaseManager.Instance.Log("Bottom Admob Banner Create");
// After
_firebaseManager.Log("Bottom Admob Banner Create");
```

```csharp
// Before (CreateInterstitial)
FirebaseManager.Instance.Log("Update High Score Admob Create");
// After
_firebaseManager.Log("Update High Score Admob Create");
```

- [ ] **Step 5: Verify in Editor**

Play `Color_Brick.unity`. Toggle BGM/SFX in the sound settings UI (still using `SoundManager.Instance` at this point — that's fine, it's unaffected by this task) and confirm audio mute state still updates correctly. Check console for injection errors on boot.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Core/SoundManager.cs Assets/Scripts/Core/AdmobManager.cs
git commit -m "Inject FirebaseManager into SoundManager and AdmobManager"
```

---

### Task 6: `GameManager` → full injection + fix broken entry-point registration

`GameManager` currently has a **broken registration**: `GameLifetimeScope.cs` uses `builder.RegisterEntryPoint<GameManager>(Lifetime.Singleton).AsSelf();`, but `RegisterEntryPoint` is meant for plain C# classes — it cannot correctly construct a `MonoBehaviour`-derived type like `GameManager` (Unity doesn't allow `new MonoBehaviourSubclass()`). This task fixes that registration, converts `GameManager` to receive all 7 managers via injection, and replaces the manual `Bootstrap()` call chain with a real `IAsyncStartable`/`IDisposable` implementation (the stub methods already exist — `Dispose()` and `StartAsync()` just throw `NotImplementedException` today). `GameManager`'s own singleton base stays for now — `RoundManager` still reads `GameManager.Instance` until Task 7.

**Files:**
- Modify: `Assets/Scripts/GameLifetimeScope.cs`
- Modify: `Assets/Scripts/Core/GameManager.cs`

**Interfaces:**
- Consumes: `AddressableManager`, `PrefabManager`, `SoundManager`, `TextDataManager`, `InputManager`, `FirebaseManager`, `AdmobManager` (all registered in `GameLifetimeScope`).
- Produces: `GameManager` now boots itself via VContainer's `IAsyncStartable.StartAsync`, replacing the old manual `Bootstrap()` entry point.

- [ ] **Step 1: Fix the registration**

`Assets/Scripts/GameLifetimeScope.cs:16`

```csharp
// Before
builder.RegisterEntryPoint<GameManager>(Lifetime.Singleton).AsSelf();

// After
builder.RegisterComponentOnNewGameObject<GameManager>(Lifetime.Singleton, nameof(GameManager)).AsSelf().AsImplementedInterfaces();
```

- [ ] **Step 2: Rewrite `GameManager.cs`**

```csharp
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

public class GameManager : SingletonInstance<GameManager>, IManager, IAsyncStartable, IDisposable
{
    private AddressableManager _addressableManager;
    private PrefabManager _prefabManager;
    private SoundManager _soundManager;
    private TextDataManager _textDataManager;
    private InputManager _inputManager;
    private FirebaseManager _firebaseManager;
    private AdmobManager _admobManager;

    [Inject]
    public void Construct(
        AddressableManager addressableManager,
        PrefabManager prefabManager,
        SoundManager soundManager,
        TextDataManager textDataManager,
        InputManager inputManager,
        FirebaseManager firebaseManager,
        AdmobManager admobManager)
    {
        _addressableManager = addressableManager;
        _prefabManager = prefabManager;
        _soundManager = soundManager;
        _textDataManager = textDataManager;
        _inputManager = inputManager;
        _firebaseManager = firebaseManager;
        _admobManager = admobManager;
    }

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

    async public UniTask StartRound()
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

    async private UniTask ShowExitToast()
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

    private void OnApplicationPause(bool pause)
    {
        if (!pause)
            return;

        PlayerPrefs.Save();

        if (_roundManager != null)
            _firebaseManager.LogModePause("Classic", Time.realtimeSinceStartup - catureEnterTime, _roundManager.CurrentScore);

        _firebaseManager.Log("App paused");
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
        _firebaseManager.LogEvent("app_quit", "real_time", Time.realtimeSinceStartup.ToString());
    }

    public void Dispose()
    {
        _inputManager.UnsubscribeToInputHandler(InputType.Game_Exit, OnClickExit);
    }
}
```

Notes on the rewrite:
- `StartAsync` replaces the old `Bootstrap()` method one-for-one (same body, `.Instance` calls swapped for injected fields) — VContainer calls `StartAsync` automatically once the container finishes building, so nothing needs to call it manually anymore.
- `Dispose()` previously threw `NotImplementedException`; it now does the one thing that's safe and obviously correct to undo — unsubscribing the input handler that `StartAsync` subscribed. Don't invent additional cleanup here; the point is to stop throwing, not to guess at unrelated teardown.
- `_admobManager` is stored but unused directly in `GameManager` today (nothing in the original code called it there either) — keep the field; `GameOverUI` still calls `AdmobManager.Instance` until Task 8.

- [ ] **Step 3: Verify in Editor**

Play `Color_Brick.unity`. This is the biggest verification checkpoint so far:
- Game boots without console errors.
- Lobby UI appears, loading UI closes after ~2s once Firebase data loads.
- Press whatever triggers `Game_Exit` input — exit popup should appear (existing `PrefabManager.Instance` calls inside `RoundManager`/UI still work, unaffected by this task).
- Stop Play mode — confirm no exception is thrown from `Dispose()`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/GameLifetimeScope.cs Assets/Scripts/Core/GameManager.cs
git commit -m "Fix GameManager VContainer registration and convert to injection"
```

---

### Task 7: `RoundManager` → `Board` → `Brick`

**Files:**
- Modify: `Assets/Scripts/Game/RoundManager.cs`
- Modify: `Assets/Scripts/Game/Board.cs`

`Brick.cs` needs no changes — its only manager reference is a commented-out line (`//_symbol.gameObject.SetActive(GameManager.Instance.IsSymbolOn);`), not live code.

**Interfaces:**
- Consumes: `PrefabManager`, `FirebaseManager`, `GameManager`, `InputManager`, `SoundManager` — available automatically since both classes are only ever created via `PrefabManager`'s `InstantiateObject`/`InstantiateStaticUI`/`InstantiateDynamicUI`, which now go through `IObjectResolver.Instantiate` (Task 3).

- [ ] **Step 1: Wire `RoundManager`**

`Assets/Scripts/Game/RoundManager.cs`:

```csharp
using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using VContainer;

public class RoundManager : MonoBehaviour, IRound
{
    private PrefabManager _prefabManager;
    private FirebaseManager _firebaseManager;
    private GameManager _gameManager;

    [Inject]
    public void Construct(PrefabManager prefabManager, FirebaseManager firebaseManager, GameManager gameManager)
    {
        _prefabManager = prefabManager;
        _firebaseManager = firebaseManager;
        _gameManager = gameManager;
    }

    public int CurrentScore => _scoreValue;
    public event Action OnUpdateSymbolState;
    private const float COMBO_DELAY = 5f;
    private RoundObject _board;
    private IScore _ingameUI;
    private IScore _gameOver;

    private int _scoreValue = 0;
    private int _comboValue = 0;
    private int _maxCombo = 0;
    private TimerModule _timer;

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

- [ ] **Step 2: Wire `Board`**

`Assets/Scripts/Game/Board.cs` — add near the top of the class (right after the existing field declarations, before `_boardDirection`):

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

Add `using VContainer;` to the top of the file.

Then replace every `.Instance` call site in the same file:

```csharp
// Awake() — before
InputManager.Instance.SubscribeToInputHandler(InputType.Player_Touch, OnTouchPoint, cancel: OnEndTouchPoint);
InputManager.Instance.SubscribeToInputHandler(InputType.Player_Point, perform: OnDragPoint);
// after
_inputManager.SubscribeToInputHandler(InputType.Player_Touch, OnTouchPoint, cancel: OnEndTouchPoint);
_inputManager.SubscribeToInputHandler(InputType.Player_Point, perform: OnDragPoint);
```

```csharp
// OnDestroy() — before
InputManager.Instance.UnsubscribeToInputHandler(InputType.Player_Touch, OnTouchPoint, cancel: OnEndTouchPoint);
InputManager.Instance.UnsubscribeToInputHandler(InputType.Player_Point, perform: OnDragPoint);
// after
_inputManager.UnsubscribeToInputHandler(InputType.Player_Touch, OnTouchPoint, cancel: OnEndTouchPoint);
_inputManager.UnsubscribeToInputHandler(InputType.Player_Point, perform: OnDragPoint);
```

```csharp
// InitBrick() — before
var brick = await PrefabManager.Instance.InstantiateObject<Brick>(PrefabData.Brick, this.transform);
// after
var brick = await _prefabManager.InstantiateObject<Brick>(PrefabData.Brick, this.transform);
```

```csharp
// SlideBrick() — before
await SoundManager.Instance.PlaySFX(SoundData.Slide);
// after
await _soundManager.PlaySFX(SoundData.Slide);
```

```csharp
// DestroyMatches() — before
await SoundManager.Instance.PlaySFX(SoundData.Match);
// after
await _soundManager.PlaySFX(SoundData.Match);
```

- [ ] **Step 3: Verify in Editor**

Play `Color_Brick.unity`, start a round. Confirm: bricks spawn, touch/drag input still slides the board, slide/match SFX play, combo and score update, ending the round shows the game-over screen correctly. This is the first real end-to-end proof that `resolver.Instantiate` injection works on a runtime-spawned object graph (`RoundManager` spawning `Board` spawning `Brick`).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Game/RoundManager.cs Assets/Scripts/Game/Board.cs
git commit -m "Convert RoundManager and Board to VContainer injection"
```

---

### Task 8: UI layer sweep

10 files, same pattern each time: add a `[Inject] Construct(...)` method for whatever managers the file uses, then replace the `.Instance` call sites. All of these are spawned via `PrefabManager` (Task 3), so injection is automatic once the field/method exists. After this task, every consumer of every manager's `.Instance` has been converted — Task 9 becomes the "prove it" checkpoint before Task 10 strips the singleton bases.

**Files:**
- Modify: `Assets/Scripts/UI/GameLobbyUI.cs`
- Modify: `Assets/Scripts/UI/GameOverUI.cs`
- Modify: `Assets/Scripts/UI/InGameUI.cs`
- Modify: `Assets/Scripts/UI/InquriyUI.cs`
- Modify: `Assets/Scripts/UI/MenuUI.cs`
- Modify: `Assets/Scripts/UI/PopupNoticeUI.cs`
- Modify: `Assets/Scripts/UI/PopupQuestionUI.cs`
- Modify: `Assets/Scripts/UI/SoundSettingUI.cs`
- Modify: `Assets/Scripts/UI/IngameScoreUI.cs`
- Modify: `Assets/Scripts/UI/TextHandler.cs`

**Interfaces:**
- Consumes: `SoundManager`, `PrefabManager`, `FirebaseManager`, `TextDataManager`, `InputManager`, `AdmobManager`.

- [ ] **Step 1: `GameLobbyUI.cs`**

Add near the top of the class:

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

Add `using VContainer;`. Replace call sites:

```csharp
// Init() — before
SoundManager.Instance.PlayBGM(SoundData.Lobby).Forget();
// after
_soundManager.PlayBGM(SoundData.Lobby).Forget();
```

```csharp
// InitLoadUI() — before
_legalUI = await PrefabManager.Instance.InstantiateDynamicUI<IBaseUI>(PrefabData.LegalUI);
// after
_legalUI = await _prefabManager.InstantiateDynamicUI<IBaseUI>(PrefabData.LegalUI);
```

```csharp
// OnCLickLeaderboard() — before
FirebaseManager.Instance.ShowLeaderboardUI().Forget();
// after
_firebaseManager.ShowLeaderboardUI().Forget();
```

```csharp
// ChangeResolution() — before
var canvasHeight = PrefabManager.Instance.MainCanvas.rect.height;
// after
var canvasHeight = _prefabManager.MainCanvas.rect.height;
```

- [ ] **Step 2: `GameOverUI.cs`**

Add near the top of the class:

```csharp
private SoundManager _soundManager;

[Inject]
public void Construct(SoundManager soundManager)
{
    _soundManager = soundManager;
}
```

Add `using VContainer;`. Replace call sites (note `AdmobManager.Instance` stays untouched here — see the note below):

```csharp
// Init() — before
SoundManager.Instance.PlayBGM().Forget();
// after
_soundManager.PlayBGM().Forget();
```

```csharp
// UpdateHighScore() — before
SoundManager.Instance.PlaySFX(SoundData.Confetti).Forget();
// after
_soundManager.PlaySFX(SoundData.Confetti).Forget();
```

`AdmobManager.Instance.CreateInterstitial(2f).Forget();` inside `UpdateHighScore()` lives inside a method that's never called (`UpdateHighScore` is only invoked from the commented-out body of `SetScore`). Because it's dead code, Task 10's grep will still flag this literal string — so add the injection now (it costs nothing) rather than leaving a grep exception to explain later:

```csharp
private AdmobManager _admobManager;

[Inject]
public void ConstructAdmob(AdmobManager admobManager)
{
    _admobManager = admobManager;
}
```

```csharp
// UpdateHighScore() — before
#if UNITY_ANDROID || UNITY_EDITOR
        AdmobManager.Instance.CreateInterstitial(2f).Forget();
#endif
// after
#if UNITY_ANDROID || UNITY_EDITOR
        _admobManager.CreateInterstitial(2f).Forget();
#endif
```

- [ ] **Step 3: `InGameUI.cs`**

Add near the top of the class:

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

Add `using VContainer;`. Replace call sites:

```csharp
// Init() — before
SoundManager.Instance.PlayBGM(SoundData.Ingame).Forget();
// after
_soundManager.PlayBGM(SoundData.Ingame).Forget();
```

```csharp
// InitLoadUI() — before
_scoreUI = await PrefabManager.Instance.InstantiateDynamicUI<IScore>(PrefabData.IngameScoreUI);
...
_menuUI = await PrefabManager.Instance.InstantiateDynamicUI<IBaseUI>(PrefabData.MenuUI);
// after
_scoreUI = await _prefabManager.InstantiateDynamicUI<IScore>(PrefabData.IngameScoreUI);
...
_menuUI = await _prefabManager.InstantiateDynamicUI<IBaseUI>(PrefabData.MenuUI);
```

- [ ] **Step 4: `InquriyUI.cs`**

Add near the top of the class:

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

Add `using VContainer;`. Replace call sites in `SendInquiry()`:

```csharp
// Before
var ret = await FirebaseManager.Instance.SendInquiryAsync(_content.text, _email.text);
var popup = await PrefabManager.Instance.InstantiateDynamicUI<IPopupNotice>(PrefabData.PopupNoticeUI);
// After
var ret = await _firebaseManager.SendInquiryAsync(_content.text, _email.text);
var popup = await _prefabManager.InstantiateDynamicUI<IPopupNotice>(PrefabData.PopupNoticeUI);
```

- [ ] **Step 5: `MenuUI.cs`**

Add near the top of the class:

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

Add `using VContainer;`. Replace call sites:

```csharp
// Init() — before
InputManager.Instance.UseInputHandler = false;
// after
_inputManager.UseInputHandler = false;
```

```csharp
// InitLoadUI() — before
_inquriyUI = await PrefabManager.Instance.InstantiateDynamicUI<IBaseUI>(PrefabData.InquriyUI, this.transform);
// after
_inquriyUI = await _prefabManager.InstantiateDynamicUI<IBaseUI>(PrefabData.InquriyUI, this.transform);
```

```csharp
// OnClickCloseBtn() — before
InputManager.Instance.UseInputHandler = true;
// after
_inputManager.UseInputHandler = true;
```

- [ ] **Step 6: `PopupNoticeUI.cs`**

Add near the top of the class:

```csharp
private TextDataManager _textDataManager;

[Inject]
public void Construct(TextDataManager textDataManager)
{
    _textDataManager = textDataManager;
}
```

Add `using VContainer;`. Replace the call site in `SetNoticeContent`:

```csharp
// Before
_content.text = TextDataManager.Instance.GetGameText(content);
// After
_content.text = _textDataManager.GetGameText(content);
```

- [ ] **Step 7: `PopupQuestionUI.cs`**

Add near the top of the class:

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

Add `using VContainer;`. Replace call sites:

```csharp
// Init() — before
InputManager.Instance.SubscribeToInputHandler(InputType.Game_Exit, OnClickBackKey);
// after
_inputManager.SubscribeToInputHandler(InputType.Game_Exit, OnClickBackKey);
```

```csharp
// Close() — before
InputManager.Instance.UnsubscribeToInputHandler(InputType.Game_Exit, OnClickBackKey);
// after
_inputManager.UnsubscribeToInputHandler(InputType.Game_Exit, OnClickBackKey);
```

```csharp
// SetNoticeContent() — before
_content.text = TextDataManager.Instance.GetGameText(content);
// after
_content.text = _textDataManager.GetGameText(content);
```

- [ ] **Step 8: `SoundSettingUI.cs`**

Add near the top of the class:

```csharp
private SoundManager _soundManager;

[Inject]
public void Construct(SoundManager soundManager)
{
    _soundManager = soundManager;
}
```

Add `using VContainer;`. Replace call sites:

```csharp
// Awake() — before
_bgmToggle.SetValueWithoutNotify(SoundManager.Instance.IsBGMOn);
_sfxToggle.SetValueWithoutNotify(SoundManager.Instance.IsSFXOn);
// after
_bgmToggle.SetValueWithoutNotify(_soundManager.IsBGMOn);
_sfxToggle.SetValueWithoutNotify(_soundManager.IsSFXOn);
```

```csharp
// OnBGMToggleChanged() — before
SoundManager.Instance.IsBGMOn = value;
// after
_soundManager.IsBGMOn = value;
```

```csharp
// OnSFXToggleChanged() — before
SoundManager.Instance.IsSFXOn = value;
// after
_soundManager.IsSFXOn = value;
```

**Important:** `SoundSettingUI` is a plain `MonoBehaviour`. Check in the Editor whether it's spawned through `PrefabManager` (as part of some other UI prefab's hierarchy — in which case injection reaches it automatically) or placed directly as a static object in `Color_Brick.unity`. If it's scene-placed, `[Inject]` alone won't run — add `builder.RegisterComponentInHierarchy<SoundSettingUI>();` to `GameLifetimeScope.Configure`.

- [ ] **Step 9: `IngameScoreUI.cs`**

Add near the top of the class:

```csharp
private PrefabManager _prefabManager;

[Inject]
public void Construct(PrefabManager prefabManager)
{
    _prefabManager = prefabManager;
}
```

Add `using VContainer;`. Replace the call site in `ChangeResolution`:

```csharp
// Before
var canvasHeight = PrefabManager.Instance.MainCanvas.rect.height;
// After
var canvasHeight = _prefabManager.MainCanvas.rect.height;
```

- [ ] **Step 10: `TextHandler.cs`**

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

**Important:** like `SoundSettingUI`, check whether any `TextHandler` instance sits on a GameObject placed directly in `Color_Brick.unity` rather than inside a prefab spawned via `PrefabManager`. If so, add `builder.RegisterComponentInHierarchy<TextHandler>();` to `GameLifetimeScope`.

- [ ] **Step 11: Verify in Editor**

Play `Color_Brick.unity` and walk through every screen touched in this task: lobby (leaderboard button, resolution change), start a round → menu (symbol toggle, inquiry), sound settings toggles, game over screen, inquiry send flow. Confirm no console errors and all text renders (proves `TextDataManager` injection reached every `TextHandler`/popup).

- [ ] **Step 12: Commit**

```bash
git add Assets/Scripts/UI
git commit -m "Convert UI layer to VContainer injection"
```

---

### Task 9: `SoundEmitter` → injection

`SoundEmitter` isn't instantiated from any script in this codebase (no reference to it outside its own file) — it's a component added directly to prefabs/scene objects in the Unity Inspector. Whether it needs `RegisterComponentInHierarchy` depends on where it actually lives.

**Files:**
- Modify: `Assets/Scripts/Core/Sound/SoundEmitter.cs`

**Interfaces:**
- Consumes: `SoundManager`.

- [ ] **Step 1: Check where `SoundEmitter` components live**

In the Unity Editor, check likely UI button prefabs for a `SoundEmitter` component. Note whether they're:
- (a) children of prefabs spawned via `PrefabManager` (e.g. a button inside `MenuUI` or `GameLobbyUI`), or
- (b) placed directly in `Color_Brick.unity`'s static hierarchy.

- [ ] **Step 2: Wire the injection**

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
        //_audioSource.clip = await SoundManager.Instance.LoadAudioClip(_soundType);
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
        //SoundManager.Instance.FadeSound(_audioSource, value, duration);
    }
}
```

Note: the old `OnDestroy` guarded with `SoundManager.IsCreatedInstance()` (a check that only made sense against the static singleton). With injection, `_soundManager` is simply null if `Awake` never ran (object destroyed before injection, or never spawned through a container path) — the `?.` null-conditional covers that case equivalently.

- [ ] **Step 3: If Step 1 found scene-placed instances, register them**

`Assets/Scripts/GameLifetimeScope.cs`, inside `Configure`:

```csharp
builder.RegisterComponentInHierarchy<SoundEmitter>();
```

Skip this step if every `SoundEmitter` lives inside a prefab spawned via `PrefabManager` — those get injected automatically.

- [ ] **Step 4: Verify in Editor**

Play `Color_Brick.unity`, trigger whatever UI action plays a `SoundEmitter` sound (e.g. a button click), confirm it plays and volume responds to the sound settings toggle from Task 8.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Sound/SoundEmitter.cs Assets/Scripts/GameLifetimeScope.cs
git commit -m "Convert SoundEmitter to VContainer injection"
```

---

### Task 10: Final cleanup — strip singleton bases, delete dead code, full regression pass

This is the only task that removes any manager's `SingletonInstance<T>`/`ReferenceManager<T>` inheritance — safe now because Tasks 1-9 converted every consumer in the codebase.

**Files:**
- Modify: `Assets/Scripts/Core/Core_Resource/AddressableManager.cs` (already `MonoBehaviour` since Task 2 — just confirmed here)
- Modify: `Assets/Scripts/Core/PrefabManager.cs`
- Modify: `Assets/Scripts/Core/SoundManager.cs`
- Modify: `Assets/Scripts/Core/TextDataManager.cs`
- Modify: `Assets/Scripts/Core/InputManager.cs`
- Modify: `Assets/Scripts/Core/FirebaseManager.cs`
- Modify: `Assets/Scripts/Core/AdmobManager.cs`
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Modify: `Assets/Scripts/Core/ReferenceManager.cs`
- Delete (once nothing extends it): `Assets/Scripts/Share/SingletonInstance.cs`
- Delete (once nothing implements/uses it): `Assets/Scripts/Core/Interface/IManager.cs`, `Assets/Scripts/Core/Attribute/ManagerOrderAttribute.cs`

**Interfaces:** None — this task only removes dead inheritance and files; no consumer's public API changes.

- [ ] **Step 1: Grep-confirm zero remaining `.Instance` references to project-owned managers**

```bash
grep -rn "AddressableManager.Instance\|PrefabManager.Instance\|SoundManager.Instance\|TextDataManager.Instance\|InputManager.Instance\|FirebaseManager.Instance\|AdmobManager.Instance\|GameManager.Instance" Assets/Scripts
```

Expected: no output. If anything remains, go convert that file first using the same `[Inject] Construct(...)` pattern from the earlier tasks before proceeding.

- [ ] **Step 2: Strip `SingletonInstance<T>` / `ReferenceManager<T>` inheritance from each manager**

```csharp
// PrefabManager.cs — Before
public class PrefabManager : ReferenceManager<PrefabManager>, IManager
// After
public class PrefabManager : MonoBehaviour, IManager
```

Since `PrefabManager` no longer extends `ReferenceManager<T>`, move the members it actually uses (`_assetMap`, `_addressableManager`, `_resolver`, `AssetReferenceMapping`, `PreloadAssets`, `LoadAsset`, `InstantiateObject`, the `Construct` injection method) directly into `PrefabManager` and `SoundManager` — `ReferenceManager<T>` becomes dead once both its only two subclasses stop extending it. After moving the members in, delete `Assets/Scripts/Core/ReferenceManager.cs`.

```csharp
// SoundManager.cs — Before
public class SoundManager : ReferenceManager<SoundManager>, IManager
// After
public class SoundManager : MonoBehaviour, IManager
```

(Same treatment — pull in the `AssetReferenceMapping`/`PreloadAssets`/`LoadAsset` members it uses from the old base, keep the injection fields it already has from Tasks 2/3/5.)

```csharp
// TextDataManager.cs
public class TextDataManager : MonoBehaviour, IManager   // was SingletonInstance<TextDataManager>

// InputManager.cs
public class InputManager : MonoBehaviour, IManager      // was SingletonInstance<InputManager>

// FirebaseManager.cs
public class FirebaseManager : MonoBehaviour, IManager   // was SingletonInstance<FirebaseManager>

// AdmobManager.cs
public class AdmobManager : MonoBehaviour, IManager       // was SingletonInstance<AdmobManager>

// GameManager.cs
public class GameManager : MonoBehaviour, IManager, IAsyncStartable, IDisposable   // was SingletonInstance<GameManager>
```

For each, remove the `Init()` override if its body is empty or only calls `base.Init()`; move any real initialization logic into `Awake()` instead, since there's no more `SingletonInstance.Awake() → Init()` chain driving it. Example for `InputManager`:

```csharp
// Before
public override void Init()
{
    base.Init();
    _inputHandler = new PlayerInput();
    UseInputHandler = true;
}

// After
private void Awake()
{
    _inputHandler = new PlayerInput();
    UseInputHandler = true;
}
```

Do the same for `AdmobManager` (move its `Init()` body — `Logging("Admob 초기화")` + `RequestConsent()` — into `Awake()`) and `FirebaseManager` (move whatever its `Init()` override does into `Awake()`).

- [ ] **Step 3: Grep-confirm zero remaining references to `IManager`/`ManagerOrder`/`SingletonInstance`, then delete the dead files**

```bash
grep -rn "IManager\|ManagerOrder\|SingletonInstance" Assets/Scripts
```

Expected: no output (every manager's declaration was cleaned up in Step 2 — drop `: IManager` and `[ManagerOrder(N)]` from each as part of that same edit if you haven't already). Then:

```bash
git rm Assets/Scripts/Share/SingletonInstance.cs Assets/Scripts/Share/SingletonInstance.cs.meta
git rm Assets/Scripts/Core/Interface/IManager.cs Assets/Scripts/Core/Interface/IManager.cs.meta
git rm Assets/Scripts/Core/Attribute/ManagerOrderAttribute.cs Assets/Scripts/Core/Attribute/ManagerOrderAttribute.cs.meta
```

- [ ] **Step 4: Full regression playtest**

Play `Color_Brick.unity` end to end:
1. Boot sequence — no console errors, loading screen closes.
2. Lobby — leaderboard button, legal popup, resolution change on window resize.
3. Start a round — bricks spawn, touch/drag slides the board, slide/match SFX play, score and combo update.
4. Menu during a round — symbol toggle, inquiry flow (send + popup), sound settings toggles, exit-to-home.
5. Let a round end naturally (board fills) — game over screen shows correct score/combo.
6. Retry and home buttons on game over screen.
7. Trigger the exit popup (`Game_Exit` input) and confirm quit flow.
8. Pause the app (or simulate via Editor) and quit — confirm no exceptions from `GameManager.Dispose()` / `OnApplicationPause` / `OnApplicationQuit`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Remove singleton base classes now that VContainer migration is complete"
```
