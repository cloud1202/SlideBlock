using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ReferenceManager<T> : BaseManager
{
    protected Dictionary<int, IAssetResource> _assetMap = new Dictionary<int, IAssetResource>();
    protected IEnumerable<IAssetResource> _assetDatas = new List<IAssetResource>();

    protected AddressableManager m_addressableManager;

    public ReferenceManager(ManagerInitTracker tracker, AddressableManager addressablemanager) : base(tracker)
    {
        LLogger.Log("ReferenceManager");
        m_addressableManager = addressablemanager;
        Init().Forget();
    }

    async protected virtual UniTask Init() => await UniTask.Yield();

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
        await m_addressableManager.PreloadAssets(label, assets.ToArray());
    }

    public async UniTask<TI> LoadAsset<TI>(int index, CancellationToken ct = new CancellationToken()) where TI : UnityEngine.Object
    {
        if (_assetMap.TryGetValue(index, out var obj) == false)
        {
            return default;
        }

        return await m_addressableManager.Load<TI>(obj, ct);
    }
    protected async UniTask<TI> InstantiateObject<TI>(int index, Transform parent = null, bool isProtected = false)
    {
        if (_assetMap.TryGetValue(index, out var obj) == false)
        {
            return default;
        }

        return await m_addressableManager.Instantiate<TI>(obj, parent, isProtected);
    }
}
