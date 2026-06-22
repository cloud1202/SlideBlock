using Cysharp.Threading.Tasks;
using UnityEngine;


[ManagerOrder(4)]
public class PrefabManager : ReferenceManager<PrefabManager>, IManager
{
    private Canvas _canvas;

    public override void Init()
    {
        base.Init();
    }
    async public override UniTask LoadAssetReference()
    {
        var assets = await AddressableManager.Instance.LoadResourceData<PrefabAssetReference>(nameof(PrefabAssetReference));
        _assetDatas = assets.assetDatas;
        await base.LoadAssetReference();
    }

    async public UniTask InitLoadObjects()
    {
        var cam = await InstantiateObject<Camera>(PrefabData.MainCamera, GameManager.Instance.transform, true);
        _canvas = await InstantiateObject<Canvas>(PrefabData.MainCanvas, this.transform, true);
        _canvas.worldCamera = cam;
    }

    public async UniTask<TI> InstantiateObject<TI>(PrefabData type, Transform parent = null, bool isProtected = false)
    {
        return await InstantiateObject<TI>(EnumConverter.Enum32ToInt(type), parent, isProtected);
    }

    async public UniTask<TI> InstantiateUI<TI>(PrefabData type, Transform parent = null, bool isProtected = false)
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

        if (parent == null)
            parent = _canvas.transform;

        return await AddressableManager.Instance.Instantiate<TI>(obj, parent, isProtected);
    }
}
