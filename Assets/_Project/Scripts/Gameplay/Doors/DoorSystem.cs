using System;
using System.Collections.Generic;
using Maze.Core.Grid;
using Maze.Core.Level;

namespace Maze.Gameplay.Doors
{
    /// <summary>
    /// Runtime door state (ТЗ §37, §45: the prefab never decides). For now only open/closed, starting from
    /// <see cref="DoorData.IsInitiallyOpen"/>; keys, locking and player interaction come with the doors stage.
    /// </summary>
    public sealed class DoorSystem
    {
        private readonly Dictionary<GridPosition, DoorData> _byCell = new Dictionary<GridPosition, DoorData>();
        private readonly Dictionary<string, bool> _open = new Dictionary<string, bool>();

        public DoorSystem(LevelData level)
        {
            foreach (var door in level.Doors)
            {
                _byCell[door.Position] = door;
                _open[door.Id] = door.IsInitiallyOpen;
            }
        }

        /// <summary>Raised after a door opened or closed.</summary>
        public event Action<DoorData, bool> DoorChanged;

        public bool TryGetDoor(GridPosition cell, out DoorData door) => _byCell.TryGetValue(cell, out door);

        public bool IsOpen(string doorId) => _open.TryGetValue(doorId, out var open) && open;

        /// <summary>False for cells without a door object (a door cell without DoorData counts as closed).</summary>
        public bool IsOpenAt(GridPosition cell) => _byCell.TryGetValue(cell, out var door) && IsOpen(door.Id);

        public void SetOpen(string doorId, bool open)
        {
            if (!_open.TryGetValue(doorId, out var current))
                throw new ArgumentException($"Unknown door '{doorId}'.", nameof(doorId));
            if (current == open) return;

            _open[doorId] = open;
            foreach (var door in _byCell.Values)
                if (door.Id == doorId)
                {
                    DoorChanged?.Invoke(door, open);
                    break;
                }
        }
    }
}
