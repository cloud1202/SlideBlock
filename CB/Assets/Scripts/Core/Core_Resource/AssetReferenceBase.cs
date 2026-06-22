using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AssetReferenceBase<E, T> : ScriptableObject
    where E : Enum
    where T : UnityEngine.Object
{
    [Serializable]
    public class AssetResource : IAssetResource
    {
        public E id;
        public int Index => EnumConverter.Enum32ToInt(id);
        public AssetReferenceT<T> data;
        public ContainLabel containLabel;

        public GameObject instance { get; private set; }
        public bool isInstance => instance != null;

        public bool isValid => data.OperationHandle.IsValid() && data.OperationHandle.Status == AsyncOperationStatus.Succeeded;
        public bool runtimeKeyIsValid => data.RuntimeKeyIsValid();

        public ContainLabel ContainLabel => containLabel;

        public async UniTask<T1> InstantiateAsync<T1>(Transform parent)
        {
            var handle = data.InstantiateAsync(parent);
            await handle.ToUniTask();
            instance = handle.Result;

            if (typeof(T1) == typeof(GameObject))
                return (T1)(object)instance;
            return instance.GetComponent<T1>();
        }

        public AsyncOperationHandle LoadAssetHandle() 
        { 
            if (isValid)
                return  data.OperationHandle;

            return data.LoadAssetAsync();
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
