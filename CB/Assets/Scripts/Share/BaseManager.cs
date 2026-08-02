using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

public class BaseManager : IInitializable
{
    private ManagerInitTracker m_tracker;

    public BaseManager(ManagerInitTracker tracker)
    {
        LLogger.Log("Construct BaseManager");
        m_tracker = tracker;
    }

    // VContainer는 RegisterEntryPoint로 등록된 타입 중 엔트리포인트 마커 인터페이스를
    // 구현한 것만 즉시 생성(resolve)한다. 실제 초기화는 생성자에서 이미 끝나므로 빈 구현으로 둔다.
    public virtual void Initialize() { }

    protected void CompleteInit(ManagerType type) => m_tracker.MarkReady(type);

    protected async UniTask CheckedManagers(params ManagerType[] types) => await m_tracker.WaitUntilAnyReady(types);

    protected void Logging(string log)
    {
        LLogger.Log(log, color: Colors.Yellow, skipFrames: 2);
    }
    protected void Warning(string log)
    {
        LLogger.Log(log, level: LLogger.LogLevel.Warning, skipFrames: 2);
    }

    protected void Error(string log)
    {
        LLogger.Log(log, level: LLogger.LogLevel.Error, skipFrames: 2);
    }
}
