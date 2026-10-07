using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Save;
using Maze.Gameplay.Combat;
using UnityEngine;

namespace Maze.Application.Services
{
    /// <summary>
    /// Player settings (ТЗ §85: part of SaveData). Every change is saved at once (a persistent event).
    /// Must be initialized after <see cref="SaveService"/>. Controls are previewed while a slider moves and saved
    /// once with <see cref="SaveControls"/> (not on every frame of a drag).
    /// </summary>
    public sealed class SettingsService : IApplicationService, IAimSettings
    {
        private readonly SaveService _save;
        private bool _controlsDirty;

        public SettingsService(SaveService save)
        {
            _save = save;
        }

        public string Name => "Settings";

        public float MusicVolume
        {
            get => _save.Data.Settings.MusicVolume;
            set => Set(ref _save.Data.Settings.MusicVolume, value);
        }

        public float SfxVolume
        {
            get => _save.Data.Settings.SfxVolume;
            set => Set(ref _save.Data.Settings.SfxVolume, value);
        }

        /// <summary>Raised when <see cref="ShowFps"/> changes (also once after loading).</summary>
        public event Action ShowFpsChanged;

        /// <summary>Frames-per-second counter on screen. Saved at once.</summary>
        public bool ShowFps
        {
            get => _save.Data.Settings.ShowFps;
            set
            {
                if (_save.Data.Settings.ShowFps == value) return;
                _save.Data.Settings.ShowFps = value;
                _save.Save();
                ShowFpsChanged?.Invoke();
            }
        }

        /// <summary>Raised when <see cref="Controls"/> change (also once after loading).</summary>
        public event Action ControlsChanged;

        public ControlsSettings Controls => _save.Data.Settings.Controls;

        AimMode IAimSettings.AimMode => Controls.AimMode;
        bool IAimSettings.AimAssist => Controls.AimAssist;

        /// <summary>Applies the controls at once; they are written by <see cref="SaveControls"/>.</summary>
        public void PreviewControls(ControlsSettings controls)
        {
            controls = controls.Clamped();
            if (controls.Equals(Controls)) return;
            _save.Data.Settings.Controls = controls;
            _controlsDirty = true;
            ControlsChanged?.Invoke();
        }

        /// <summary>Applies and saves the controls.</summary>
        public void SetControls(ControlsSettings controls)
        {
            PreviewControls(controls);
            SaveControls();
        }

        public void ResetControls() => SetControls(ControlsSettings.Default);

        /// <summary>Saves previewed controls, if any.</summary>
        public void SaveControls()
        {
            if (!_controlsDirty) return;
            _controlsDirty = false;
            _save.Save();
        }

        public UniTask InitializeAsync(CancellationToken cancellation)
        {
            var settings = _save.Data.Settings;
            settings.MusicVolume = Mathf.Clamp01(settings.MusicVolume);
            settings.SfxVolume = Mathf.Clamp01(settings.SfxVolume);
            settings.Controls = settings.Controls.Clamped();
            ControlsChanged?.Invoke();
            ShowFpsChanged?.Invoke();
            return UniTask.CompletedTask;
        }

        private void Set(ref float field, float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(field, value)) return;
            field = value;
            _save.Save();
        }
    }

    /// <summary>Music and sound effects (ТЗ §71). Stub until the sound stage.</summary>
    public sealed class AudioService : IApplicationService
    {
        public string Name => "Audio";

        public UniTask InitializeAsync(CancellationToken cancellation) => UniTask.CompletedTask;
    }
}
