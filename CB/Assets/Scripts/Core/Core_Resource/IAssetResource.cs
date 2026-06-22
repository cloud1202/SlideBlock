using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

public interface IAssetResource
{
    public int Index { get; }
    public ContainLabel ContainLabel { get;  }
    public GameObject instance { get; }
    public bool isInstance { get; }

    public bool isValid { get; }
    public bool runtimeKeyIsValid { get; }
    public AsyncOperationHandle LoadAssetHandle();
    public UniTask<T> InstantiateAsync<T>(Transform parent);
    public void ReleaseAsset();
}
