using System;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

public class InputManager : BaseManager
{
    private PlayerInput _inputHandler;

    public InputManager(ManagerInitTracker tracker) : base(tracker)
    {
        _inputHandler = new PlayerInput();
        UseInputHandler = true;
        CompleteInit(ManagerType.Input);
    }

    public bool UseInputHandler
    {
        set
        {
            if (value)
                _inputHandler.Player.Enable();
            else
                _inputHandler.Player.Disable();
        }
    }


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
