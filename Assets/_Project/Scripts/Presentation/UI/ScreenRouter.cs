using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Maze.Application.Flow;
using Maze.Application.Levels;

namespace Maze.Presentation.UI
{
    /// <summary>
    /// Presenter of the application screens: shows the screen matching <see cref="GameFlow.State"/>
    /// and turns screen events into GameFlow commands.
    /// </summary>
    public sealed class ScreenRouter : IDisposable
    {
        private readonly GameFlow _flow;
        private readonly ILevelCatalog _catalog;
        private readonly UIRoot _ui;
        private bool _initialized;

        public ScreenRouter(GameFlow flow, ILevelCatalog catalog, UIRoot ui)
        {
            _flow = flow;
            _catalog = catalog;
            _ui = ui;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _flow.StateChanged += Show;
            _ui.MainMenu.LevelSelected += OnLevelSelected;
            _ui.Error.BackClicked += OnMenuRequested;
            _ui.Hud.PauseClicked += _flow.PauseGameplay;
            _ui.Pause.ResumeClicked += _flow.ResumeGameplay;
            _ui.Pause.RetryClicked += OnRetryRequested;
            _ui.Pause.ExitClicked += OnMenuRequested;
            _ui.Pause.DebugCompleteClicked += _flow.CompleteLevel;
            _ui.Pause.DebugFailClicked += _flow.FailLevel;
            _ui.Result.RetryClicked += OnRetryRequested;
            _ui.Result.MenuClicked += OnMenuRequested;
            _ui.ConfirmExit.YesClicked += OnFinishLevelConfirmed;
            _ui.ConfirmExit.NoClicked += OnFinishLevelDeclined;

            Show(_flow.State);
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;

            _flow.StateChanged -= Show;
            if (_ui == null) return; // Scene already destroyed on application quit.

            _ui.MainMenu.LevelSelected -= OnLevelSelected;
            _ui.Error.BackClicked -= OnMenuRequested;
            _ui.Hud.PauseClicked -= _flow.PauseGameplay;
            _ui.Pause.ResumeClicked -= _flow.ResumeGameplay;
            _ui.Pause.RetryClicked -= OnRetryRequested;
            _ui.Pause.ExitClicked -= OnMenuRequested;
            _ui.Pause.DebugCompleteClicked -= _flow.CompleteLevel;
            _ui.Pause.DebugFailClicked -= _flow.FailLevel;
            _ui.Result.RetryClicked -= OnRetryRequested;
            _ui.Result.MenuClicked -= OnMenuRequested;
            _ui.ConfirmExit.YesClicked -= OnFinishLevelConfirmed;
            _ui.ConfirmExit.NoClicked -= OnFinishLevelDeclined;
        }

        private void Show(GameFlowState state)
        {
            var menu = state == GameFlowState.MainMenu;
            var loading = state == GameFlowState.None || state == GameFlowState.Initializing || state == GameFlowState.Loading;
            var inLevel = state == GameFlowState.Playing || state == GameFlowState.Paused ||
                          state == GameFlowState.ExitConfirmation;
            var result = state == GameFlowState.Completed || state == GameFlowState.Failed;
            var error = state == GameFlowState.Error;

            if (menu) _ui.MainMenu.SetLevels(BuildLevelList());
            if (inLevel) _ui.Hud.SetLevelName(DisplayNameOf(_flow.CurrentLevelId));
            if (result) _ui.Result.SetTitle(state == GameFlowState.Completed ? "Level complete" : "Level failed");
            if (error) _ui.Error.SetMessage(_flow.ErrorMessage);

            _ui.MainMenu.SetVisible(menu);
            _ui.Loading.SetVisible(loading);
            _ui.Hud.SetVisible(inLevel);
            _ui.Pause.SetVisible(state == GameFlowState.Paused);
            _ui.Result.SetVisible(result);
            _ui.Error.SetVisible(error);
            _ui.ConfirmExit.SetVisible(state == GameFlowState.ExitConfirmation);
        }

        private List<KeyValuePair<string, string>> BuildLevelList()
        {
            var list = new List<KeyValuePair<string, string>>(_catalog.Levels.Count);
            foreach (var entry in _catalog.Levels)
                list.Add(new KeyValuePair<string, string>(entry.LevelId,
                    string.IsNullOrEmpty(entry.DisplayName) ? entry.LevelId : entry.DisplayName));
            return list;
        }

        private string DisplayNameOf(string levelId)
        {
            var entry = levelId != null ? _catalog.Find(levelId) : null;
            return entry != null && !string.IsNullOrEmpty(entry.DisplayName) ? entry.DisplayName : levelId;
        }

        private void OnLevelSelected(string levelId) => _flow.StartLevel(levelId).Forget();

        private void OnRetryRequested() => _flow.RetryLevel().Forget();

        private void OnMenuRequested() => _flow.ExitToMenu().Forget();

        private void OnFinishLevelConfirmed() => _flow.ConfirmExit(true);

        private void OnFinishLevelDeclined() => _flow.ConfirmExit(false);
    }
}
