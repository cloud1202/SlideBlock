using Cysharp.Threading.Tasks;
#if UNITY_ANDROID || UNITY_EDITOR
using Firebase;
using Firebase.Analytics;
using Firebase.Auth;
using Firebase.Crashlytics;
using Firebase.Extensions;
using Firebase.Firestore;
using Firebase.Messaging;
using Firebase.RemoteConfig;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using VContainer;

[ManagerOrder(1)]
public class FirebaseManager : SingletonInstance<FirebaseManager>, IManager
{
    public bool IsInitialized { get; private set; }
#if UNITY_ANDROID || UNITY_EDITOR
    public bool IsUpdate { get; private set; } = false;
#else
    public bool IsUpdate { get; private set; } = true;
#endif
    public bool IsLoadData => _user != null;
    private UserData _user = null;

    public string UserId { get; private set; }

#if UNITY_ANDROID || UNITY_EDITOR
    private FirebaseFirestore _firestore;
#endif
    private const string USERS_COLLECTION = "users";
    private const string MAIL_COLLECTION = "mail";
    private const string RECEIVER_EMAIL = "oortcloud98@gmail.com";
    private const int MIN_INTERVAL_SECONDS = 60; // 스팸 방지: 최소 발송 간격

    private DateTime _lastSentTimeUtc = DateTime.MinValue;


    private UniTaskCompletionSource<bool> _authTcs;
    /// <summary>
    /// 닉네임 설정용 캐시. 랭킹 기록 시 같이 올라감. SetNickname()으로 변경 가능.
    /// </summary>
    public string Nickname { get; private set; } = "Player";
    public int ClassicScore
    {
        get
        {
            return _user.ClassicScore;
        }
        set
        {
            PlayerPrefs.SetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.HighScore_Classic)], value);
            _user.ClassicScore = value;
            _user.IsDirty = true;
            SaveUserData();
            TryReportLeaderboard().Forget();
        }
    }
    public bool IsBGMOn
    {
        get
        {
            return _user.IsBGMOn;
        }
        set
        {
            PlayerPrefs.SetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.IsBGMOn)], value.GetHashCode());
            if (!value) LogEvent("bgm_off");
            _user.IsBGMOn = value;
            _user.IsDirty = true;
            SaveUserData();
        }
    }
    public bool IsSFXOn
    {
        get
        {
            return _user.IsSFXOn;
        }
        set
        {
            PlayerPrefs.SetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.IsSFXOn)], value.GetHashCode());
            if (!value) LogEvent("sfx_off");
            _user.IsSFXOn = value;
            _user.IsDirty = true;
            SaveUserData();
        }
    }

    public bool IsSymbolOn
    {
        get
        {
            return _user.IsSymbolOn;
        }
        set
        {
            PlayerPrefs.SetInt(SaveFieldData.Fields[EnumConverter.Enum32ToInt(SaveFieldType.IsSymbolOn)], value.GetHashCode());

            if (value) LogEvent("symbol_on");
            _user.IsSymbolOn = value;
            _user.IsDirty = true;
            SaveUserData();
        }
    }


    public override void Init()
    {
        base.Init();
        InitializeFirebase();
    }

    #region Core Initialization

    private void InitializeFirebase()
    {
#if UNITY_ANDROID || UNITY_EDITOR
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var status = task.Result;
            if (status != DependencyStatus.Available)
            {
                Error($"Firebase 의존성 문제: {status}");
                return;
            }
            _firestore = FirebaseFirestore.DefaultInstance;
            Logging("Firebase 초기화 완료");

            InitCrashlytics();
            //InitMessaging();
            SignInAuth();
            IsInitialized = true;
        });
#else
        _user = new UserData();
        Logging("WebGL: PlayerPrefs 기반 로컬 데이터로 초기화");
        IsInitialized = true;
#endif
    }

    #endregion

    #region Authentication

