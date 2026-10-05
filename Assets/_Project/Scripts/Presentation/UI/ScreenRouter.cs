using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Maze.Application.Flow;
using Maze.Application.Levels;
using Maze.Application.Save;
using Maze.Gameplay.Level;
using UnityEngine;

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
        private readonly IProgressService _progress;
        private readonly StringBuilder _text = new StringBuilder();
        private bool _initialized;

        public ScreenRouter(GameFlow flow, ILevelCatalog catalog, UIRoot ui, IProgressService progress)
        {
            _progress = progress;
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
            _ui.MainMenu.DebugResetProgressClicked += OnResetProgressRequested;
            _ui.Error.BackClicked += OnMenuRequested;
            _ui.Hud.PauseClicked += _flow.PauseGameplay;
            _ui.Hud.MapClicked += _flow.OpenMap;
            _ui.Map.CloseClicked += _flow.CloseMap;
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
            _ui.MainMenu.DebugResetProgressClicked -= OnResetProgressRequested;
            _ui.Error.BackClicked -= OnMenuRequested;
            _ui.Hud.PauseClicked -= _flow.PauseGameplay;
            _ui.Hud.MapClicked -= _flow.OpenMap;
            _ui.Map.CloseClicked -= _flow.CloseMap;
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
            // The map is fullscreen: the HUD is hidden under it.
            var inLevel = state == GameFlowState.Playing || state == GameFlowState.Paused ||
                          state == GameFlowState.ExitConfirmation;
            var result = state == GameFlowState.Completed || state == GameFlowState.Failed;
            var error = state == GameFlowState.Error;

            if (menu) RefreshMainMenu();
            if (inLevel) _ui.Hud.SetLevelName(DisplayNameOf(_flow.CurrentLevelId));
            if (result)
            {
                _ui.Result.SetTitle(state == GameFlowState.Completed ? "Level complete" : "Level failed");
                _ui.Result.SetDetails(DescribeResult(_flow.LastResult));
            }
            if (error) _ui.Error.SetMessage(_flow.ErrorMessage);

            _ui.MainMenu.SetVisible(menu);
            _ui.Loading.SetVisible(loading);
            _ui.Hud.SetVisible(inLevel);
            _ui.Pause.SetVisible(state == GameFlowState.Paused);
            _ui.Result.SetVisible(result);
            _ui.Error.SetVisible(error);
            _ui.ConfirmExit.SetVisible(state == GameFlowState.ExitConfirmation);
            _ui.Map.SetVisible(state == GameFlowState.Map);
        }

        /// <summary>
        /// Level buttons with best stars. Locked levels are disabled; development builds (and the editor) still let
        /// you start them so any level can be tested.
        /// </summary>
        private void RefreshMainMenu()
        {
            var levels = _catalog.Levels;
            var list = new List<LevelListItem>(levels.Count);
            var totalStars = 0;
            foreach (var entry in levels)
            {
                var name = string.IsNullOrEmpty(entry.DisplayName) ? entry.LevelId : entry.DisplayName;
                var stars = _progress.GetStars(entry.LevelId);
                totalStars += stars;
                var unlocked = _progress.IsUnlocked(entry.LevelId);

                string label;
                if (!unlocked) label = Debug.isDebugBuild ? $"{name}   (locked, debug)" : $"{name}   (locked)";
                else label = $"{name}   {StarsText(stars)}";
                list.Add(new LevelListItem(entry.LevelId, label, unlocked || Debug.isDebugBuild));
            }

            _ui.MainMenu.SetLevels(list);
            _ui.MainMenu.SetSummary(levels.Count == 0
                ? string.Empty
                : $"Stars: {totalStars}/{levels.Count * LevelResult.MaxStars}    Zombies killed: {_progress.TotalKills}");
        }

        private string DescribeResult(LevelResult? lastResult)
        {
            if (!lastResult.HasValue) return string.Empty;
            var result = lastResult.Value;

            _text.Clear();
            if (result.Completed)
                _text.Append(StarsText(result.Stars)).Append('\n');
            _text.Append(Check(result.Completed)).Append(" Exit reached\n");
            _text.Append(Check(result.Completed && result.AllZombiesKilled)).Append(" Zombies killed: ")
                .Append(result.Kills).Append('/').Append(result.TotalZombies).Append('\n');
            _text.Append(Check(result.Completed && result.AllFragmentsCollected)).Append(" Map fragments: ")
                .Append(result.Fragments).Append('/').Append(result.TotalFragments);
            return _text.ToString();
        }

        /// <summary>The built-in UI font has no star glyph, so stars are written as a count.</summary>
        private static string StarsText(int stars) => $"Stars: {stars}/{LevelResult.MaxStars}";

        private static string Check(bool done) => done ? "[x]" : "[ ]";

        private string DisplayNameOf(string levelId)
        {
            var entry = levelId != null ? _catalog.Find(levelId) : null;
            return entry != null && !string.IsNullOrEmpty(entry.DisplayName) ? entry.DisplayName : levelId;
        }

        private void OnLevelSelected(string levelId) => _flow.StartLevel(levelId).Forget();

        private void OnRetryRequested() => _flow.RetryLevel().Forget();

        private void OnResetProgressRequested()
        {
            _progress.ResetProgress();
            if (_flow.State == GameFlowState.MainMenu) RefreshMainMenu();
        }

        private void OnMenuRequested() => _flow.ExitToMenu().Forget();

        private void OnFinishLevelConfirmed() => _flow.ConfirmExit(true);

        private void OnFinishLevelDeclined() => _flow.ConfirmExit(false);
    }
}
