using System;
using Maze.Application.Levels;
using Maze.Core.Common;
using Maze.Gameplay.Level;

namespace Maze.Application.Save
{
    /// <summary>Player progress across levels: unlocked levels, best stars, total kills (ТЗ §85, §89).</summary>
    public interface IProgressService
    {
        int TotalKills { get; }

        /// <summary>The first catalog level is always unlocked; the others after completing the previous one.</summary>
        bool IsUnlocked(string levelId);

        /// <summary>Best stars of the level, 0 when it was never completed.</summary>
        int GetStars(string levelId);

        /// <summary>Stores a completed run (persistent event): best stars, kills, unlocks the next level, saves.</summary>
        void RecordCompletion(string levelId, LevelResult result);

        /// <summary>Forgets all progress (debug).</summary>
        void ResetProgress();
    }

    public sealed class ProgressService : IProgressService
    {
        private readonly SaveService _save;
        private readonly ILevelCatalog _catalog;

        public ProgressService(SaveService save, ILevelCatalog catalog)
        {
            _save = save;
            _catalog = catalog;
        }

        private SaveData Data => _save.Data;

        public int TotalKills => Data.TotalKills;

        public bool IsUnlocked(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return false;
            var levels = _catalog.Levels;
            if (levels.Count > 0 && string.Equals(levels[0].LevelId, levelId, StringComparison.Ordinal))
                return true;

            return Data.UnlockedLevels.Contains(levelId);
        }

        public int GetStars(string levelId)
        {
            var record = FindStars(levelId);
            return record?.Stars ?? 0;
        }

        public void RecordCompletion(string levelId, LevelResult result)
        {
            if (string.IsNullOrEmpty(levelId)) throw new ArgumentException("Level id is empty.", nameof(levelId));
            if (!result.Completed) throw new ArgumentException("Only completed runs are saved.", nameof(result));

            var record = FindStars(levelId);
            if (record == null)
                Data.LevelStars.Add(record = new LevelStarsRecord { LevelId = levelId });
            record.Stars = Math.Max(record.Stars, Math.Min(result.Stars, LevelResult.MaxStars));

            Data.TotalKills += result.Kills;
            Unlock(levelId);
            var next = NextLevelId(levelId);
            if (next != null) Unlock(next);

            _save.Save();
            GameLog.Info(LogChannel.Save, $"Progress saved: '{levelId}' {result}" +
                                          (next != null ? $", unlocked '{next}'." : "."));
        }

        public void ResetProgress()
        {
            Data.UnlockedLevels.Clear();
            Data.LevelStars.Clear();
            Data.TotalKills = 0;
            _save.Save();
            GameLog.Info(LogChannel.Save, "Progress reset.");
        }

        private LevelStarsRecord FindStars(string levelId)
        {
            foreach (var record in Data.LevelStars)
                if (string.Equals(record.LevelId, levelId, StringComparison.Ordinal))
                    return record;

            return null;
        }

        private void Unlock(string levelId)
        {
            if (!Data.UnlockedLevels.Contains(levelId))
                Data.UnlockedLevels.Add(levelId);
        }

        private string NextLevelId(string levelId)
        {
            var levels = _catalog.Levels;
            for (var i = 0; i < levels.Count - 1; i++)
                if (string.Equals(levels[i].LevelId, levelId, StringComparison.Ordinal))
                    return levels[i + 1].LevelId;

            return null;
        }
    }
}
