// GameKit 뼈대 생성물. 게임에 맞게 값을 채울 것.
using System;

/// <summary>프리팹 에셋 키.</summary>
public enum PrefabData
{
    MainCamera,
    StaticCanvas,
    DynamicCanvas,
}

/// <summary>프리로드 그룹. 플래그이므로 None = 0을 유지할 것.</summary>
[Flags]
public enum ContainLabel
{
    None = 0,
    Common = 1 << 0,
}

/// <summary>사운드 에셋 키.</summary>
public enum SoundData
{
    None,
}

/// <summary>텍스트 키.</summary>
public enum GameTextData
{
    None,
}

/// <summary>PlayerPrefs 저장 필드. 바꾼 뒤 Tools/GameKit/SaveFieldData 생성 을 실행할 것.</summary>
public enum SaveFieldType
{
    IsBGMOn,
    IsSFXOn,
}

public enum LanguageType
{
    Korean,
    English,
}
