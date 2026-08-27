// GameKit 뼈대 생성물.
using Cysharp.Threading.Tasks;
using LayonCraft.GameKit;

public class PrefabManager : PrefabManagerBase<PrefabData, ContainLabel>
{
    public PrefabManager(ManagerInitTracker tracker, AddressableManager addressable)
        : base(tracker, addressable) { }

    protected override async UniTask Init()
    {
        // Addressables 초기화가 끝나야 에셋을 읽을 수 있다.
        await CheckedManagers(typeof(AddressableManager));

        var table = await m_addressableManager
            .LoadResourceData<PrefabAssetReference>(nameof(PrefabAssetReference));

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
        await CheckedManagers(typeof(AddressableManager));

        var table = await m_addressableManager
            .LoadResourceData<SoundAssetReference>(nameof(SoundAssetReference));

        _assetDatas = table.Resources;

        CreateAudioSources();
        await LoadAssetReference(ContainLabel.Common);

        CompleteInit();
    }
}

public class TextDataManager : TextDataManagerBase<GameTextData>
{
    public TextDataManager(ManagerInitTracker tracker, AddressableManager addressable)
        : base(tracker, addressable)
    {
        Init().Forget();
    }

    private async UniTask Init()
    {
        await CheckedManagers(typeof(AddressableManager));

        var table = await m_addressableManager
            .LoadResourceData<GameTextTable>(nameof(GameTextTable));

        BuildMap(table);
        LanguageIndex = EnumConverter.Enum32ToInt(LanguageType.Korean);

        CompleteInit();
    }
}
