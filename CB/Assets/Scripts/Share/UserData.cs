using Firebase.Firestore;
using UnityEngine;

[FirestoreData]
public class UserData
{
    public UserData()
    {
        ClassicScore = PlayerPrefs.GetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.HighScore_Classic)], 0);
        IsBGMOn = PlayerPrefs.GetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.IsBGMOn)], 1) > 0 ? true : false;
        IsSFXOn = PlayerPrefs.GetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.IsSFXOn)], 1) > 0 ? true : false;
        IsSymbolOn = PlayerPrefs.GetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.IsSymbolOn)], 1) > 0 ? true : false;
    }

    // DB 필드명: "high_score" → C# 프로퍼티명: HighScore
    [FirestoreProperty("HighScore_Classic")]
    public int ClassicScore { get; set; } = 0;

    [FirestoreProperty("lastPlayedAt")]
    public Timestamp LastPlayed { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    [FirestoreProperty("IsBGMOn")]
    public bool IsBGMOn { get; set; } = true;

    [FirestoreProperty("IsSFXOn")]
    public bool IsSFXOn { get; set; } = true;

    [FirestoreProperty("IsSymbolOn")]
    public bool IsSymbolOn { get; set; } = true;
    public bool IsDirty { get; set; } = false;
}
