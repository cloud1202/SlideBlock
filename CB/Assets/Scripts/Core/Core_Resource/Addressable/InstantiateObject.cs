using UnityEngine;
using UnityEngine.AddressableAssets;

public class InstantiateObject : MonoBehaviour
{
    private IAssetResource m_assetRef;

    public void SetAssetReference(IAssetResource assetRef)
    {
        m_assetRef = assetRef;
    }
    private void OnDestroy()
    {
        m_assetRef.ReleaseAsset();
    }
}
