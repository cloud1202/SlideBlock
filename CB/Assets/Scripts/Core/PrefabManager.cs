using Cysharp.Threading.Tasks;
using UnityEngine;


[ManagerOrder(4)]
public class PrefabManager : ReferenceManager<PrefabManager, PrefabData>, IManager
{
    private Canvas _canvas;

    public override void Init()
    {
        base.Init();
    }

    async public UniTask InitLoadObjects()
    {
        var cam = await InstantiateObject<Camera>(PrefabData.MainCamera, GameManager.Instance.transform, true);
        _canvas = await InstantiateObject<Canvas>(PrefabData.MainCanvas, this.transform, true);
        _canvas.worldCamera = cam;
    }

    async public UniTask<TI> InstantiateUI<TI>(PrefabData data, Transform parent = null, bool isProtected = false)
    {
        if (_assetMap.TryGetValue(data, out var obj) == false)
        {
            Logging($"Not Find AssetReference! {data}");
            return default;
        }

        if (obj.isInstance)
        {
            Logging($"Current Use Instance! {data}");
            return obj.instance.GetComponent<TI>();
        }

        if (parent == null)
            parent = _canvas.transform;

        return await AddressableManager.Instance.Instantiate<TI>(obj, parent, isProtected);
    }
}
