using System;
using System.Collections.Generic;
using Maze.Core.Grid;
using Maze.Core.Level;

namespace Maze.Gameplay.Doors
{
    /// <summary>
    /// Runtime door state (ТЗ §37, §45: the prefab never decides): open/closed and locked/unlocked.
    /// A door with a key starts locked unless it is initially open. A key only unlocks its door (1 key = 1 door);
    /// the door stays closed until it is opened separately, and an unlocked door can be opened and closed any
    /// number of times. Who may open or close (reach, occupancy) is decided by the interaction, not here.
    /// </summary>
    public sealed class DoorSystem
    {
        private readonly Dictionary<GridPosition, DoorData> _byCell = new Dictionary<GridPosition, DoorData>();
        private readonly Dictionary<string, DoorData> _byId = new Dictionary<string, DoorData>();
        private readonly Dictionary<string, bool> _open = new Dictionary<string, bool>();
        private readonly HashSet<string> _locked = new HashSet<string>();

        public DoorSystem(LevelData level)
        {
            foreach (var door in level.Doors)
            {
                _byCell[door.Position] = door;
                _byId[door.Id] = door;
                _open[door.Id] = door.IsInitiallyOpen;
                if (door.RequiresKey && !door.IsInitiallyOpen)
                    _locked.Add(door.Id);
            }
        }

        /// <summary>Raised after a door opened (true) or closed (false).</summary>
        public event Action<DoorData, bool> DoorChanged;

        /// <summary>Raised after a locked door was unlocked with its key.</summary>
        public event Action<DoorData> DoorUnlocked;

        public IEnumerable<DoorData> Doors => _byId.Values;

        public bool TryGetDoor(GridPosition cell, out DoorData door) => _byCell.TryGetValue(cell, out door);

        public bool IsOpen(string doorId) => _open.TryGetValue(doorId, out var open) && open;

        /// <summary>False for cells without a door object (a door cell without DoorData counts as closed).</summary>
        public bool IsOpenAt(GridPosition cell) => _byCell.TryGetValue(cell, out var door) && IsOpen(door.Id);

        public bool IsLocked(string doorId) => _locked.Contains(doorId);

        /// <summary>Unlocks the door; the door stays closed. False when it was not locked.</summary>
        public bool Unlock(string doorId)
        {
            var door = Get(doorId);
            if (!_locked.Remove(doorId))
                return false;

            DoorUnlocked?.Invoke(door);
            return true;
        }

        public void SetOpen(string doorId, bool open)
        {
            var door = Get(doorId);
            if (_open[doorId] == open) return;
            if (open && _locked.Contains(doorId))
                throw new InvalidOperationException($"Door '{doorId}' is locked: unlock it with its key first.");

            _open[doorId] = open;
            DoorChanged?.Invoke(door, open);
        }

        private DoorData Get(string doorId) =>
            _byId.TryGetValue(doorId, out var door) ? door : throw new ArgumentException($"Unknown door '{doorId}'.", nameof(doorId));
    }
}
