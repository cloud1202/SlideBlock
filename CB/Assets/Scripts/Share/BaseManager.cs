using Cysharp.Threading.Tasks;
using VContainer;

public class BaseManager
{
    private ManagerInitTracker m_tracker;
    [Inject]
    public void Construct(ManagerInitTracker tracker)
    {
        m_tracker = tracker;
    }

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
