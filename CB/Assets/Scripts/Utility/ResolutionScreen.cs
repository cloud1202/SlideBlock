using System;
using System.Linq;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;


public static class ResolutionScreen
{
    // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式
    // Constants
    // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式
    public const float REF_WIDTH = 1080f;
    public const float REF_HEIGHT = 1920f;
    public const float REF_ASPECT = REF_WIDTH / REF_HEIGHT;
    public const float ORTHOGRAPHIC_SIZE = 5f;
    public const float MATCH_WIDTH_OR_HEIGHT = 0.5f;

    private static float _cachedWidth;
    private static float _cachedHeight;
    private static float _logBase = 2f;

    /// <summary>
    /// changeWidth, changeHeig
    /// </summary>
    private static Action<float, float, float> _onChangeResolutionEvent;

    private static float _scaleFactor;

    public static void InitResolution()
    {
        RegisterPlayerLoop();
        


#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
    }
#if UNITY_EDITOR
    private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
        {
            UnregisterPlayerLoop();
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }
    }
#endif
    private static void RegisterPlayerLoop()
    {
        _cachedWidth = REF_WIDTH;
        _cachedHeight = REF_HEIGHT;

        float logWidth = Mathf.Log(Screen.width / REF_WIDTH, _logBase);
        float logHeight = Mathf.Log(Screen.height / REF_HEIGHT, _logBase);
        float logWeightedAverage = Mathf.Lerp(logWidth, logHeight, MATCH_WIDTH_OR_HEIGHT);
        _scaleFactor = Mathf.Pow(_logBase, logWeightedAverage);
        var loop = PlayerLoop.GetCurrentPlayerLoop();

        for (int i = 0; i < loop.subSystemList.Length; i++)
        {
            if (loop.subSystemList[i].type == typeof(PostLateUpdate))
            {
                var subList = loop.subSystemList[i].subSystemList;

                var newSubList = new PlayerLoopSystem[subList.Length + 1];
                Array.Copy(subList, newSubList, subList.Length);
                newSubList[subList.Length] = new PlayerLoopSystem
                {
                    type = typeof(ResolutionScreen),
                    updateDelegate = ChangeResolution,
                };
                loop.subSystemList[i].subSystemList = newSubList;
                break;
            }
        }

        PlayerLoop.SetPlayerLoop(loop);
    }

    private static void UnregisterPlayerLoop()
    {
        var loop = PlayerLoop.GetCurrentPlayerLoop();

        for (int i = 0; i < loop.subSystemList.Length; i++)
        {
            if (loop.subSystemList[i].type == typeof(PostLateUpdate))
            {
                var subList = loop.subSystemList[i].subSystemList;
                var newSubList = subList
                    .Where(s => s.type != typeof(ResolutionScreen))
                    .ToArray();
                loop.subSystemList[i].subSystemList = newSubList;
                break;
            }
        }

        PlayerLoop.SetPlayerLoop(loop);
    }
    private static void ChangeResolution()
    {
        if (Screen.width != _cachedWidth || Screen.height != _cachedHeight)
        {
            _cachedWidth = Screen.width;
            _cachedHeight = Screen.height;

            float logWidth = Mathf.Log(Screen.width / REF_WIDTH, _logBase);
            float logHeight = Mathf.Log(Screen.height / REF_HEIGHT, _logBase);
            float logWeightedAverage = Mathf.Lerp(logWidth, logHeight, MATCH_WIDTH_OR_HEIGHT);
            _scaleFactor = Mathf.Pow(_logBase, logWeightedAverage);

            _onChangeResolutionEvent?.Invoke(_cachedWidth, _cachedHeight, _scaleFactor);
        }
    }

    public static void Subscribe(Action<float, float, float> callback)
    {
        _onChangeResolutionEvent += callback;
        callback.Invoke(_cachedWidth, _cachedHeight, _scaleFactor);
    }

    public static void Unsubscribe(Action<float, float, float> callback)
    {
        _onChangeResolutionEvent -= callback;
    }
}
