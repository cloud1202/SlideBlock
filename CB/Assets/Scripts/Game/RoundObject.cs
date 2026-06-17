using UnityEngine;

public class RoundObject : MonoBehaviour, IRoundObject
{
    protected IRound _roundManager;
    public virtual void Init()
    {

    }
    public void SetRoundManager(IRound roundManager)
    {
        _roundManager = roundManager;
    }
}
