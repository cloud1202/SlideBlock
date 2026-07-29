using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public class ManagerInitTracker
{
    private readonly HashSet<ManagerType> _ready = new HashSet<ManagerType>();
    public void MarkReady(ManagerType type) => _ready.Add(type);

    public bool IsReady(ManagerType type) => _ready.Contains(type);

    public UniTask WaitUntilReady(ManagerType type)
        => UniTask.WaitUntil(() => IsReady(type));

    public UniTask WaitUntilAnyReady(params ManagerType[] types)
        => UniTask.WaitUntil(() => types.All(IsReady));
}
