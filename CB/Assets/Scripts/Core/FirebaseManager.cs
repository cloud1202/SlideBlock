using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using Firebase.Auth;
using Firebase.Crashlytics;
using Firebase.Extensions;
using Firebase.Firestore;
using Firebase.Messaging;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

/// <summary>
/// Firebase 전 기능(Auth, Firestore, Analytics, Crashlytics, Messaging)을 한 곳에서 관리하는 매니저.
/// Bootstrap의 리플렉션 기반 매니저 로더에 의해 자동으로 생성/초기화됨.
/// 초기화 순서: CheckAndFixDependencies -> 익명 로그인 -> FCM 토큰 등록 -> 유저 데이터 로드
/// </summary>
[ManagerOrder(1)]
public class FirebaseManager : SingletonInstance<FirebaseManager>, IManager
{
    public bool IsInitialized { get; private set; }
    public string UserId { get; private set; }

    private FirebaseFirestore _firestore;
    private DocumentSnapshot _snapShot;
    private const string USERS_COLLECTION = "users";
    private const string LEADERBOARD_COLLECTION = "leaderboard";

    /// <summary>
    /// 닉네임 설정용 캐시. 랭킹 기록 시 같이 올라감. SetNickname()으로 변경 가능.
    /// </summary>
    public string Nickname { get; private set; } = "Player";

    private bool _isInit = false;

    public override void Init()
    {
        base.Init();
        InitializeFirebase();
    }

    #region Core Initialization

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var status = task.Result;
            if (status != DependencyStatus.Available)
            {
                Error($"Firebase 의존성 문제: {status}");
                return;
            }
            _firestore = FirebaseFirestore.DefaultInstance;
            IsInitialized = true;
            Logging("Firebase 초기화 완료");

            InitCrashlytics();
            InitMessaging();
            SignInAnonymously();
        });
    }

    #endregion

    #region Authentication

    private void SignInAnonymously()
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
#if UNITY_EDITOR
        // 에디터: 기존 로그인 세션 재사용 시도
        if (auth.CurrentUser != null)
        {
            UserId = auth.CurrentUser.UserId;
            Logging($"[Editor] 기존 세션 재사용: {UserId}");
            Crashlytics.SetUserId(UserId);
            LoadUserData();
            return;
        }

        // 저장된 UID로 재로그인 불가능 (익명은 토큰 재사용 안 됨)
        // → PlayerPrefs에 저장된 UID를 Firestore 키로만 활용
        string savedUid = PlayerPrefs.GetString("editor_uid", "");
        if (!string.IsNullOrEmpty(savedUid))
        {
            UserId = savedUid;
            Logging($"[Editor] 저장된 UID 재사용: {UserId}");
            LoadUserData();
            return;
        }
#endif
        auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Error($"익명 로그인 실패: {task.Exception}");
                return;
            }

            UserId = task.Result.User.UserId;
            Logging($"로그인 완료, UID: {UserId}");

#if UNITY_EDITOR
            // 에디터에서 첫 로그인 시 저장
            PlayerPrefs.SetString("editor_uid", UserId);
            PlayerPrefs.Save();
            Logging($"[Editor] UID 저장 완료");
