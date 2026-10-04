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

        public string Name => "Input";

        public event Action PauseRequested;
        public event Action AttackPressed;
        public event Action InteractPressed;
        public event Action SwitchMeleePressed;
        public event Action SwitchRangedPressed;
        public event Action OpenMapPressed;

        public Vector2 Move => _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;
        public bool LookIsPointer => _look?.activeControl?.device is Pointer;
        public bool AttackHeld => _attack != null && _attack.IsPressed();

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

            _attack = Button("Attack", () => AttackPressed?.Invoke(), "<Mouse>/leftButton", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Button("Interact", () => InteractPressed?.Invoke(), "<Keyboard>/e", "<Gamepad>/buttonWest");
            Button("SwitchMelee", () => SwitchMeleePressed?.Invoke(), "<Keyboard>/1", "<Gamepad>/leftShoulder");
            Button("SwitchRanged", () => SwitchRangedPressed?.Invoke(), "<Keyboard>/2", "<Gamepad>/rightShoulder");
            Button("OpenMap", () => OpenMapPressed?.Invoke(), "<Keyboard>/m", "<Keyboard>/tab", "<Gamepad>/select");
            // Android Back arrives as Escape.
            Button("Pause", () => PauseRequested?.Invoke(), "<Keyboard>/escape", "<Gamepad>/start");

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
        }

        private InputAction Button(string name, Action onPerformed, params string[] bindings)
        {
            var action = _actions.AddAction(name, InputActionType.Button);
            foreach (var binding in bindings)
                action.AddBinding(binding);
            action.performed += _ => onPerformed();
            return action;
        }
    }
}