#if UNITY_ANDROID || UNITY_EDITOR
    private void SignInAuth()
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
#if UNITY_EDITOR
        Nickname = "TestPlayer";
        // 에디터: 기존 로그인 세션 재사용 시도
        if (auth.CurrentUser != null)
        {
            UserId = auth.CurrentUser.UserId;
            Logging($"[Editor] 기존 세션 재사용: {UserId}");
            Crashlytics.SetUserId(UserId);
            LoadUserData().Forget();
            return;
        }

        // 저장된 UID로 재로그인 불가능 (익명은 토큰 재사용 안 됨)
        // → PlayerPrefs에 저장된 UID를 Firestore 키로만 활용
        string savedUid = PlayerPrefs.GetString("editor_uid", "");
        if (!string.IsNullOrEmpty(savedUid))
        {
            UserId = savedUid;
            Logging($"[Editor] 저장된 UID 재사용: {UserId}");
            LoadUserData().Forget();
            return;
        }
#endif

        if (TryPlayGamesAuthentication())
            return;
    }

    public UniTask<bool> ManuallyAuthenticationAsync()
    {
        if (PlayGamesPlatform.Instance.IsAuthenticated())
            return UniTask.FromResult(true);

        _authTcs = new UniTaskCompletionSource<bool>();
        PlayGamesPlatform.Instance.ManuallyAuthenticate(ProcessAuthentication);
        return _authTcs.Task;
    }

    private bool TryPlayGamesAuthentication()
    {
        try
        {
            PlayGamesPlatform.Activate();
            PlayGamesPlatform.Instance.Authenticate(ProcessAuthentication);
            return PlayGamesPlatform.Instance.IsAuthenticated();
        }
        catch (Exception e)
        {
            LogError(e);
            Logging(e.ToString());
            return false;
        }
    }

    internal void ProcessAuthentication(SignInStatus status)
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        if (status == SignInStatus.Success)
        {
            Logging("PlayGames Login Success");
            PlayGamesPlatform.Instance.RequestServerSideAccess(
                false,
                (string authCode) =>
                {
                    Credential credential =
                        PlayGamesAuthProvider.GetCredential(authCode);
                    auth.SignInAndRetrieveDataWithCredentialAsync(credential).ContinueWithOnMainThread(SignInAuth);
                });

            Nickname = PlayGamesPlatform.Instance.localUser.userName;

        }
        else
        {
            Logging("PlayGames Login Failed");
            auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(SignInAuth);
            Nickname = string.Empty;
            // Disable your integration with Play Games Services or show a login
            // button to ask users to sign-in. Clicking it should call
            // PlayGamesPlatform.Instance.ManuallyAuthenticate(ProcessAuthentication).
        }
    }

    private void SignInAuth(Task<AuthResult> task)
    {
        if (task.IsCanceled)
        {
            _authTcs?.TrySetResult(false);
            Error("SignInAndRetrieveDataWithCredentialAsync was canceled.");
            return;
        }
        if (task.IsFaulted)
        {
            _authTcs?.TrySetResult(false);
            Error("SignInAndRetrieveDataWithCredentialAsync encountered an error: " + task.Exception);
            return;
        }

#if UNITY_EDITOR
        // 에디터에서 첫 로그인 시 저장
        PlayerPrefs.SetString("editor_uid", UserId);
        PlayerPrefs.Save();
        Logging($"[Editor] UID 저장 완료");
#endif
        AuthResult result = task.Result;
        UserId = result.User.UserId;
        Crashlytics.SetUserId(UserId);

        _authTcs?.TrySetResult(!string.IsNullOrEmpty(Nickname));
        LoadUserData().Forget();
        Logging($"User signed in successfully: {result.User.DisplayName} ({result.User.UserId})");
    }
#endif

