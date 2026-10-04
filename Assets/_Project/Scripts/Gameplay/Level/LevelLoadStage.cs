using System.Threading;
using Cysharp.Threading.Tasks;

namespace Maze.Gameplay.Level
{
    /// <summary>Level loading stages after the LevelScope is created, in ТЗ §9 order.</summary>
    public enum LevelLoadStage
    {
        BuildRuntime = 0,
        InitializeSystems = 1,
        BuildNavigation = 2,
        InitializeVisibility = 3,
        BuildVisuals = 4,
        InitializeUI = 5,
        SpawnPlayer = 6,
        SpawnZombies = 7,
    }

    /// <summary>
    /// A piece of level initialization registered in the LevelScope. <see cref="LevelRuntime"/> runs all steps
    /// ordered by <see cref="Stage"/>, then by registration order. Any layer may contribute a step
    /// (e.g. Presentation builds visuals), Gameplay only knows the contract.
    /// </summary>
    public interface ILevelLoadStep
    {
        LevelLoadStage Stage { get; }

        UniTask ExecuteAsync(CancellationToken cancellation);
    }

    /// <summary>Per-frame level simulation. Ticked by <see cref="LevelRuntime"/> only while gameplay runs (not paused).</summary>
    public interface ILevelTickable
    {
        void Tick(float deltaTime);
    }

    public enum LevelOutcome
    {
        Completed = 0,
        Failed = 1,
    }
}
