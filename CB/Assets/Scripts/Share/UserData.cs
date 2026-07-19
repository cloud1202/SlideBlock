#if UNITY_ANDROID || UNITY_EDITOR
using Firebase.Firestore;
#endif
using UnityEngine;

#if UNITY_ANDROID || UNITY_EDITOR
[FirestoreData]
#endif
public class UserData
{
    public UserData()
    {
        ClassicScore = PlayerPrefs.GetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.HighScore_Classic)], 0);
        IsBGMOn = PlayerPrefs.GetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.IsBGMOn)], 1) > 0 ? true : false;
        IsSFXOn = PlayerPrefs.GetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.IsSFXOn)], 1) > 0 ? true : false;
        IsSymbolOn = PlayerPrefs.GetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.IsSymbolOn)], 1) > 0 ? true : false;
    }

    // DB 필드명: "high_score" ↔ C# 프로퍼티명: HighScore
#if UNITY_ANDROID || UNITY_EDITOR
    [FirestoreProperty("HighScore_Classic")]
#endif
    public int ClassicScore { get; set; } = 0;

#if UNITY_ANDROID || UNITY_EDITOR
    [FirestoreProperty("lastPlayedAt")]
    public Timestamp LastPlayed { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    [FirestoreProperty("IsBGMOn")]
#endif
    public bool IsBGMOn { get; set; } = true;

#if UNITY_ANDROID || UNITY_EDITOR
    [FirestoreProperty("IsSFXOn")]
#endif
    public bool IsSFXOn { get; set; } = true;

#if UNITY_ANDROID || UNITY_EDITOR
    [FirestoreProperty("IsSymbolOn")]
#endif
    public bool IsSymbolOn { get; set; } = false;
    public bool IsDirty { get; set; } = false;
}
