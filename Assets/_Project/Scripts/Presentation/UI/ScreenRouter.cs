using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Maze.Application.Flow;
using Maze.Application.Levels;
using Maze.Application.Save;
using Maze.Application.Services;
using Maze.Gameplay.Level;
using Maze.Presentation.Localization;
using UnityEngine;

namespace Maze.Presentation.UI
{
    /// <summary>
    /// Presenter of the application screens: shows the screen matching <see cref="GameFlow.State"/>
    /// and turns screen events into GameFlow commands. The level select is part of the main menu state: Play opens it
    /// over the menu, Back returns.
    /// </summary>
    public sealed class ScreenRouter : IDisposable
    {
        private readonly GameFlow _flow;
        private readonly ILevelCatalog _catalog;
        private readonly UIRoot _ui;
        private readonly IProgressService _progress;
        private readonly LocalizationService _texts;
        private readonly List<LevelTileData> _tiles = new List<LevelTileData>();
        private bool _levelSelectOpen;
        private bool _initialized;

        public ScreenRouter(GameFlow flow, ILevelCatalog catalog, UIRoot ui, IProgressService progress,
            LocalizationService texts)
        {
            _texts = texts;
            _progress = progress;
            _flow = flow;
            _catalog = catalog;
            _ui = ui;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _ui.ScaleDragThreshold();
            _flow.StateChanged += Show;
            _texts.LanguageChanged += OnLanguageChanged;
            _ui.MainMenu.PlayClicked += OnPlayClicked;
            _ui.MainMenu.DebugResetProgressClicked += OnResetProgressRequested;
            _ui.LevelSelect.LevelSelected += OnLevelSelected;
            _ui.LevelSelect.BackClicked += OnLevelSelectBack;
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
            _ui.Result.NextClicked += OnNextRequested;
            _ui.ConfirmExit.YesClicked += OnFinishLevelConfirmed;
            _ui.ConfirmExit.NoClicked += OnFinishLevelDeclined;

            Show(_flow.State);
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;

            _flow.StateChanged -= Show;
            _texts.LanguageChanged -= OnLanguageChanged;
            if (_ui == null) return; // Scene already destroyed on application quit.

            _ui.MainMenu.PlayClicked -= OnPlayClicked;
            _ui.MainMenu.DebugResetProgressClicked -= OnResetProgressRequested;
            _ui.LevelSelect.LevelSelected -= OnLevelSelected;
            _ui.LevelSelect.BackClicked -= OnLevelSelectBack;
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
            _ui.Result.NextClicked -= OnNextRequested;
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

            if (!menu) _levelSelectOpen = false;
            if (menu) RefreshMenus();
            if (inLevel) _ui.Hud.SetLevelName(LevelCaption(_flow.CurrentLevelId));
            if (result)
            {
                var completed = state == GameFlowState.Completed;
                _ui.Result.SetResult(completed, _flow.LastResult, LevelCaption(_flow.CurrentLevelId),
                    completed && NextLevelId() != null, _flow.LastPreviousBestTime);
            }
            if (error) _ui.Error.SetMessage(_flow.ErrorMessage);

            _ui.MainMenu.SetVisible(menu && !_levelSelectOpen);
            _ui.LevelSelect.SetVisible(menu && _levelSelectOpen);
            _ui.Loading.SetVisible(loading);
            _ui.Hud.SetVisible(inLevel);
            _ui.Pause.SetVisible(state == GameFlowState.Paused);
            _ui.Result.SetVisible(result);
            _ui.Error.SetVisible(error);
            _ui.ConfirmExit.SetVisible(state == GameFlowState.ExitConfirmation);
            _ui.Map.SetVisible(state == GameFlowState.Map);
        }

        /// <summary>
        /// Totals for the main menu and the level tiles with best stars. Locked levels are disabled; development builds
        /// (and the editor) still let you start them so any level can be tested.
        /// </summary>
        private void RefreshMenus()
        {
            var levels = _catalog.Levels;
            _tiles.Clear();
            var totalStars = 0;
            for (var i = 0; i < levels.Count; i++)
            {
                var entry = levels[i];
                var stars = _progress.GetStars(entry.LevelId);
                totalStars += stars;
                var unlocked = _progress.IsUnlocked(entry.LevelId);
                _tiles.Add(new LevelTileData(entry.LevelId, i + 1, stars, unlocked, unlocked || Debug.isDebugBuild,
                    _progress.GetBestTime(entry.LevelId)));
            }

            var maxStars = levels.Count * LevelResult.MaxStars;
            _ui.MainMenu.SetSummary(totalStars, maxStars, _progress.TotalKills);
            _ui.MainMenu.SetPlayable(levels.Count > 0);
            _ui.LevelSelect.SetLevels(_tiles, totalStars, maxStars);
        }

        /// <summary>The level after the current one in the catalog, if it is open (completing opens it).</summary>
        private string NextLevelId()
        {
            var levels = _catalog.Levels;
            for (var i = 0; i + 1 < levels.Count; i++)
            {
                if (levels[i].LevelId != _flow.CurrentLevelId) continue;
                var next = levels[i + 1].LevelId;
                return _progress.IsUnlocked(next) || Debug.isDebugBuild ? next : null;
            }

            return null;
        }

        /// <summary>
        /// "LEVEL 3": levels are shown by their place in the catalog (the display name of a level is for development
        /// only, decided by the user); a level outside the catalog (tests) — by its id.
        /// </summary>
        private string LevelCaption(string levelId)
        {
            var levels = _catalog.Levels;
            for (var i = 0; i < levels.Count; i++)
                if (levels[i].LevelId == levelId) return _texts.Format(TextKeys.HudLevel, i + 1);
            return levelId ?? string.Empty;
        }

        /// <summary>The language can change only over the menu or the pause screen (settings): the HUD caption.</summary>
        private void OnLanguageChanged()
        {
            var state = _flow.State;
            if (state == GameFlowState.Paused || state == GameFlowState.Playing || state == GameFlowState.ExitConfirmation)
                _ui.Hud.SetLevelName(LevelCaption(_flow.CurrentLevelId));
        }

        private void OnPlayClicked()
        {
            if (_flow.State != GameFlowState.MainMenu) return;
            _levelSelectOpen = true;
            Show(_flow.State);
        }

        private void OnLevelSelectBack()
        {
            _levelSelectOpen = false;
            Show(_flow.State);
        }

        private void OnLevelSelected(string levelId) => _flow.StartLevel(levelId).Forget();

        private void OnRetryRequested() => _flow.RetryLevel().Forget();

        private void OnNextRequested()
        {
            var next = NextLevelId();
            if (next != null) _flow.StartLevel(next).Forget();
        }

        private void OnResetProgressRequested()
        {
            _progress.ResetProgress();
            if (_flow.State == GameFlowState.MainMenu) RefreshMenus();
        }

        private void OnMenuRequested() => _flow.ExitToMenu().Forget();

        private void OnFinishLevelConfirmed() => _flow.ConfirmExit(true);

        private void OnFinishLevelDeclined() => _flow.ConfirmExit(false);
    }
}
