# VContainer Full Migration — Design

## Goal

Replace the MonoBehaviour singleton pattern (`SingletonInstance<T>` / `ReferenceManager<T>` + `.Instance` access, 119 call sites across 22 files) with VContainer dependency injection across the whole project. Remove `Bootstrap.cs`'s reflection-based manager spawner so `GameLifetimeScope` is the single bootstrap path.

## Current State

- `GameLifetimeScope` already registers 7 managers as components (`AddressableManager`, `PrefabManager`, `SoundManager`, `TextDataManager`, `InputManager`, `FirebaseManager`, `AdmobManager`) plus `GameManager` as an entry point — but consumers still reach them via `.Instance`.
- `Bootstrap.cs` independently reflects over all `IManager` implementations and spawns them as GameObjects, duplicating what `GameLifetimeScope` already does.
- `GameManager.Dispose()` / `GameManager.StartAsync()` exist but throw `NotImplementedException` — remnants of a stalled migration attempt.
- Runtime-instantiated objects (`RoundManager`, `Board`, `Brick`, all UI popups) are spawned via `PrefabManager`'s Addressables-based helpers (`InstantiateObject`, `InstantiateStaticUI`, `InstantiateDynamicUI`) and currently get no injection at all — they reach managers via `.Instance`.

## Architecture

### 1. Manager registration stays in `GameLifetimeScope`

No change to *what* gets registered — the 7 managers + `GameManager` stay. `GameManager` keeps `RegisterComponentOnNewGameObject<GameManager>().AsSelf().AsImplementedInterfaces()` so VContainer drives its `IAsyncStartable`/`IDisposable` lifecycle (filling in the currently-stubbed `StartAsync`/`Dispose`) in place of the old manual `Bootstrap()` call. It must stay a MonoBehaviour — it relies on `OnApplicationPause`/`OnApplicationQuit`.

### 2. `IObjectResolver` powers runtime instantiation

`PrefabManager` receives `IObjectResolver` via `[Inject]`. Every helper that currently does `Addressables.InstantiateAsync` (or wraps it) switches to `resolver.Instantiate(prefab, parent)` after the asset loads. This makes injection automatic for anything spawned at runtime — `RoundManager`, `Board`, `Brick`, and every UI popup — with no manual `resolver.Inject(...)` calls needed at call sites.

### 3. Managers drop singleton inheritance, gain `[Inject]` methods

`AddressableManager`, `SoundManager`, `TextDataManager`, `InputManager`, `FirebaseManager`, `AdmobManager` stop extending `SingletonInstance<T>` and become plain `MonoBehaviour`s. Since MonoBehaviours can't take constructor injection, each gains a method-injection entry point:

```csharp
[Inject]
public void Construct(FirebaseManager firebaseManager, AddressableManager addressableManager)
{
    _firebaseManager = firebaseManager;
    _addressableManager = addressableManager;
}
```

`ReferenceManager<T>` (shared by `PrefabManager`, `SoundManager`, `TextDataManager`) keeps its shared logic (`LoadAssetReference`, `PreloadAssets`, `LoadAsset`) but drops the `SingletonInstance<T>` base and takes `AddressableManager` through its own `[Inject]` method. VContainer calls `[Inject]` methods on both base and derived types, so a base-class injection method and a derived-class injection method can coexist without conflict.

### 4. Gameplay & UI layer

`RoundManager`, `Board`, `Brick`, and all UI components (`GameLobbyUI`, `InGameUI`, `GameOverUI`, `MenuUI`, `InquriyUI`, `PopupQuestionUI`, `PopupNoticeUI`, `SoundSettingUI`, `IngameScoreUI`, `TextHandler`, `SoundEmitter`) convert their `.Instance` field reads to `[Inject]`-populated private fields, since they're now spawned through `resolver.Instantiate`.

### 5. Cleanup

- Delete `Bootstrap.cs` entirely.
- After confirming zero remaining references, remove the singleton machinery from `SingletonInstance.cs` (or delete the file if nothing extends it anymore).
- `PlayGamesPlatform.Instance` references in `FirebaseManager` are a third-party SDK singleton — out of scope, left as-is.

## Conversion Order

Bottom-up by dependency, so nothing references an unconverted `.Instance` mid-migration:

1. `AddressableManager` (leaf)
2. `PrefabManager` (depends on `AddressableManager`; introduces `IObjectResolver`)
3. `FirebaseManager` (leaf, but many things depend on it)
4. `SoundManager`, `TextDataManager`, `InputManager`, `AdmobManager`
5. `GameManager` (entry point; fill in `StartAsync`/`Dispose`, replace manual `Bootstrap()`)
6. `RoundManager` → `Board` → `Brick`
7. UI layer (`GameLobbyUI`, `InGameUI`, `GameOverUI`, `MenuUI`, `InquriyUI`, `PopupQuestionUI`, `PopupNoticeUI`, `SoundSettingUI`, `IngameScoreUI`, `TextHandler`, `SoundEmitter`)
8. Delete `Bootstrap.cs`
9. Remove/clean `SingletonInstance.cs` / `ReferenceManager.cs` singleton remnants
10. Play-test `Color_Brick.unity` end to end (boot → lobby → round → game over → exit)

## Verification

No automated test suite exists for this project. Verification is manual: after each numbered step (or small group of steps), open `Color_Brick.unity` in the Editor and Play — confirm no `NullReferenceException`/missing-injection errors in the console, and that the feature touched by that step still behaves correctly. Full pass at the end covering: boot sequence, lobby, starting a round, playing a round (input, sound, score, combo), game over screen, exiting, app pause/quit logging.

## Out of Scope

- `EditBrickColor.unity` / `BrickColorEditorManager` (editor tool, not part of the manager/DI graph).
- `Test.unity` scene.
- Third-party SDK singletons (`PlayGamesPlatform.Instance`).
