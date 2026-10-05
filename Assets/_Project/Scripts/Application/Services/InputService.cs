using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Maze.Application.Services
{
    public interface IInputService
    {
        /// <summary>Escape / gamepad Start / Android Back.</summary>
        event Action PauseRequested;
    }

    /// <summary>
    /// Input System (new only) wrapper with the input abstraction of ТЗ §64: Move, Look, Attack, Interact,
    /// SwitchMelee, SwitchRanged, OpenMap (+ Pause). Desktop: WASD/arrows, mouse, keys. Gamepad: sticks and buttons.
    /// Android: the HUD's on-screen stick and buttons drive the same gamepad controls.
    /// </summary>
    public sealed class InputService : IInputService, IPlayerInput, IApplicationService, IDisposable
    {
        private InputActionMap _actions;
        private InputAction _move;
        private InputAction _look;
        private InputAction _attack;
        private readonly InputAction[] _buttons = new InputAction[5];

        public string Name => "Input";

        public event Action PauseRequested;

        public Vector2 Move => _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;
        public bool LookIsPointer => _look?.activeControl?.device is Pointer;
        public bool AttackHeld => _attack != null && _attack.IsPressed();

        public bool WasPressed(PlayerAction action)
        {
            var button = _buttons[(int)action];
            return button != null && button.WasPerformedThisFrame();
        }

        public UniTask InitializeAsync(CancellationToken cancellation)
        {
            _actions = new InputActionMap("Maze");

            _move = _actions.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick"); // also the on-screen stick

            _look = _actions.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            _look.AddBinding("<Pointer>/position");
            _look.AddBinding("<Gamepad>/rightStick");

            _attack = Button(PlayerAction.Attack, "<Mouse>/leftButton", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Button(PlayerAction.Interact, "<Keyboard>/e", "<Gamepad>/buttonWest");
            Button(PlayerAction.SwitchMelee, "<Keyboard>/1", "<Gamepad>/leftShoulder");
            Button(PlayerAction.SwitchRanged, "<Keyboard>/2", "<Gamepad>/rightShoulder");
            Button(PlayerAction.OpenMap, "<Keyboard>/m", "<Keyboard>/tab", "<Gamepad>/select");

            // Pause works in any state, so it is an event rather than polled by level ticks. Android Back arrives as Escape.
            var pause = _actions.AddAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");
            pause.performed += _ => PauseRequested?.Invoke();

            _actions.Enable();
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            if (_actions == null) return;
            _actions.Disable();
            _actions.Dispose();
            _actions = null;
            _move = _look = _attack = null;
            Array.Clear(_buttons, 0, _buttons.Length);
        }

        private InputAction Button(PlayerAction playerAction, params string[] bindings)
        {
            var action = _actions.AddAction(playerAction.ToString(), InputActionType.Button);
            foreach (var binding in bindings)
                action.AddBinding(binding);
            _buttons[(int)playerAction] = action;
            return action;
        }
    }
}
