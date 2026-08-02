using Cysharp.Threading.Tasks;
#if UNITY_ANDROID || UNITY_EDITOR
using Firebase;
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

public class FirebaseManager : BaseManager
{
    public bool IsInitialized { get; private set; }
#if UNITY_ANDROID || UNITY_EDITOR
    public bool IsUpdate { get; private set; } = false;
#else
    public bool IsUpdate { get; private set; } = true;
#endif
    public string UserId { get; private set; }

#if UNITY_ANDROID || UNITY_EDITOR
    private FirebaseFirestore _firestore;

    /// <summary>
    /// 반환한 UserData가 Firestore 문서에서 왔는지 여부.
    /// 인증 대기 시간 초과로 PlayerPrefs 기반 로컬 UserData를 돌려준 경우 false로 남으며,
    /// 이때 SaveUser는 로컬 값으로 클라우드 문서를 덮어쓰지 않도록 저장을 거부한다.
    /// </summary>
    private bool _userDocumentLoaded;

    /// <summary>저장 차단 경고를 세션당 한 번만 남기기 위한 플래그.</summary>
    private bool _saveRefusalWarned;
#endif
    private const string USERS_COLLECTION = "users";
    private const string MAIL_COLLECTION = "mail";
    private const string RECEIVER_EMAIL = "oortcloud98@gmail.com";
    private const int MIN_INTERVAL_SECONDS = 60; // 스팸 방지: 최소 발송 간격

    private DateTime _lastSentTimeUtc = DateTime.MinValue;


    private UniTaskCompletionSource<bool> _authTcs;

#if UNITY_ANDROID || UNITY_EDITOR
    /// <summary>
    /// 최초 인증의 완료 신호. 성공·실패·백스톱 타임아웃 등 모든 종료 지점에서 완료된다.
    /// 기기에서의 인증은 Authenticate → RequestServerSideAccess → SignInAndRetrieveData로
    /// 이어지는 3단 콜백 체인이라 완료를 관측할 방법이 없었고, 그래서 이전에는 UserId 필드를
    /// 폴링해야 했다. 이 신호가 그 폴링과 임의의 대기 시간을 대체한다.
    /// </summary>
    private readonly UniTaskCompletionSource<bool> _initialAuth = new UniTaskCompletionSource<bool>();

    /// <summary>최초 인증이 끝날 때까지 기다린다. 성공 여부를 돌려주며 예외를 던지지 않는다.</summary>
    public UniTask<bool> WaitForAuthAsync() => _initialAuth.Task;
#endif

    /// <summary>
    /// 닉네임 설정용 캐시. 랭킹 기록 시 같이 올라감. SetNickname()으로 변경 가능.
    /// </summary>
    public string Nickname { get; private set; } = "Player";

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
                // IsInitialized는 false로 두되 게이트는 연다.
                // 대기가 풀리고 Telemetry/Firestore 호출은 전부 no-op으로 빠진다.
                CompleteInit(ManagerType.Firebase);
                return;
            }
            _firestore = FirebaseFirestore.DefaultInstance;
            Logging("Firebase 초기화 완료");

            InitCrashlytics();
            SignInAuth();
            IsInitialized = true;
            CompleteInit(ManagerType.Firebase);
        });
#else
        Logging("WebGL: PlayerPrefs 기반 로컬 데이터로 초기화");
        IsInitialized = true;
        CompleteInit(ManagerType.Firebase);
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
            _initialAuth.TrySetResult(true);
            return;
        }

        // 저장된 UID로 재로그인 불가능 (익명은 토큰 재사용 안 됨)
        // → PlayerPrefs에 저장된 UID를 Firestore 키로만 활용
        string savedUid = PlayerPrefs.GetString("editor_uid", "");
        if (!string.IsNullOrEmpty(savedUid))
        {
            UserId = savedUid;
            Logging($"[Editor] 저장된 UID 재사용: {UserId}");
            _initialAuth.TrySetResult(true);
            return;
        }
