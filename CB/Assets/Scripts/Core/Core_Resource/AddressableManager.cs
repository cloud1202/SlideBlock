using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using VContainer.Unity;

public class AddressableManager : BaseManager
{
    private Dictionary<ContainLabel, List<AsyncOperationHandle>> _loadHandles = new Dictionary<ContainLabel, List<AsyncOperationHandle>>();
    private List<GameObject> _instantiateHandles = new List<GameObject>();


    private readonly IObjectResolver m_resolver;
    public AddressableManager(ManagerInitTracker tracker, IObjectResolver resolver) : base(tracker)
    {
        m_resolver = resolver;
        LLogger.Log("AddressableManager");
        SetAddressable().Forget();
    }
    public async UniTask SetAddressable()
    {
        await Addressables.InitializeAsync();
        await LoadRemoteAddressable();
        CompleteInit(ManagerType.Addressable);
    }

    private async UniTask LoadRemoteAddressable()
    {
        long downloadSize = await Addressables.GetDownloadSizeAsync("default");
        if (downloadSize == 0)
        {
            Logging($"Not Found Download Addressable");
            return;
        }
        var downloadHandle = Addressables.DownloadDependenciesAsync("default", false);
        float progress = 0;

        while (downloadHandle.Status == AsyncOperationStatus.None)
        {
            float percentageComplete = downloadHandle.GetDownloadStatus().Percent;
            if (percentageComplete > progress * 1.1) // Report at most every 10% or so
            {
                progress = percentageComplete; // More accurate %
                Logging($"Progress :: {progress} Download Size :: {downloadHandle.GetDownloadStatus().DownloadedBytes} / {downloadSize}");
                //UpdateLoadGauage(progress);
            }

            await UniTask.NextFrame();
        }

        CompletionAddressableLoad(downloadHandle.Status == AsyncOperationStatus.Succeeded);
        downloadHandle.Release(); //Release the operation handle
    }

    private void UpdateLoadGauage(float progress)
    {
        Logging($"Progress :: {progress}");
    }

    private void CompletionAddressableLoad(bool complete)
    {
        Logging($"Complete :: {complete}");
    }

    public void AssetReleaseForLabel(ContainLabel label)
    {
        if (_loadHandles.TryGetValue(label, out List<AsyncOperationHandle> handles) == false)
            return;

        Logging($"Release {label} Load Handle Count : {handles.Count}");

        foreach (var handle in handles)
        {
            Logging($"{handle.DebugName}");
            handle.Release();
        }

        _loadHandles.Remove(label);

        Logging($"Clear {label} Load Handle Count : {handles.Count}");
    }

    public void InstantiateRelease()
    {
        Logging($"Release InstantiateRelease Handle Count : {_instantiateHandles.Count}");

        foreach (var handle in _instantiateHandles)
        {
            if (handle == null)
                continue;
            Logging($"{handle.name}");
            GameObject.Destroy(handle);
        }

        _instantiateHandles.Clear();

        Logging($"Clear InstantiateRelease Handle Count : {_instantiateHandles.Count}");
    }

    public async UniTask PreloadAssets(ContainLabel label, IAssetResource[] assetResources)
    {
        if (_loadHandles.ContainsKey(label) == false)
            _loadHandles.Add(label, new List<AsyncOperationHandle>());

        List<UniTask> handles = new List<UniTask>();

        Logging($"PreLoad Asset : {label}");
        int length = assetResources.Length;
        for (int index = 0; index < length; ++index)
        {
            if (assetResources[index].isValid)
                continue;
            var loadHandle = assetResources[index].LoadAssetHandle();
            loadHandle.Completed += OnComplete;
            _loadHandles[label].Add(loadHandle);
            handles.Add(loadHandle.ToUniTask());
        }

        await UniTask.WhenAll(handles);
        Logging($"Complete PreLoad Assets");

        void OnComplete(AsyncOperationHandle handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                Logging($"loadHandle Asset : {handle.Result.ToString()}");
            }
            else
            {
                Logging($"Error Load Asset : {handle.OperationException}");
            }
        }
    }

    public async UniTask<T> Load<T>(IAssetResource assetRef, CancellationToken ct = new CancellationToken()) where T : UnityEngine.Object
    {
        if (assetRef == null || !assetRef.runtimeKeyIsValid)
        {
            Warning($"AssetReference가 유효하지 않습니다.");
            return null;
        }

        if (assetRef.isValid)
        {
            var handle = assetRef.LoadAssetHandle();

            return handle.Result as T;
        }

        var newHandle = assetRef.LoadAssetHandle();

        _loadHandles[assetRef.ContainLabel].Add(newHandle);
        await newHandle.ToUniTask(cancellationToken : ct);
        
        if (newHandle.Status == AsyncOperationStatus.Succeeded)
        {
            Logging("newLoadHandle");
            return newHandle.Result as T;
        }
        
        Error($"Failed to load {newHandle.OperationException}");
        return null;
    }

    public async UniTask<T> Instantiate<T>(IAssetResource assetResource, Transform parent, bool isProtected)
    {
        //await Load<GameObject>(assetRef);

        var go = await assetResource.InstantiateAsync<GameObject>(parent);
        m_resolver.InjectGameObject(go);
        var obj = go.AddComponent<InstantiateObject>();
        if (isProtected == false)
            _instantiateHandles.Add(go);

        obj.SetAssetReference(assetResource);

        return go.GetComponent<T>();
    }


    public async UniTask<T> LoadResourceData<T>(string name)
    {
        return await Addressables.LoadAssetAsync<T>($"Assets/AddressableAssets/ScriptableObject/{name}.asset");
    }
}
