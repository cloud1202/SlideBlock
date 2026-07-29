using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using static GameTextSO;

public class TextDataManager : BaseManager
{
    private GameTextSO _gameText;

    protected Dictionary<int, GameText> _gameTextMap = new Dictionary<int, GameText>();

    private AddressableManager m_addressableManager;
    [Inject]
    public void Construct(AddressableManager addressableManager)
    {
        m_addressableManager = addressableManager;
        LoadAssetReference().Forget();
        CompleteInit(ManagerType.TextData);
    }


    async public virtual UniTask LoadAssetReference()
    {
        _gameText = await m_addressableManager.LoadResourceData<GameTextSO>(nameof(GameTextSO));
        AssetReferenceMapping();
    }

    protected void AssetReferenceMapping()
    {
        foreach (var text in _gameText.textData)
        {
            if (!_gameTextMap.ContainsKey(text.Index))
            {
                _gameTextMap.Add(text.Index, text);
            }
        }
    }

    public string GetGameText(GameTextData data)
    {
        var index = EnumConverter.Enum32ToInt(data);
        if (_gameTextMap.TryGetValue(index, out GameText gt) == false)
        {
            LLogger.Log($"Not Found Game Text : {data}");
            return string.Empty;
        }
        //return gt.text[EnumConverter.Enum32ToInt(GameManager.Instance.Language)];
        return "";
    }
}
