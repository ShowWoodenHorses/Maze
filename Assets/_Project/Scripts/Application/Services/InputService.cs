using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;

namespace Maze.Application.Services
{
    public interface IInputService
    {
        /// <summary>Escape / gamepad Start / Android Back.</summary>
        event Action PauseRequested;
    }

    /// <summary>
    /// Input System (new only) wrapper. For now only application-level actions; player movement and
    /// attacks come with the player stage (ТЗ §64–66).
    /// </summary>
    public sealed class InputService : IInputService, IApplicationService, IDisposable
    {
        private InputAction _pause;

        public string Name => "Input";

        public event Action PauseRequested;

        public UniTask InitializeAsync(CancellationToken cancellation)
        {
            _pause = new InputAction("Pause", InputActionType.Button);
            _pause.AddBinding("<Keyboard>/escape"); // Android Back arrives as Escape.
            _pause.AddBinding("<Gamepad>/start");
            _pause.performed += OnPausePerformed;
            _pause.Enable();
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            if (_pause == null) return;
            _pause.performed -= OnPausePerformed;
            _pause.Dispose();
            _pause = null;
        }

        private void OnPausePerformed(InputAction.CallbackContext context) => PauseRequested?.Invoke();
    }
}
