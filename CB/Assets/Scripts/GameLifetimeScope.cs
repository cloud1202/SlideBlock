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

        builder.RegisterEntryPoint<GameManager>(Lifetime.Singleton).AsSelf();
    }
}
