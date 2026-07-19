using Cysharp.Threading.Tasks;
using UnityEngine;


[ManagerOrder(4)]
public class PrefabManager : ReferenceManager<PrefabManager>, IManager
{
    private ISafeAreaFitter _staticCanvas;
    private ISafeAreaFitter _dynamicCanvas;
    private Camera _mainCamera;
    public RectTransform MainCanvas => _staticCanvas.MyRT;
    public Camera MainCamera => _mainCamera;

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
        _mainCamera = await InstantiateObject<Camera>(PrefabData.MainCamera, GameManager.Instance.transform, true);
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

        return await AddressableManager.Instance.Instantiate<TI>(obj, parent, isProtected);
    }
}
