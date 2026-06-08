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

    async public UniTask LoadCanvas()
    {
        _canvas = await InstantiateObject<Canvas>(PrefabData.MainCanvas, this.transform, true);
    }
    async public UniTask LoadLobbyUI()
    {
        await InstantiateUI<GameObject>(PrefabData.LobbyUI);
    }

    async public UniTask<TI> InstantiateUI<TI>(PrefabData data, Transform parent = null, bool isProtected = false) where TI : UnityEngine.Object
    {
        if (_assetMap.TryGetValue(data, out var obj) == false)
        {
            Logging($"Not Find AssetReference! {data}");
            return default;
        }
        
        if (obj.isInstance)
        {
            Logging($"Current Use Instance! {data}");
            return obj.isInstance as TI;
        }

        if (parent == null)
            parent = _canvas.transform;

        return await AddressableManager.Instance.Instantiate<TI>(obj, parent, isProtected);
    }
}
