using VContainer;

/// <summary>
/// 모달 UI의 베이스. 백키 스택과 입력 차단 배선을 흡수해 파생 클래스가
/// InputManager를 직접 참조하지 않게 한다.
/// <para>
/// 이 클래스를 상속한다는 것은 "백키로 닫을 수 있다"와 "열려 있는 동안 게임 진행이
/// 막힌다"를 동시에 뜻한다. 두 성질은 분리되지 않는다 — 백키로 닫히는 UI는 곧 모달이고,
/// 모달이 떠 있는 동안 보드가 움직이면 안 된다. 그래서 Init()에서 백키 핸들러와
/// 입력 차단을 함께 밀고, Close()에서 함께 뺀다.
/// 게임 진행을 막으면 안 되는 UI라면 이 클래스가 아니라 BaseUI를 상속해야 한다.
/// </para>
/// <para>
/// ⚠️ 파생 클래스는 OnDestroy()를 선언하지 말 것. 정리 코드는 반드시
/// <see cref="OnDestroyed"/>를 오버라이드해서 넣는다. private 멤버는 상속되지 않아
/// 파생이 OnDestroy()를 재선언해도 컴파일 경고가 뜨지 않고, Unity는 최파생 선언만
/// 호출하므로 백키/입력차단 정리가 조용히 사라진다. OnDestroyed를 abstract로 둔 이유가
/// 이것이다 — 상속하는 순간 컴파일러가 이 파일을 열어보게 만든다.
/// </para>
/// </summary>
public abstract class CloseBaseUI : BaseUI
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

    /// <summary>
    /// 파괴 시 정리 훅. 파생 클래스는 OnDestroy() 대신 이것을 오버라이드한다.
    /// 정리할 게 없으면 빈 본문으로 둔다.
    /// </summary>
    protected abstract void OnDestroyed();

    private void OnBackKey()
    {
        Close();
    }
}