#endregion

    #region Firestore
    /// <summary>
    /// 유저 문서 한 필드만 병합 저장. 예: SaveField("highScore_classic", 15200)
    /// </summary>

    public void SaveUserData()
    {
#if UNITY_ANDROID || UNITY_EDITOR
        PlayerPrefs.Save();
        if (!IsInitialized || string.IsNullOrEmpty(UserId) || !IsLoadData)
        {
            Warning("Firestore 저장 실패: 아직 초기화/로그인되지 않음");
            return;
        }
        LLogger.Log("Save Firestore");
        var docRef = _firestore.Collection(USERS_COLLECTION).Document(UserId);

        docRef.SetAsync(_user, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
                Error($"Firestore 저장 실패 ({UserId}): {task.Exception}");

            _user.IsDirty = false;
        });
#else
        PlayerPrefs.Save();
#endif
    }

#if UNITY_ANDROID || UNITY_EDITOR
    async private UniTask LoadUserData()
    {
        Logging("유저 데이터 로드 시작");
        var docRef = _firestore.Collection(USERS_COLLECTION).Document(UserId);
        try
        {
            var snapshot = await docRef.GetSnapshotAsync().AsUniTask();

            if (snapshot.Exists)
            {
                _user = snapshot.ConvertTo<UserData>();
            }
            else
            {
                Logging("신규 유저, 초기 문서 생성");
                _user = new UserData();
                _user.CreatedAt = Timestamp.GetCurrentTimestamp();
                SaveUserData();
            }
            Logging("유저 데이터 로드 완료");
            // 필요 시 여기서 GameManager 등에 로드된 데이터를 전달
            // 예: int highScore = snapshot.GetValue<int>("highScore_classic");
        }
        catch(Exception e)
        {
            LogError(e);
            Error(e.ToString());
        }
    }
#endif

    #endregion

    #region Analytics

#if UNITY_ANDROID || UNITY_EDITOR
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
#else
    public void LogEvent(string eventName)
    {
        WebAnalyticsBridge.LogEvent(eventName);
    }

    public void LogEvent(string eventName, string paramName, string paramValue)
    {
        WebAnalyticsBridge.LogEvent(eventName, paramName, paramValue);
    }
#endif

    /// <summary>
    /// 모드 시작 이벤트. 예: LogModeStart("classic")
    /// </summary>
    public void LogModeStart(string mode)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        LogEvent("game_start", new Parameter("mode", mode));
#else
        WebAnalyticsBridge.LogModeStart(mode);
#endif
    }

    /// <summary>
    /// 중도 이탈 이벤트. 플레이 시간(초)과 모드를 함께 기록.
    /// </summary>
    public void LogModeQuit(string mode, float playDurationSec, int currentScore)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        LogEvent("game_quit",
            new Parameter("mode", mode),
            new Parameter("play_duration_sec", playDurationSec),
            new Parameter("score", currentScore));
#else
        WebAnalyticsBridge.LogModeQuit(mode, playDurationSec, currentScore);
#endif
    }

    public void LogModePause(string mode, float playDurationSec, int currentScore)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        LogEvent("game_pause",
            new Parameter("mode", mode),
            new Parameter("play_duration_sec", playDurationSec),
            new Parameter("score", currentScore));
#else
        WebAnalyticsBridge.LogModePause(mode, playDurationSec, currentScore);
#endif
    }

    /// <summary>
    /// 정상적으로 게임오버 화면까지 도달했을 때.
    /// </summary>
    public void LogGameOver(string mode, int finalScore, int maxCombo)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        LogEvent("game_over",
            new Parameter("mode", mode),
            new Parameter("final_score", finalScore),
            new Parameter("max_combo", maxCombo)
            );
#else
        WebAnalyticsBridge.LogGameOver(mode, finalScore, maxCombo);
#endif
    }

    #endregion

    #region Crashlytics

