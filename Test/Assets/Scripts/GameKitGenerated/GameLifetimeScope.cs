using LayonCraft.GameKit;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        PlayerPrefsStore.SetKeys(SaveFieldData.Fields);

        builder.Register<ManagerInitTracker>(Lifetime.Singleton);
        builder.Register<ITelemetry, ConsoleTelemetry>(Lifetime.Singleton);

        builder.RegisterEntryPoint<AddressableManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<InputManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<PrefabManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<SoundManager>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<TextDataManager>(Lifetime.Singleton).AsSelf();
    }
}
