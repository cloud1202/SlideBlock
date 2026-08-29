using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using static GameTextSO;

[ManagerOrder(5)]
public class TextDataManager : SingletonInstance<TextDataManager>, IManager
{
    private GameTextSO _gameText;

    protected Dictionary<int, GameText> _gameTextMap = new Dictionary<int, GameText>();
    public override void Init()
    {
        base.Init();
    }

    async public virtual UniTask LoadAssetReference()
    {
        _gameText = await AddressableManager.Instance.LoadResourceData<GameTextSO>(nameof(GameTextSO));
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
        return gt.text[EnumConverter.Enum32ToInt(GameManager.Instance.Language)];
    }
}
