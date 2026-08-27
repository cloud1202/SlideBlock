using LayonCraft.GameKit;
using UnityEngine;
using VContainer;

public class BootCheck : MonoBehaviour
{
    private PrefabManager m_prefab;
    private InputManager m_input;

    [Inject]
    public void Construct(PrefabManager prefab, InputManager input)
    {
        m_prefab = prefab;
        m_input = input;
        Test();
    }

    private void Test()
    {
        LLogger.Log($"캔버스: {(m_prefab.MainCanvas != null ? "OK" : "아직 없음")}");
        LLogger.Log($"카메라: {(m_prefab.MainCamera != null ? "OK" : "아직 없음")}");

        m_input.PushBackHandler(() => LLogger.Log("아래 핸들러"));
        m_input.PushBackHandler(() => LLogger.Log("위 핸들러"));
        // Esc를 누르면 "위 핸들러"만 찍혀야 한다.
    }
}
