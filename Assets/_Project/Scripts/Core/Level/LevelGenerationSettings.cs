using System;
using UnityEngine;

namespace Maze.Core.Level
{
    public enum MazeAlgorithm
    {
        RecursiveBacktracker = 0,
    }

    /// <summary>
    /// Input for "Generate New". Used only at level creation time; runtime never reads it.
    /// MazeSeed drives the logical maze, VisualSeed drives the initial visual variant selection.
    /// </summary>
    [Serializable]
    public sealed class LevelGenerationSettings
    {
        [SerializeField] private int _width = 20;
        [SerializeField] private int _height = 20;
        [SerializeField] private int _mazeSeed;
        [SerializeField] private int _visualSeed;
        [SerializeField] private MazeAlgorithm _mazeAlgorithm = MazeAlgorithm.RecursiveBacktracker;
        [SerializeField, Range(0f, 1f)] private float _loopDensity = 0.1f;
        [SerializeField, Min(1)] private int _initialPlayerStartCount = 1;
        [SerializeField, Min(1)] private int _initialExitCount = 1;

        public int Width { get => _width; internal set => _width = value; }
        public int Height { get => _height; internal set => _height = value; }
        public int MazeSeed { get => _mazeSeed; internal set => _mazeSeed = value; }
        public int VisualSeed { get => _visualSeed; internal set => _visualSeed = value; }
        public MazeAlgorithm MazeAlgorithm { get => _mazeAlgorithm; internal set => _mazeAlgorithm = value; }
        public float LoopDensity { get => _loopDensity; internal set => _loopDensity = value; }
        public int InitialPlayerStartCount { get => _initialPlayerStartCount; internal set => _initialPlayerStartCount = value; }
        public int InitialExitCount { get => _initialExitCount; internal set => _initialExitCount = value; }

        /// <summary>Width and height must be even (ТЗ §12).</summary>
        public bool HasEvenSize => _width % 2 == 0 && _height % 2 == 0;
    }
}
