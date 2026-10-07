using System;
using Maze.Application.Flow;
using Maze.Application.Save;
using Maze.Application.Services;
using Maze.Presentation.UI.Touch;

namespace Maze.Presentation.UI
{
    /// <summary>
    /// Applies <see cref="SettingsService.Controls"/> to the on-screen controls and <see cref="SettingsService.ShowFps"/>
    /// to the FPS counter, and runs the settings screen: it is an overlay over the main menu or the pause screen, not a
    /// GameFlow state, and closes (saving) on any state change. "Edit button layout" hides the screen and runs
    /// <see cref="TouchLayoutEditor"/>; its changes are previewed like the other controls and saved when it is done.
    /// </summary>
    public sealed class SettingsPresenter : IDisposable
    {
        private readonly SettingsService _settings;
        private readonly GameFlow _flow;
        private readonly UIRoot _ui;
        private bool _initialized;

        private TouchLayoutEditor LayoutEditor => _ui.TouchControls != null ? _ui.TouchControls.LayoutEditor : null;

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
            _ui.Settings.EditLayoutClicked += BeginLayoutEdit;
            if (LayoutEditor != null)
            {
                LayoutEditor.Changed += OnLayoutEdited;
                LayoutEditor.Done += EndLayoutEdit;
            }

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
            _ui.Settings.EditLayoutClicked -= BeginLayoutEdit;
            if (LayoutEditor != null)
            {
                LayoutEditor.Changed -= OnLayoutEdited;
                LayoutEditor.Done -= EndLayoutEdit;
            }
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
            if (LayoutEditor != null && LayoutEditor.IsEditing)
            {
                LayoutEditor.End();
                _settings.SaveControls();
            }

            if (_ui.Settings.IsVisible) Close();
        }

        private void BeginLayoutEdit()
        {
            if (LayoutEditor == null) return;
            _ui.Settings.SetVisible(false);
            LayoutEditor.Begin(_settings.Controls.Layout);
        }

        private void OnLayoutEdited(TouchLayout layout)
        {
            var controls = _settings.Controls;
            controls.Layout = layout;
            _settings.PreviewControls(controls);
        }

        private void EndLayoutEdit()
        {
            LayoutEditor.End();
            _settings.SaveControls();
            Open();
        }

        private void OnEdited(ControlsSettings controls) => _settings.PreviewControls(controls);

        private void OnReset(SettingsTab tab)
        {
            switch (tab)
            {
                case SettingsTab.Controls: _settings.ResetTouchControls(); break;
                case SettingsTab.Shooting: _settings.ResetAim(); break;
                default: _settings.ShowFps = false; break;
            }

            _ui.Settings.SetValues(_settings.Controls);
            _ui.Settings.SetShowFps(_settings.ShowFps);
        }
    }
}
