using System;
using Maze.Application.Services;

namespace Maze.Application.Flow
{
    /// <summary>Toggles pause from the Pause input action (Escape / Start / Android Back).</summary>
    public sealed class PauseController : IDisposable
    {
        private readonly IInputService _input;
        private readonly GameFlow _flow;
        private bool _initialized;

        public PauseController(IInputService input, GameFlow flow)
        {
            _input = input;
            _flow = flow;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _input.PauseRequested += OnPauseRequested;
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _input.PauseRequested -= OnPauseRequested;
        }

        private void OnPauseRequested()
        {
            if (_flow.State == GameFlowState.Playing) _flow.PauseGameplay();
            else if (_flow.State == GameFlowState.Paused) _flow.ResumeGameplay();
        }
    }
}
