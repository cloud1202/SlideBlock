using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

public class InputManager : BaseManager
{
    private PlayerInput _inputHandler;
    private readonly List<Action> _backHandlers = new List<Action>();
    private int _blockDepth;

    public InputManager(ManagerInitTracker tracker) : base(tracker)
    {
        LLogger.Log("InputManager");
        _inputHandler = new PlayerInput();
        _inputHandler.Player.Enable();
        SubscribeToInputHandler(InputType.Game_Exit, perform: OnBackKeyPerformed);
        CompleteInit(ManagerType.Input);
    }

    #region 백키 스택

    /// <summary>
    /// 백키 핸들러를 스택 최상단에 올린다. 이미 있으면 최상단으로 끌어올린다.
    /// </summary>
    public void PushBackHandler(Action handler)
    {
        if (handler == null)
            return;

        _backHandlers.Remove(handler);
        _backHandlers.Add(handler);
    }

    /// <summary>
    /// 백키 핸들러를 제거한다. 최상단이 아니어도 안전하며, 없으면 아무 일도 하지 않는다.
    /// </summary>
    public void PopBackHandler(Action handler)
    {
        if (handler == null)
            return;

        _backHandlers.Remove(handler);
    }

    private void OnBackKeyPerformed(CallbackContext context)
    {
        if (_backHandlers.Count == 0)
            return;

        _backHandlers[^1].Invoke();
    }

    #endregion

    #region 입력 차단 스택

    public void PushInputBlock()
    {
        _blockDepth++;
        ApplyInputBlock();
    }

    public void PopInputBlock()
    {
        if (_blockDepth > 0)
            _blockDepth--;

        ApplyInputBlock();
    }

    private void ApplyInputBlock()
    {
        if (_blockDepth > 0)
        {
            _inputHandler.Player.Click.Disable();
            _inputHandler.Player.Point.Disable();
        }
        else
        {
            _inputHandler.Player.Click.Enable();
            _inputHandler.Player.Point.Enable();
        }
    }

    #endregion

    public void SubscribeToInputHandler(InputType type, 
        Action<CallbackContext> start = null, 
        Action<CallbackContext> perform = null, 
        Action<CallbackContext> cancel = null)
    {
        InputAction input = null;
        switch (type)
        {
            case InputType.Player_Touch:
                input = _inputHandler.Player.Click;
                break;
            case InputType.Player_Point:
                input = _inputHandler.Player.Point;
                break;
            case InputType.Game_Exit:
                input = _inputHandler.Player.Exit;
                break;
            default:
                return;
        }

        if (input == null)
            return;

        if (start != null)
            input.started += start;

        if (perform != null)
            input.performed += perform;

        if (cancel != null)
            input.canceled += cancel;
    }

    public void UnsubscribeToInputHandler(InputType type, 
        Action<CallbackContext> start = null, 
        Action<CallbackContext> perform = null, 
        Action<CallbackContext> cancel = null)
    {
        InputAction input = null;
        switch (type)
        {
            case InputType.Player_Touch:
                input = _inputHandler.Player.Click;
                break;
            case InputType.Player_Point:
                input = _inputHandler.Player.Point;
                break;
            case InputType.Game_Exit:
                input = _inputHandler.Player.Exit;
                break;
            default:
                return;
        }

        if (input == null)
            return;

        if (start != null)
            input.started -= start;

        if (perform != null)
            input.performed -= perform;

        if (cancel != null)
            input.canceled -= cancel;
    }
}
