using UnityEngine;

public class BaseUI : MonoBehaviour, IBaseUI
{
    public virtual void Init()
    {
        gameObject.SetActive(true);
    }

    public virtual void Close()
    {
        gameObject.SetActive(false);
    }
}
