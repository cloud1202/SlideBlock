using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameTextSO", menuName = "SO/GameTextSO")]
public class GameTextSO : ScriptableObject
{
    [Serializable]
    public class GameText
    {
        public GameTextData id;
        public int Index => EnumConverter.Enum32ToInt(id);
        public string[] text;
    }
    public List<GameText> textData;
}