#if UNITY_ANDROID || UNITY_EDITOR
    private void InitCrashlytics()
    {
        Crashlytics.ReportUncaughtExceptionsAsFatal = true;
#if DEVELOP
    Crashlytics.SetCustomKey("build_type", "DEVELOP");
#else
        Crashlytics.SetCustomKey("build_type", "RELEASE");
#endif
    }

    public void Log(string message)
    {
        Crashlytics.Log(message);
    }

    public void LogError(Exception e)
    {
        Crashlytics.LogException(e);
    }

    public void SetCustomKey(string key, string value)
    {
        Crashlytics.SetCustomKey(key, value);
    }
#else
    private void InitCrashlytics()
    {
    }

    public void Log(string message)
    {
    }

    public void LogError(Exception e)
    {
    }

    public void SetCustomKey(string key, string value)
    {
    }
#endif
    #endregion

#if UNITY_ANDROID || UNITY_EDITOR
    #region Messaging (FCM)

    private void InitMessaging()
    {
        FirebaseMessaging.TokenReceived += OnTokenReceived;
        FirebaseMessaging.MessageReceived += OnMessageReceived;
    }

    private void OnTokenReceived(object sender, TokenReceivedEventArgs e)
    {
        Logging($"FCM 토큰 수신: {e.Token}");
        //SaveField("fcmToken", e.Token);
    }

    private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
    {
        Logging($"FCM 메시지 수신: {e.Message.Notification?.Title}");
    }

    private void OnDestroy()
    {
        //FirebaseMessaging.TokenReceived -= OnTokenReceived;
        //FirebaseMessaging.MessageReceived -= OnMessageReceived;
    }

    #endregion
#endif
    #region PlayGames
#if UNITY_ANDROID || UNITY_EDITOR
    public async UniTask<bool> IsAuthenticated()
    {
        if (!PlayGamesPlatform.Instance.IsAuthenticated())
        {
            bool success = await ManuallyAuthenticationAsync();
            if (!success)
            {
                Warning("인증 실패");
                return false;
            }
        }
        return true;
    }
    private async UniTask TryReportLeaderboard()
    {
        if(await IsAuthenticated())
            PlayGamesPlatform.Instance.ReportScore(ClassicScore, GPGSIds.leaderboard_high_score, ResultReportLeaderboard);
    }

    private void ResultReportLeaderboard(bool isComplete)
    {
        LogEvent("report_leaderboard", "is_complete", isComplete.ToString());
    }

    public async UniTask ShowLeaderboardUI()
    {
        if (await IsAuthenticated())
            PlayGamesPlatform.Instance.ShowLeaderboardUI(GPGSIds.leaderboard_high_score);
    }
#else
    public UniTask<bool> IsAuthenticated() => UniTask.FromResult(false);

    private UniTask TryReportLeaderboard() => UniTask.CompletedTask;

    public UniTask ShowLeaderboardUI() => UniTask.CompletedTask;
