using System;
using System.Collections.Generic;

namespace Maze.Application.Save
{
    /// <summary>
    /// Everything that persists between sessions (ТЗ §85). Serialized with <c>JsonUtility</c>, hence public fields.
    /// The current unfinished run is never stored.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;

        /// <summary>Levels opened by completing the previous one. The first catalog level is always open.</summary>
        public List<string> UnlockedLevels = new List<string>();

        /// <summary>Best stars per completed level.</summary>
        public List<LevelStarsRecord> LevelStars = new List<LevelStarsRecord>();

        /// <summary>Zombies killed in completed runs.</summary>
        public int TotalKills;

        public SettingsData Settings = new SettingsData();

        /// <summary>Replaces lists and objects missing in old or hand-edited JSON.</summary>
        public void Normalize()
        {
            UnlockedLevels ??= new List<string>();
            LevelStars ??= new List<LevelStarsRecord>();
            Settings ??= new SettingsData();
            UnlockedLevels.RemoveAll(string.IsNullOrEmpty);
            LevelStars.RemoveAll(record => record == null || string.IsNullOrEmpty(record.LevelId));
            if (TotalKills < 0) TotalKills = 0;
        }
    }

    [Serializable]
    public sealed class LevelStarsRecord
    {
        public string LevelId;
        public int Stars;
    }

    [Serializable]
    public sealed class SettingsData
    {
        public float MusicVolume = 1f;
        public float SfxVolume = 1f;
    }
}
