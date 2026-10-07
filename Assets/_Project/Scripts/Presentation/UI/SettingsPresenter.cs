using System;
using Maze.Application.Flow;
using Maze.Application.Save;
using Maze.Application.Services;

namespace Maze.Presentation.UI
{
    /// <summary>
    /// Applies <see cref="SettingsService.Controls"/> to the on-screen controls and <see cref="SettingsService.ShowFps"/>
    /// to the FPS counter, and runs the settings screen: it is an overlay over the main menu or the pause screen, not a
    /// GameFlow state, and closes (saving) on any state change.
    /// </summary>
    public sealed class SettingsPresenter : IDisposable
    {
        private readonly SettingsService _settings;
        private readonly GameFlow _flow;
        private readonly UIRoot _ui;
        private bool _initialized;

        public SettingsPresenter(SettingsService settings, GameFlow flow, UIRoot ui)
        {
            _settings = settings;
            _flow = flow;
            _ui = ui;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _settings.ControlsChanged += ApplyControls;
            _settings.ShowFpsChanged += ApplyShowFps;
            _ui.Settings.ShowFpsChanged += OnShowFpsEdited;
            _flow.StateChanged += OnStateChanged;
            _ui.MainMenu.SettingsClicked += Open;
            _ui.Pause.SettingsClicked += Open;
            _ui.Settings.Changed += OnEdited;
            _ui.Settings.ResetClicked += OnReset;
            _ui.Settings.BackClicked += Close;
            ApplyControls();
            ApplyShowFps();
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;

            _settings.SaveControls();
            _settings.ControlsChanged -= ApplyControls;
            _settings.ShowFpsChanged -= ApplyShowFps;
            _flow.StateChanged -= OnStateChanged;
            if (_ui == null) return; // Scene already destroyed on application quit.

            _ui.MainMenu.SettingsClicked -= Open;
            _ui.Pause.SettingsClicked -= Open;
            _ui.Settings.Changed -= OnEdited;
            _ui.Settings.ResetClicked -= OnReset;
            _ui.Settings.BackClicked -= Close;
            _ui.Settings.ShowFpsChanged -= OnShowFpsEdited;
        }

        private void ApplyControls() => _ui.TouchControls.Apply(_settings.Controls);

        private void ApplyShowFps()
        {
            if (_ui.FpsCounter != null) _ui.FpsCounter.SetVisible(_settings.ShowFps);
        }

        private void OnShowFpsEdited(bool show) => _settings.ShowFps = show;

        private void Open()
        {
            _ui.Settings.SetValues(_settings.Controls);
            _ui.Settings.SetShowFps(_settings.ShowFps);
            _ui.Settings.SetVisible(true);
        }

        private void Close()
        {
            _settings.SaveControls();
            _ui.Settings.SetVisible(false);
        }

        private void OnStateChanged(GameFlowState state)
        {
            if (_ui.Settings.IsVisible) Close();
        }

        private void OnEdited(ControlsSettings controls) => _settings.PreviewControls(controls);

        private void OnReset()
        {
            _settings.ResetControls();
            _ui.Settings.SetValues(_settings.Controls);
        }
    }
}