#endif
            Crashlytics.SetUserId(UserId);
            LoadUserData();
        });
    }

    #endregion

    #region Firestore
    async public UniTask<int> GetField(SaveFieldType type, int defaultValue = 0)
    {
        await UniTask.WaitUntil(() => _isInit);
        if (_snapShot == null)
        {
            LLogger.Log($"Not Load User Data :: {UserId}");
            return default;
        }

        string field = SaveFieldData.Fields[EnumConverter.Enum32ToInt(type)];
        if (_snapShot.TryGetValue(field, out int ret))
            return ret;
        else
            return PlayerPrefs.GetInt(field, defaultValue);
    }

    async public UniTask<float> GetField(SaveFieldType type, float defaultValue = 0)
    {
        await UniTask.WaitUntil(() => _isInit);
        if (_snapShot == null)
        {
            LLogger.Log($"Not Load User Data :: {UserId}");
            return default;
        }

        string field = SaveFieldData.Fields[EnumConverter.Enum32ToInt(type)];
        if (_snapShot.TryGetValue(field, out float ret))
            return ret;
        else
            return PlayerPrefs.GetFloat(field, defaultValue);
    }

    async public UniTask<T> GetField<T>(SaveFieldType type, T defaultValue = null)
        where T : class
    {
        await UniTask.WaitUntil(() => _isInit);
        if (_snapShot == null)
        {
            LLogger.Log($"Not Load User Data :: {UserId}");
            return default;
        }

        string field = SaveFieldData.Fields[EnumConverter.Enum32ToInt(type)];
        if (_snapShot.TryGetValue(field, out T ret))
            return ret;
        else
        {
            var str = PlayerPrefs.GetString(field, string.Empty);

            return JsonUtility.FromJson<T>(str);
        }
    }

    public void SaveField(SaveFieldType type, object value)
    {
        SaveField(SaveFieldData.Fields[EnumConverter.Enum32ToInt(type)], value);
    }
    public void SaveField(SaveFieldType type, float value)
    {
        string field = SaveFieldData.Fields[EnumConverter.Enum32ToInt(type)];
        PlayerPrefs.SetFloat(field, value);
        SaveField(field, value);
    }
    public void SaveField(SaveFieldType type, int value)
    {
        string field = SaveFieldData.Fields[EnumConverter.Enum32ToInt(type)];
        PlayerPrefs.SetInt(field, value);
        SaveField(field, value);
    }
    public void SaveField(SaveFieldType[] type, int[] value)
    {
        var dic = new Dictionary<string, object>();
        for (int i = 0; i< type.Length; ++i)
        {
            string field = SaveFieldData.Fields[EnumConverter.Enum32ToInt(type[i])];
            dic.Add(field, value[i]);
            PlayerPrefs.SetInt(field, value[i]);
        }
        SaveFields(dic);
    }
    /// <summary>
    /// 유저 문서 한 필드만 병합 저장. 예: SaveField("highScore_classic", 15200)
    /// </summary>
    public void SaveField(string field, object value)
    {
        if (!IsInitialized || string.IsNullOrEmpty(UserId))
        {
            Warning("Firestore 저장 실패: 아직 초기화/로그인되지 않음");
            return;
        }

        LLogger.Log($"SaveField {field} :: {value}");
        var docRef = _firestore.Collection(USERS_COLLECTION).Document(UserId);
        var data = new Dictionary<string, object> { { field, value } };

        docRef.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
                Error($"Firestore 저장 실패 ({field}): {task.Exception}");
        });
    }

    /// <summary>
    /// 모드별 최고점수 저장. users(개인)와 leaderboard(공개 랭킹) 양쪽에 반영됨.
    /// 기존 점수보다 낮으면 leaderboard는 갱신하지 않음(서버에서 최종 검증 전까지는 클라이언트 신뢰 기준).
    /// </summary>
    public void SaveHighScore(SaveFieldType mode, int score)
    {
        if (mode > SaveFieldType.HighScore_Classic)
            return;

        string field = SaveFieldData.Fields[EnumConverter.Enum32ToInt(mode)];
        PlayerPrefs.SetInt(field, score);
        // 개인 기록(users)에는 항상 저장
        SaveFields(new Dictionary<string, object>
        {
            { field, score },
            { "lastPlayedAt", Timestamp.GetCurrentTimestamp() }
        });

        // 공개 랭킹(leaderboard)은 최고점만 반영
        UpdateLeaderboardIfHigher(field, score);
    }

    /// <summary>
    /// 여러 필드를 한 번에 병합 저장.
    /// </summary>
    public void SaveFields(Dictionary<string, object> fields)
    {
        if (!IsInitialized || string.IsNullOrEmpty(UserId))
        {
            Warning("Firestore 저장 실패: 아직 초기화/로그인되지 않음");
            return;
        }
        
        foreach (var key in fields.Keys)
        {
            LLogger.Log($"SaveField {key} :: {fields[key]}");
        }

        var docRef = _firestore.Collection(USERS_COLLECTION).Document(UserId);
        docRef.SetAsync(fields, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
                Error($"Firestore 저장 실패(다중 필드): {task.Exception}");
        });
    }

    /// <summary>
    /// 닉네임 변경. 다음 랭킹 갱신 시 leaderboard 문서에도 반영됨.
    /// </summary>
    public void SetNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return;

        Nickname = nickname;
        SaveField("nickname", nickname);
    }

    private void UpdateLeaderboardIfHigher(string mode, int score)
    {
        if (!IsInitialized || string.IsNullOrEmpty(UserId))
        {
            Warning("Leaderboard 저장 실패: 아직 초기화/로그인되지 않음");
            return;
        }

        var docRef = _firestore.Collection(LEADERBOARD_COLLECTION).Document(UserId);

        docRef.GetSnapshotAsync().ContinueWithOnMainThread(getTask =>
        {
            if (getTask.IsFaulted || getTask.IsCanceled)
            {
                Error($"Leaderboard 조회 실패: {getTask.Exception}");
                return;
            }

            var snapshot = getTask.Result;
            int previousBest = 0;
            if (snapshot.Exists && snapshot.ContainsField(mode))
                previousBest = snapshot.GetValue<int>(mode);

            if (score <= previousBest)
                return; // 기존 최고점보다 낮으면 갱신할 필요 없음

            var data = new Dictionary<string, object>
            {
                { "nickname", Nickname },
                { mode, score },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            };

            docRef.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(setTask =>
            {
                if (setTask.IsFaulted || setTask.IsCanceled)
                    Error($"Leaderboard 저장 실패: {setTask.Exception}");
                else
                    Logging($"Leaderboard 갱신: {mode} = {score}");
            });
        });
    }

    /// <summary>
    /// 모드별 상위 랭킹 조회. 콜백으로 (닉네임, 점수) 리스트 전달.
    /// 예: GetTopScores("classic", 50, list => { ... });
    /// </summary>
    public void GetTopScores(string mode, int limit, Action<List<(string Nickname, int Score)>> onComplete)
    {
        if (!IsInitialized)
        {
            Warning("Leaderboard 조회 실패: 아직 초기화되지 않음");
            onComplete?.Invoke(new List<(string, int)>());
            return;
        }

        string scoreField = $"highScore_{mode}";
        _firestore.Collection(LEADERBOARD_COLLECTION)
            .OrderByDescending(scoreField)
            .Limit(limit)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Error($"Leaderboard 조회 실패: {task.Exception}");
                    onComplete?.Invoke(new List<(string, int)>());
                    return;
                }

                var result = new List<(string, int)>();
                foreach (var doc in task.Result.Documents)
                {
                    string nickname = doc.ContainsField("nickname") ? doc.GetValue<string>("nickname") : "Player";
                    int score = doc.ContainsField(scoreField) ? doc.GetValue<int>(scoreField) : 0;
                    result.Add((nickname, score));
                }

                onComplete?.Invoke(result);
            });
    }

    private void LoadUserData()
    {
        var docRef = _firestore.Collection(USERS_COLLECTION).Document(UserId);
        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            _isInit = true;
            if (task.IsFaulted || task.IsCanceled)
            {
                Error($"유저 데이터 로드 실패: {task.Exception}");
                return;
            }

            _snapShot = task.Result;
            if (!_snapShot.Exists)
            {
                Logging("신규 유저, 초기 문서 생성");
                SaveFields(new Dictionary<string, object>
                {
                    { "createdAt", Timestamp.GetCurrentTimestamp() }
                });
                return;
            }

            Logging("유저 데이터 로드 완료");
            if (_snapShot.ContainsField("nickname"))
                Nickname = _snapShot.GetValue<string>("nickname");
            // 필요 시 여기서 GameManager 등에 로드된 데이터를 전달
            // 예: int highScore = snapshot.GetValue<int>("highScore_classic");
        });
    }

    #endregion

    #region Analytics

    public void LogEvent(string eventName)
    {
        FirebaseAnalytics.LogEvent(eventName);
    }

    public void LogEvent(string eventName, string paramName, string paramValue)
    {
        FirebaseAnalytics.LogEvent(eventName, new Parameter(paramName, paramValue));
    }

    public void LogEvent(string eventName, params Parameter[] parameters)
    {
        FirebaseAnalytics.LogEvent(eventName, parameters);
    }

    /// <summary>
    /// 모드 시작 이벤트. 예: LogModeStart("classic")
    /// </summary>
    public void LogModeStart(string mode)
    {
        FirebaseAnalytics.LogEvent("game_start", new Parameter("mode", mode));
    }

    /// <summary>
    /// 중도 이탈 이벤트. 플레이 시간(초)과 모드를 함께 기록.
    /// </summary>
    public void LogModeQuit(string mode, float playDurationSec, int currentScore)
    {
        FirebaseAnalytics.LogEvent("game_quit",
            new Parameter("mode", mode),
            new Parameter("play_duration_sec", playDurationSec),
            new Parameter("score", currentScore));
    }

    /// <summary>
    /// 정상적으로 게임오버 화면까지 도달했을 때.
    /// </summary>
    public void LogGameOver(string mode, int finalScore)
    {
        FirebaseAnalytics.LogEvent("game_over",
            new Parameter("mode", mode),
            new Parameter("final_score", finalScore));
    }

    #endregion

    #region Crashlytics

    private void InitCrashlytics()
    {
        Crashlytics.ReportUncaughtExceptionsAsFatal = true;
    }

    public void Log(string message)
    {
        Crashlytics.Log(message);
    }

    public void SetCustomKey(string key, string value)
    {
        Crashlytics.SetCustomKey(key, value);
    }

    public void SetCustomKey(string key, int value)
    {
        Crashlytics.SetCustomKey(key, value.ToString());
    }

    #endregion

    #region Messaging (FCM)

    private void InitMessaging()
    {
        FirebaseMessaging.TokenReceived += OnTokenReceived;
        FirebaseMessaging.MessageReceived += OnMessageReceived;
    }

    private void OnTokenReceived(object sender, TokenReceivedEventArgs e)
    {
        Logging($"FCM 토큰 수신: {e.Token}");
        SaveField("fcmToken", e.Token);
    }

    private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
    {
        Logging($"FCM 메시지 수신: {e.Message.Notification?.Title}");
    }

    private void OnDestroy()
    {
        FirebaseMessaging.TokenReceived -= OnTokenReceived;
        FirebaseMessaging.MessageReceived -= OnMessageReceived;
    }

    #endregion
}
