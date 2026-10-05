using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Save;
using UnityEngine;

namespace Maze.Application.Services
{
    /// <summary>
    /// Player settings (ТЗ §85: part of SaveData). Every change is saved at once (a persistent event).
    /// Must be initialized after <see cref="SaveService"/>.
    /// </summary>
    public sealed class SettingsService : IApplicationService
    {
        private readonly SaveService _save;

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

        public UniTask InitializeAsync(CancellationToken cancellation)
        {
            var settings = _save.Data.Settings;
            settings.MusicVolume = Mathf.Clamp01(settings.MusicVolume);
            settings.SfxVolume = Mathf.Clamp01(settings.SfxVolume);
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
