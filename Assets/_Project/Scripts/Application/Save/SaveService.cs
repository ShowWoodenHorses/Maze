using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Services;
using Maze.Core.Common;
using UnityEngine;

namespace Maze.Application.Save
{
    /// <summary>Where the save JSON lives. Swapped for an in-memory one in tests.</summary>
    public interface ISaveStorage
    {
        /// <summary>Null when nothing is saved.</summary>
        string Read();

        void Write(string json);
    }

    /// <summary>
    /// Stores the save in <see cref="PlayerPrefs"/>: works the same on Android and in WebGL (IndexedDB),
    /// where files in persistentDataPath need extra syncing.
    /// </summary>
    public sealed class PlayerPrefsSaveStorage : ISaveStorage
    {
        public const string Key = "Maze.SaveData";

        public string Read() => PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;

        public void Write(string json)
        {
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// SaveData persistence (ТЗ §85). Loaded once at start-up; <see cref="Save"/> is called by owners of persistent
    /// events: level completion (progress) and settings changes. A broken save never stops the game: it is
    /// replaced by a fresh one and the problem is logged.
    /// </summary>
    public sealed class SaveService : IApplicationService
    {
        private readonly ISaveStorage _storage;

        public SaveService(ISaveStorage storage)
        {
            _storage = storage;
        }

        public string Name => "Save";

        /// <summary>Current data; never null after initialization.</summary>
        public SaveData Data { get; private set; } = new SaveData();

        public UniTask InitializeAsync(CancellationToken cancellation)
        {
            Load();
            return UniTask.CompletedTask;
        }

        public void Load()
        {
            Data = Parse(ReadSafe()) ?? new SaveData();
            Data.Normalize();
        }

        /// <summary>Writes <see cref="Data"/>. False (and a logged error) when the storage failed.</summary>
        public bool Save()
        {
            try
            {
                _storage.Write(JsonUtility.ToJson(Data));
                return true;
            }
            catch (Exception e)
            {
                GameLog.Exception(LogChannel.Save, e, "Saving failed");
                return false;
            }
        }

        /// <summary>Forgets all progress and settings.</summary>
        public void ResetAll()
        {
            Data = new SaveData();
            Save();
            GameLog.Info(LogChannel.Save, "Save data reset.");
        }

        private string ReadSafe()
        {
            try
            {
                return _storage.Read();
            }
            catch (Exception e)
            {
                GameLog.Exception(LogChannel.Save, e, "Reading the save failed");
                return null;
            }
        }

        private static SaveData Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                GameLog.Info(LogChannel.Save, "No save found: starting fresh.");
                return null;
            }

            try
            {
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) throw new FormatException("Empty save.");
                GameLog.Info(LogChannel.Save, $"Save loaded: {data.UnlockedLevels?.Count ?? 0} unlocked level(s), " +
                                              $"{data.LevelStars?.Count ?? 0} with stars, {data.TotalKills} kill(s).");
                return data;
            }
            catch (Exception e)
            {
                GameLog.Warning(LogChannel.Save, $"Save is corrupted, starting fresh: {e.Message}");
                return null;
            }
        }
    }
}
