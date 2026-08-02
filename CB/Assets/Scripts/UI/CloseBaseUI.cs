using VContainer;

/// <summary>
/// 닫기 버튼을 가진 UI의 베이스. 백키 스택 배선을 흡수해 파생 클래스가
/// InputManager를 직접 참조하지 않게 한다.
/// </summary>
public class CloseBaseUI : BaseUI
{
    protected InputManager m_input;
    private bool _blocking;

    // VContainer는 상속 계층을 타고 올라가며 [Inject] 메서드를 수집하므로
    // 파생 클래스의 Construct와 별개로 이 메서드도 호출된다.
    // 주의: 호출 순서는 파생 → 베이스다. 파생 Construct 안에서 m_input을 쓰면 안 된다.
    [Inject]
    public void ConstructBaseUI(InputManager input)
    {
        m_input = input;
    }

    public override void Init()
    {
        base.Init();
        if (!_blocking) { m_input.PushInputBlock(); _blocking = true; }
        m_input.PushBackHandler(OnBackKey);
    }

    public override void Close()
    {
        m_input.PopBackHandler(OnBackKey);
        if (_blocking) { m_input.PopInputBlock(); _blocking = false; }
        base.Close();
    }

    private void OnDestroy()
    {
        m_input?.PopBackHandler(OnBackKey);
        if (_blocking) { m_input?.PopInputBlock(); _blocking = false; }
        OnDestroyed();
    }

    protected virtual void OnDestroyed() { }

    private void OnBackKey()
    {
        Close();
    }
}
