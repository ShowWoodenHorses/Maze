using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maze.Core.Level
{
    /// <summary>One playable level: stable id, Addressables address of its <see cref="LevelData"/> and menu name.</summary>
    [Serializable]
    public sealed class LevelCatalogEntry
    {
        [SerializeField] private string _levelId;
        [SerializeField] private string _address;
        [SerializeField] private string _displayName;

        internal LevelCatalogEntry(string levelId, string address, string displayName)
        {
            _levelId = levelId;
            _address = address;
            _displayName = displayName;
        }

        public string LevelId => _levelId;
        public string Address { get => _address; internal set => _address = value; }
        public string DisplayName { get => _displayName; internal set => _displayName = value; }
    }

    /// <summary>
    /// Ordered list of levels shown in the main menu. Addressable (<see cref="Address"/>), filled by Build / Sync
    /// in the Level Designer; the order can be changed in the inspector. The menu reads names from here,
    /// so it never has to load every level.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Maze/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        public const string Address = "LevelCatalog";

        [SerializeField] private List<LevelCatalogEntry> _levels = new List<LevelCatalogEntry>();

        public IReadOnlyList<LevelCatalogEntry> Levels => _levels;

        public LevelCatalogEntry Find(string levelId)
        {
            foreach (var entry in _levels)
                if (string.Equals(entry.LevelId, levelId, StringComparison.Ordinal))
                    return entry;

            return null;
        }

        /// <summary>Adds the level to the end or updates its address and name. Returns true when something changed.</summary>
        internal bool AddOrUpdate(string levelId, string address, string displayName)
        {
            var entry = Find(levelId);
            if (entry == null)
            {
                _levels.Add(new LevelCatalogEntry(levelId, address, displayName));
                return true;
            }

            if (entry.Address == address && entry.DisplayName == displayName)
                return false;

            entry.Address = address;
            entry.DisplayName = displayName;
            return true;
        }

        internal bool Remove(string levelId)
        {
            var entry = Find(levelId);
            return entry != null && _levels.Remove(entry);
        }
    }
}
