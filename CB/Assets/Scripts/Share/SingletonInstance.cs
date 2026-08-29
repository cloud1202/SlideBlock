using UnityEngine;

public class SingletonInstance<T> : MonoBehaviour
    where T : Component
{
    private static T m_instance;
    public static T Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = FindAnyObjectByType<T>();

                if (m_instance == null)
                {
                    Debug.Log($"Created instance Manager of: {typeof(T)}");
                    GameObject go = new GameObject();
                    go.name = $"{typeof(T)}(Singleton)";
                    m_instance = go.AddComponent<T>();
                    DontDestroyOnLoad(go);
                }
            }


            return m_instance;
        }
    }
    
    public static bool IsCreatedInstance() => m_instance != null;

    private void Awake()
    {
        Init();
    }

    public virtual void Init()
    {
        if (m_instance == null)
        {
            m_instance = FindAnyObjectByType<T>();
            DontDestroyOnLoad(m_instance.gameObject);
        }
        else
            Destroy(gameObject);
    }

    protected void Logging(string log)
    {
        LLogger.Log(log,color: Colors.Yellow, skipFrames:2);
    }
    protected void Warning(string log)
    {
        LLogger.Log(log, level:LLogger.LogLevel.Warning, skipFrames: 2);
    }

    protected void Error(string log)
    {
        LLogger.Log(log, level: LLogger.LogLevel.Error, skipFrames: 2);
    }
}
