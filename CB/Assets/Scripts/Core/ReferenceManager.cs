using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ReferenceManager<T> : SingletonInstance<T>
    where T : MonoBehaviour
{
    protected Dictionary<int, IAssetResource> _assetMap = new Dictionary<int, IAssetResource>();
    protected IEnumerable<IAssetResource> _assetDatas = new List<IAssetResource>();
    public override void Init()
    {
        base.Init();
    }

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
        Debug.Log($"{assets.Count}");
        await AddressableManager.Instance.PreloadAssets(label, assets.ToArray());
    }

    public async UniTask<TI> LoadAsset<TI>(int index, CancellationToken ct = new CancellationToken()) where TI : UnityEngine.Object
    {
        if (_assetMap.TryGetValue(index, out var obj) == false)
        {
            return default;
        }

        return await AddressableManager.Instance.Load<TI>(obj, ct);
    }
    public async UniTask<TI> InstantiateObject<TI>(int index, Transform parent = null, bool isProtected = false)
    {
        if (_assetMap.TryGetValue(index, out var obj) == false)
        {
            return default;
        }

        if (parent == null)
            parent = this.transform;

        return await AddressableManager.Instance.Instantiate<TI>(obj, parent, isProtected);
    }
}