#endif

        // 콜백 체인이 영영 돌아오지 않는 경우(SDK 홀드, 네트워크 대기)를 대비한 백스톱.
        // 정상 경로에서는 아래 콜백들이 먼저 신호를 완료하므로 이 타이머는 지고 끝난다.
        ArmAuthTimeoutAsync().Forget();

        TryPlayGamesAuthentication();
    }

    /// <summary>
    /// 인증 신호가 제한 시간 안에 오지 않으면 실패로 확정한다.
    /// TrySetResult가 true를 돌려준 경우에만 이 타이머가 이긴 것이므로 그때만 경고한다.
    /// </summary>
    private async UniTaskVoid ArmAuthTimeoutAsync()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(AUTH_WAIT_SECONDS), ignoreTimeScale: true);

        if (_initialAuth.TrySetResult(false))
            Warning($"인증 신호가 {AUTH_WAIT_SECONDS}초 안에 오지 않았다. 로컬 데이터로 진행한다.");
    }

    public UniTask<bool> ManuallyAuthenticationAsync()
    {
        if (PlayGamesPlatform.Instance.IsAuthenticated())
            return UniTask.FromResult(true);

        _authTcs = new UniTaskCompletionSource<bool>();
        PlayGamesPlatform.Instance.ManuallyAuthenticate(ProcessAuthentication);
        return _authTcs.Task;
    }

    /// <summary>
    /// PlayGames 인증을 시작한다. 결과는 ProcessAuthentication 콜백으로 돌아오므로
    /// 이 메서드의 반환 시점에는 아직 인증이 끝나지 않았다. 완료는 _initialAuth로 관측한다.
    /// </summary>
    private void TryPlayGamesAuthentication()
    {
        try
        {
            PlayGamesPlatform.Activate();
            PlayGamesPlatform.Instance.Authenticate(ProcessAuthentication);
        }
        catch (Exception e)
        {
            Crashlytics.LogException(e);
            Logging(e.ToString());
            // 예외가 나면 콜백이 오지 않는다. 백스톱을 기다리지 말고 즉시 실패로 확정한다.
            _initialAuth.TrySetResult(false);
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
        }
    }

    private void SignInAuth(Task<AuthResult> task)
    {
        if (task.IsCanceled)
        {
            _authTcs?.TrySetResult(false);
            _initialAuth.TrySetResult(false);
            Error("SignInAndRetrieveDataWithCredentialAsync was canceled.");
            return;
        }
        if (task.IsFaulted)
        {
            _authTcs?.TrySetResult(false);
            _initialAuth.TrySetResult(false);
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

        // TrySetResult가 false를 돌려주면 백스톱 타임아웃이 이미 실패로 확정한 뒤다.
        // 즉 인증이 뒤늦게 성공한 경우이며, 이 세션은 유저 문서를 읽지 못한 채로 남아 있다.
        // 로컬 값으로 클라우드를 덮어쓰지 않도록 저장은 계속 막히며, 병합 정책이 정해지면
        // 이 지점이 복구를 거는 자리다.
        if (!_initialAuth.TrySetResult(true))
            Warning("인증이 백스톱 타임아웃 이후에 완료됐다. 이 세션은 클라우드 저장이 계속 차단된다.");

        Logging($"User signed in successfully: {result.User.DisplayName} ({result.User.UserId})");
    }
#endif

#endregion

    #region Firestore
    /// <summary>
    /// 유저 문서 한 필드만 병합 저장. 예: SaveField("highScore_classic", 15200)
    /// </summary>

    public void SaveUser(UserData user)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        PlayerPrefs.Save();
        if (!IsInitialized || string.IsNullOrEmpty(UserId) || user == null || !_userDocumentLoaded)
        {
            // 이 상태는 세션 내내 유지되므로 setter를 누를 때마다 경고가 쌓인다. 한 번만 남긴다.
            if (!_saveRefusalWarned)
            {
                _saveRefusalWarned = true;
                Warning("Firestore 저장 차단: 초기화/로그인 미완료이거나 유저 문서를 읽지 못했다. "
                      + "이 세션의 변경은 로컬에만 저장된다.");
            }
            return;
        }
        LLogger.Log("Save Firestore");
        var docRef = _firestore.Collection(USERS_COLLECTION).Document(UserId);

        docRef.SetAsync(user, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
                Error($"Firestore 저장 실패 ({UserId}): {task.Exception}");

            user.IsDirty = false;
        });
#else
        PlayerPrefs.Save();
#endif
    }

    private const float AUTH_WAIT_SECONDS = 10f;

    /// <summary>
    /// 유저 문서를 읽어온다. 인증 완료 신호를 기다렸다가, 성공했으면 Firestore 문서를,
    /// 실패했으면 PlayerPrefs 기반 로컬 UserData를 돌려준다. 절대 null을 반환하지 않는다.
    /// </summary>
    public async UniTask<UserData> LoadUserAsync()
    {
#if UNITY_ANDROID || UNITY_EDITOR
        // IsInitialized가 false면 SignInAuth 자체가 호출되지 않아 신호가 완료되지 않는다.
        // 이 검사가 그 경우의 무한 대기를 막는다.
        if (IsInitialized)
        {
            bool authenticated = await WaitForAuthAsync();

            if (authenticated && !string.IsNullOrEmpty(UserId))
                return await FetchUserDocumentAsync();

            Warning("인증에 실패했다. 로컬 데이터로 진행한다.");
        }
#endif
        Logging("로컬 PlayerPrefs 기반 UserData로 진행");
        return new UserData();
    }

