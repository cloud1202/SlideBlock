using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 유저 설정 상태의 단일 소유자. PlayerPrefs를 즉시 미러링하고 Firestore 저장을 요청한다.
/// 로드가 끝나기 전에는 IsLoaded가 false이며, 접근자를 호출하면 안 된다.
/// </summary>
public class UserSettings : BaseManager
{
    private readonly FirebaseManager m_firebase;
    private readonly TelemetryManager m_telemetry;
    private UserData _user;

    public UserSettings(ManagerInitTracker tracker, FirebaseManager firebase, TelemetryManager telemetry)
        : base(tracker)
    {
        LLogger.Log("UserSettings");
        m_firebase = firebase;
        m_telemetry = telemetry;
        LoadAsync().Forget();
    }

    public bool IsLoaded => _user != null;

    private async UniTask LoadAsync()
    {
        await CheckedManagers(ManagerType.Firebase);
        _user = await m_firebase.LoadUserAsync();
        CompleteInit(ManagerType.UserSettings);
        Logging("유저 설정 준비 완료");
    }

    public int ClassicScore
    {
        get => _user.ClassicScore;
        set
        {
            SetPref(SaveFieldType.HighScore_Classic, value);
            _user.ClassicScore = value;
            _user.IsDirty = true;
            m_firebase.SaveUser(_user);
            m_firebase.ReportScore(value).Forget();
        }
    }

    public bool IsBGMOn
    {
        get => _user.IsBGMOn;
        set
        {
            SetPref(SaveFieldType.IsBGMOn, value ? 1 : 0);
            if (!value) m_telemetry.LogEvent("bgm_off");
            _user.IsBGMOn = value;
            _user.IsDirty = true;
            m_firebase.SaveUser(_user);
        }
    }

    public bool IsSFXOn
    {
        get => _user.IsSFXOn;
        set
        {
            SetPref(SaveFieldType.IsSFXOn, value ? 1 : 0);
            if (!value) m_telemetry.LogEvent("sfx_off");
            _user.IsSFXOn = value;
            _user.IsDirty = true;
            m_firebase.SaveUser(_user);
        }
    }

    public bool IsSymbolOn
    {
        get => _user.IsSymbolOn;
        set
        {
            SetPref(SaveFieldType.IsSymbolOn, value ? 1 : 0);
            if (value) m_telemetry.LogEvent("symbol_on");
            _user.IsSymbolOn = value;
            _user.IsDirty = true;
            m_firebase.SaveUser(_user);
        }
    }

    private static void SetPref(SaveFieldType field, int value)
    {
        PlayerPrefs.SetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(field)], value);
    }
}
