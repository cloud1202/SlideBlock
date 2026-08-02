using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        LLogger.Log("GameLifetimeScope");
        builder.RegisterEntryPoint<ManagerInitTracker>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<AddressableManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<FirebaseManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<TelemetryManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<UserSettings>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<AdmobManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<InputManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<PrefabManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<SoundManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<TextDataManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<GameManager>(Lifetime.Singleton).AsSelf();
        LLogger.Log("GameLifetimeScope End");
    }
}