#if UNITY_ANDROID || UNITY_EDITOR
    private async UniTask<UserData> FetchUserDocumentAsync()
    {
        Logging("유저 데이터 로드 시작");
        var docRef = _firestore.Collection(USERS_COLLECTION).Document(UserId);
        try
        {
            var snapshot = await docRef.GetSnapshotAsync().AsUniTask();

            // SaveUser의 가드가 이 플래그를 보므로, 아래에서 저장하기 전에 세운다.
            _userDocumentLoaded = true;

            if (snapshot.Exists)
            {
                var remote = snapshot.ConvertTo<UserData>();
                var local = new UserData();   // PlayerPrefs 기반 스냅샷

                // 이전 실행이 오프라인이었거나 인증이 늦어 클라우드 저장이 막혔다면, 그 세션의
                // 변경은 PlayerPrefs에만 남아 있고 LastPlayed가 원격보다 최신이다. 그대로
                // 원격을 채택하면 그 진행이 사라지므로, 최종 플레이 시각으로 최신본을 고른다.
                if (local.LastPlayed > remote.LastPlayed)
                {
                    // createdAt은 로컬에 기록하지 않으므로 원격 값을 유지한다.
                    // 그러지 않으면 MergeAll 저장이 기본값으로 덮어쓴다.
                    local.CreatedAt = remote.CreatedAt;

                    Warning($"로컬 데이터가 더 최신이다(로컬 {local.LastPlayed}, 원격 {remote.LastPlayed}). "
                          + "로컬을 채택하고 클라우드에 반영한다.");
                    SaveUser(local);
                    return local;
                }

                Logging("유저 데이터 로드 완료");
                return remote;
            }

            Logging("신규 유저, 초기 문서 생성");
            var created = new UserData();
            created.CreatedAt = Timestamp.GetCurrentTimestamp();
            created.TouchLastPlayed();
            SaveUser(created);
            return created;
        }
        catch (Exception e)
        {
            // 역직렬화 도중 던지면 플래그가 이미 선 채로 여기 떨어질 수 있다. 문서를 신뢰할 수
            // 없는 상태이므로 되돌려, 로컬 값이 클라우드를 덮어쓰지 못하게 저장 차단을 유지한다.
            _userDocumentLoaded = false;
            Crashlytics.LogException(e);
            Error(e.ToString());
            return new UserData();
        }
    }
#endif

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
#else
    private void InitCrashlytics()
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
    public async UniTask ReportScore(int score)
    {
        if (await IsAuthenticated())
            PlayGamesPlatform.Instance.ReportScore(score, GPGSIds.leaderboard_high_score, ResultReportLeaderboard);
    }

    private void ResultReportLeaderboard(bool isComplete)
    {
        Logging($"리더보드 보고 결과: {isComplete}");
    }

    public async UniTask ShowLeaderboardUI()
    {
        if (await IsAuthenticated())
            PlayGamesPlatform.Instance.ShowLeaderboardUI(GPGSIds.leaderboard_high_score);
    }
#else
    public UniTask<bool> IsAuthenticated() => UniTask.FromResult(false);

    public UniTask ReportScore(int score) => UniTask.CompletedTask;

    public UniTask ShowLeaderboardUI() => UniTask.CompletedTask;
#endif
    #endregion

    #region RemoteConfig

    private const string MIN_VERSION_KEY = "min_required_version";
    private static string PLAY_STORE_URL => $"https://play.google.com/store/apps/details?id={Application.identifier}";

    private PrefabManager m_prefabManager;

    public FirebaseManager(ManagerInitTracker tracker, PrefabManager prefabManager) : base(tracker)
    {
        m_prefabManager = prefabManager;
        InitializeFirebase();
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
            Crashlytics.LogException(e);
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
        // 의존성 실패 분기도 CompleteInit(Firebase)로 게이트를 열기 때문에, Firebase가
        // 초기화되지 않은 상태로도 게임이 여기까지 도달할 수 있다. 그 경우 _firestore가
        // null이라 역참조하면 NRE가 나는데, 아래 catch의 Crashlytics.LogException도
        // 같은 분기에서 InitCrashlytics()를 거치지 않아 예외가 밖으로 샌다.
        if (!IsInitialized)
        {
            Warning("Firebase가 초기화되지 않아 문의를 전송할 수 없다.");
            return InquiryResult.Fail(GameTextData.INQURIY_SEND_FAIL_4);
        }

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
            Crashlytics.LogException(e);
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
