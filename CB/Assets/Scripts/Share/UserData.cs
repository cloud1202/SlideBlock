using System;
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
#if UNITY_ANDROID || UNITY_EDITOR
        LastPlayed = ReadLastPlayedFromPrefs();
#endif
    }

#if UNITY_ANDROID || UNITY_EDITOR
    private static string LastPlayedKey
        => SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.LastPlayed)];

    /// <summary>
    /// PlayerPrefs에 기록된 최종 플레이 시각을 읽는다. 기록이 없으면 Unix epoch(1970)을
    /// 돌려주어, 원격 문서와 비교할 때 항상 원격이 최신으로 판정되게 한다.
    /// </summary>
    private static Timestamp ReadLastPlayedFromPrefs()
    {
        string raw = PlayerPrefs.GetString(LastPlayedKey, string.Empty);

        return long.TryParse(raw, out long unixSeconds)
            ? Timestamp.FromDateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(unixSeconds))
            : Timestamp.FromDateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(0));
    }

    /// <summary>
    /// 최종 플레이 시각을 지금으로 갱신하고 PlayerPrefs에도 기록한다.
    /// 클라우드 저장이 차단된 상태에서도 로컬에는 남으므로, 다음 실행에서 원격 문서와
    /// 비교해 이 세션의 변경을 살릴 수 있다.
    /// </summary>
    public void TouchLastPlayed()
    {
        LastPlayed = Timestamp.GetCurrentTimestamp();
        PlayerPrefs.SetString(LastPlayedKey, LastPlayed.ToDateTimeOffset().ToUnixTimeSeconds().ToString());
    }
#else
    /// <summary>WebGL에는 대조할 원격 문서가 없어 최종 플레이 시각을 추적하지 않는다.</summary>
    public void TouchLastPlayed() { }
#endif

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