#endif
    #endregion

    #region RemoteConfig

    private const string MIN_VERSION_KEY = "min_required_version";
    private static string PLAY_STORE_URL => $"https://play.google.com/store/apps/details?id={Application.identifier}";

    private PrefabManager m_prefabManager;
    [Inject]
    public void Construct(PrefabManager prefabManager)
    {
        m_prefabManager = prefabManager;
    }
    public async UniTask CheckForForceUpdateAsync()
    {
#if UNITY_ANDROID || UNITY_EDITOR
        try
        {
            var remoteConfig = FirebaseRemoteConfig.DefaultInstance;
            await remoteConfig.SetDefaultsAsync(new System.Collections.Generic.Dictionary<string, object>
            {
                { MIN_VERSION_KEY, "0.0.0" }
            }).AsUniTask();
            await remoteConfig.SetConfigSettingsAsync(new ConfigSettings { MinimumFetchIntervalInMilliseconds = 0 }).AsUniTask();
            await remoteConfig.FetchAndActivateAsync().AsUniTask();

            var minVersion = new Version(remoteConfig.GetValue(MIN_VERSION_KEY).StringValue);
            var currentVersion = new Version(Application.version);

            if (currentVersion < minVersion)
            {
                Logging($"강제 업데이트 필요: 현재 {currentVersion} < 최소 {minVersion}");
                await ShowForceUpdatePopupAsync();
            }
            else
                IsUpdate = true;
        }
        catch (Exception e)
        {
            LogError(e);
            Warning($"강제 업데이트 체크 실패, 통과 처리: {e}");
            IsUpdate = true;
        }
#endif
    }

    private async UniTask ShowForceUpdatePopupAsync()
    {
        await UniTask.Yield();

        var popup = await m_prefabManager.InstantiateDynamicUI<IPopupQuestion>(PrefabData.PopupQuestionUI);
        popup.SetNoticeContent(GameTextData.POPUP_UPDATE_REQUIRED);
        popup.RegistQuestionAction(
            onClickYesAction: () =>
            {
                Application.OpenURL(PLAY_STORE_URL);
                ShowForceUpdatePopupAsync().Forget();
            },
#if UNITY_EDITOR
            onClickNoAction: () => UnityEditor.EditorApplication.isPlaying = false
#else
            onClickNoAction: Application.Quit
#endif
        );
    }

#endregion

    #region Public API

    /// <summary>
    /// 문의 내용을 전송한다.
    /// </summary>
    /// <param name="content">유저가 입력한 문의 내용</param>
    /// <param name="userEmail">답장을 받을 유저 이메일 (선택, 없으면 익명)</param>
    public async UniTask<InquiryResult> SendInquiryAsync(string content, string userEmail = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return InquiryResult.Fail(GameTextData.INQURIY_SEND_FAIL_1);
        }

        if (content.Length > 2000)
        {
            return InquiryResult.Fail(GameTextData.INQURIY_SEND_FAIL_2);
        }

        // 연타/스팸 방지
        var elapsed = (DateTime.UtcNow - _lastSentTimeUtc).TotalSeconds;
        if (elapsed < MIN_INTERVAL_SECONDS)
        {
            var remain = MIN_INTERVAL_SECONDS - (int)elapsed;
            return InquiryResult.Fail(GameTextData.INQURIY_SEND_FAIL_3);
        }

#if UNITY_ANDROID || UNITY_EDITOR
        try
        {
            var body = BuildEmailBody(content, userEmail);

            var docData = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "to", RECEIVER_EMAIL },
                    { "message", new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "subject", $"[Slide Block] 문의가 도착했습니다" },
                            { "text", body }
                        }
                    }
                };

            await _firestore.Collection(MAIL_COLLECTION).AddAsync(docData).AsUniTask();

            _lastSentTimeUtc = DateTime.UtcNow;
            return InquiryResult.Success();
        }
        catch (Exception e)
        {
            LogError(e);
            Debug.LogError($"[InquiryManager] 문의 전송 실패: {e}");
            return InquiryResult.Fail(GameTextData.INQURIY_SEND_FAIL_4);
        }
#else
        Warning("웹 빌드에서는 문의 전송이 지원되지 않습니다.");
        return InquiryResult.Fail(GameTextData.INQURIY_SEND_FAIL_4);
#endif
    }

    #endregion

    #region Private Helpers

#if UNITY_ANDROID || UNITY_EDITOR
    private string BuildEmailBody(string content, string userEmail)
    {
        var deviceInfo = $"{SystemInfo.deviceModel} / OS: {SystemInfo.operatingSystem}";
        var appVersion = Application.version;
        var replyTo = string.IsNullOrEmpty(userEmail) ? "(미입력)" : userEmail;

        return
            $"문의 내용:\n{content}\n\n" +
            $"---\n" +
            $"답장 받을 이메일: {replyTo}\n" +
            $"앱 버전: {appVersion}\n" +
            $"기기 정보: {deviceInfo}\n" +
            $"전송 시각(UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
    }
#endif

    #endregion
}
