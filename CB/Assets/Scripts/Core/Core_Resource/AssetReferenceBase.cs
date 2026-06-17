using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using System;
using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AssetReferenceBase<E, T> : ScriptableObject
    where E : Enum
    where T : UnityEngine.Object
{
    [Serializable]
    public class AssetResource : IAssetResource
    {
        public E id;
        public AssetReferenceT<T> data;
        public ContainLabel containLabel;

        public GameObject instance { get; private set; }
        public bool isInstance => instance != null;

        public bool isValid => data.OperationHandle.IsValid();

        public async UniTask<T1> InstantiateAsync<T1>(Transform parent)
        {
            var handle = data.InstantiateAsync(parent);
            await handle.ToUniTask();

            instance = handle.Result;
            if (typeof(T1) == typeof(GameObject))
                return (T1)(object)handle.Result;
            return handle.Result.GetComponent<T1>();
        }

        public AsyncOperationHandle<GameObject> LoadAsset(Action<AsyncOperationHandle<GameObject>> complete)
        {
            var loadHandle = data.LoadAssetAsync<GameObject>();
            loadHandle.Completed += complete;
            return loadHandle;
        }

        public void ReleaseAsset()
        {
            // Asset은 Addressable에서 한번에
            //data.ReleaseAsset();
            if(isInstance)
                data.ReleaseInstance(instance);

            instance = null;
        }
    }
    public List<AssetResource> assetDatas;
}
