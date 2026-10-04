using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Core.Common;

namespace Maze.Application.Services
{
    /// <summary>
    /// Player settings (ТЗ §85: part of SaveData). Stub: values live in memory only;
    /// persistence comes with <see cref="SaveService"/>.
    /// </summary>
    public sealed class SettingsService : IApplicationService
    {
        public string Name => "Settings";

        public float MusicVolume { get; set; } = 1f;
        public float SfxVolume { get; set; } = 1f;

        public UniTask InitializeAsync(CancellationToken cancellation) => UniTask.CompletedTask;
    }

    /// <summary>SaveData persistence (ТЗ §85). Stub until progress, stars and death/completion exist.</summary>
    public sealed class SaveService : IApplicationService
    {
        public string Name => "Save";

        public UniTask InitializeAsync(CancellationToken cancellation)
        {
            GameLog.Info(LogChannel.Save, "Save service is a stub: nothing is loaded or stored yet.");
            return UniTask.CompletedTask;
        }
    }

    /// <summary>Music and sound effects (ТЗ §71). Stub until the sound stage.</summary>
    public sealed class AudioService : IApplicationService
    {
        public string Name => "Audio";

        public UniTask InitializeAsync(CancellationToken cancellation) => UniTask.CompletedTask;
    }
}
