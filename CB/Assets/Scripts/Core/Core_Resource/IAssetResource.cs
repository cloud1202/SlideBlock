using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

public interface IAssetResource
{
    public GameObject instance { get; }
    public bool isInstance { get; }

    public bool isValid { get; }
    public AsyncOperationHandle<GameObject> LoadAsset(Action<AsyncOperationHandle<GameObject>> complete);
    public UniTask<T> InstantiateAsync<T>(Transform parent);
    public void ReleaseAsset();
}
