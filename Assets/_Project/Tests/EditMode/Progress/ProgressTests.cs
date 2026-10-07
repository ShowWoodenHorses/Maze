using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Maze.Application.Levels;
using Maze.Application.Save;
using Maze.Application.Services;
using Maze.Core.Grid;
using Maze.Core.Level;
using Maze.Gameplay.Level;
using Maze.Gameplay.Map;
using Maze.Presentation.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Maze.Tests.EditMode.Progress
{
    /// <summary>SaveData, progress across levels, stars, settings and the map image (ТЗ §58–60, §85–87).</summary>
    public class ProgressTests
    {
        private LevelCatalog _catalogAsset;
        private MemoryStorage _storage;
        private SaveService _save;
        private ProgressService _progress;

        [SetUp]
        public void SetUp()
        {
            _catalogAsset = ScriptableObject.CreateInstance<LevelCatalog>();
            _catalogAsset.AddOrUpdate("first", "Levels/first", "First");
            _catalogAsset.AddOrUpdate("second", "Levels/second", "Second");
            _catalogAsset.AddOrUpdate("third", "Levels/third", "Third");

            _storage = new MemoryStorage();
            _save = new SaveService(_storage);
            _save.Load();
            _progress = new ProgressService(_save, new Catalog(_catalogAsset));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_catalogAsset);

        private static LevelResult Completed(int kills = 0, int zombies = 0, int fragments = 0, int totalFragments = 0) =>
            new LevelResult(true, kills, zombies, fragments, totalFragments);

        // ------------------------------------------------------------ Stars

        [Test]
        public void Stars_ExitAllZombiesAllFragments()
        {
            Assert.AreEqual(1, Completed(kills: 1, zombies: 2, fragments: 0, totalFragments: 1).Stars);
            Assert.AreEqual(2, Completed(kills: 2, zombies: 2, fragments: 0, totalFragments: 1).Stars);
            Assert.AreEqual(2, Completed(kills: 0, zombies: 2, fragments: 1, totalFragments: 1).Stars);
            Assert.AreEqual(3, Completed(kills: 2, zombies: 2, fragments: 1, totalFragments: 1).Stars);
            Assert.AreEqual(3, Completed().Stars, "Nothing to kill or collect: those stars are free.");
            Assert.AreEqual(0, new LevelResult(false, 2, 2, 1, 1).Stars, "No stars without completion.");
        }

        // ------------------------------------------------------------ Progress

        [Test]
        public void OnlyFirstLevelIsUnlocked_CompletionUnlocksTheNext()
        {
            Assert.IsTrue(_progress.IsUnlocked("first"));
            Assert.IsFalse(_progress.IsUnlocked("second"));

            _progress.RecordCompletion("first", Completed());

            Assert.IsTrue(_progress.IsUnlocked("second"));
            Assert.IsFalse(_progress.IsUnlocked("third"));
        }

        [Test]
        public void BestStarsAreKept_KillsAreSummed()
        {
            _progress.RecordCompletion("first", Completed(kills: 2, zombies: 2));
            _progress.RecordCompletion("first", Completed(kills: 1, zombies: 2));

            Assert.AreEqual(3, _progress.GetStars("first"));
            Assert.AreEqual(3, _progress.TotalKills);
            Assert.AreEqual(0, _progress.GetStars("second"));
        }

        [Test]
        public void FailedRun_IsNotRecorded()
        {
            Assert.Throws<System.ArgumentException>(() => _progress.RecordCompletion("first", new LevelResult(false, 1, 1, 0, 0)));
            Assert.AreEqual(0, _storage.Writes);
        }

        [Test]
        public void Progress_SurvivesRestart()
        {
            _progress.RecordCompletion("first", Completed(kills: 1, zombies: 3));
            Assert.AreEqual(1, _storage.Writes, "Saved right after completion.");

            var save = new SaveService(_storage);
            save.Load();
            var progress = new ProgressService(save, new Catalog(_catalogAsset));

            Assert.AreEqual(2, progress.GetStars("first"));
            Assert.AreEqual(1, progress.TotalKills);
            Assert.IsTrue(progress.IsUnlocked("second"));
        }

        [Test]
        public void ResetProgress_ForgetsEverything()
        {
            _progress.RecordCompletion("first", Completed(kills: 4, zombies: 4));
            _progress.ResetProgress();

            Assert.AreEqual(0, _progress.GetStars("first"));
            Assert.AreEqual(0, _progress.TotalKills);
            Assert.IsFalse(_progress.IsUnlocked("second"));
            Assert.IsTrue(_progress.IsUnlocked("first"));
        }

        // ------------------------------------------------------------ Save

        [Test]
        public void CorruptedSave_StartsFresh()
        {
            _storage.Json = "{ not json";
            LogAssert.Expect(LogType.Warning, new Regex("corrupted"));

            _save.Load();

            Assert.IsNotNull(_save.Data.UnlockedLevels);
            Assert.AreEqual(0, _save.Data.TotalKills);
        }

        [Test]
        public void MissingFields_AreFilledIn()
        {
            _storage.Json = "{\"TotalKills\":5}";
            _save.Load();

            Assert.AreEqual(5, _save.Data.TotalKills);
            Assert.IsNotNull(_save.Data.LevelStars);
            Assert.IsNotNull(_save.Data.Settings);
        }

        [Test]
        public void Settings_AreStoredInSave_AndSavedOnChange()
        {
            var settings = new SettingsService(_save);
            settings.InitializeAsync(CancellationToken.None);

            settings.MusicVolume = 0.25f;
            settings.MusicVolume = 0.25f;
            settings.SfxVolume = 0.5f;
            settings.SfxVolume = 7f;

            Assert.AreEqual(3, _storage.Writes, "Saved once per real change.");
            var save = new SaveService(_storage);
            save.Load();
            Assert.AreEqual(0.25f, save.Data.Settings.MusicVolume);
            Assert.AreEqual(1f, save.Data.Settings.SfxVolume, "Clamped.");
        }

        [Test]
        public void ControlsSettings_PreviewAppliesAtOnce_SaveWritesOnce_Clamped()
        {
            var settings = new SettingsService(_save);
            settings.InitializeAsync(CancellationToken.None);
            var changes = 0;
            settings.ControlsChanged += () => changes++;
            var writes = _storage.Writes;

            var controls = settings.Controls;
            controls.StickDeadZone = 0.3f;
            settings.PreviewControls(controls);
            controls.Size = 5f;
            settings.PreviewControls(controls);
            settings.PreviewControls(controls);

            Assert.AreEqual(2, changes, "Once per real change.");
            Assert.AreEqual(ControlsSettings.MaxSize, settings.Controls.Size, "Clamped.");
            Assert.AreEqual(writes, _storage.Writes, "Preview does not write.");

            settings.SaveControls();
            settings.SaveControls();
            Assert.AreEqual(writes + 1, _storage.Writes);

            var save = new SaveService(_storage);
            save.Load();
            Assert.AreEqual(0.3f, save.Data.Settings.Controls.StickDeadZone, 1e-5f);
            Assert.AreEqual(ControlsSettings.MaxSize, save.Data.Settings.Controls.Size, 1e-5f);

            settings.ResetControls();
            Assert.AreEqual(ControlsSettings.Default, settings.Controls);
            Assert.AreEqual(writes + 2, _storage.Writes);
        }

        [Test]
        public void SaveWithoutControls_GetsDefaultControls()
        {
            _storage.Json = "{\"Settings\":{\"MusicVolume\":0.5,\"SfxVolume\":1.0}}";
            _save.Load();

            Assert.AreEqual(0.5f, _save.Data.Settings.MusicVolume);
            Assert.AreEqual(ControlsSettings.Default, _save.Data.Settings.Controls);
        }

        // ------------------------------------------------------------ Map

        [Test]
        public void MapRenderer_DrawsOnlyCollectedRegions_WithMarkers()
        {
            var level = ScriptableObject.CreateInstance<LevelData>();
            try
            {
                var geometry = new LevelGeometry(4, 2);
                geometry.SetCell(new GridPosition(1, 0), CellType.Floor);
                geometry.SetCell(new GridPosition(2, 0), CellType.Door);
                geometry.SetCell(new GridPosition(3, 0), CellType.Floor);
                level.ReplaceGeometry(geometry);
                level.MutableMapFragments.Add(new MapFragmentData("fragment_1", new GridPosition(1, 0), new GridRect(0, 0, 2, 2)));
                level.MutableMapFragments.Add(new MapFragmentData("fragment_2", new GridPosition(3, 0), new GridRect(2, 0, 2, 2)));

                var grid = new LevelGrid(level.Geometry);
                var map = new MapSystem(level);
                var markers = new Dictionary<GridPosition, Color32> { [new GridPosition(3, 0)] = MapRenderer.Exit };
                var pixels = new Color32[grid.CellCount];

                MapRenderer.Render(grid, map, markers, pixels);
                foreach (var pixel in pixels)
                    Assert.AreEqual(MapRenderer.Unknown, pixel, "Nothing collected: the map is empty.");

                map.Collect(level.MapFragments[0]);
                MapRenderer.Render(grid, map, markers, pixels);
                Assert.AreEqual(MapRenderer.Wall, pixels[grid.ToIndex(new GridPosition(0, 0))]);
                Assert.AreEqual(MapRenderer.Floor, pixels[grid.ToIndex(new GridPosition(1, 0))]);
                Assert.AreEqual(MapRenderer.Wall, pixels[grid.ToIndex(new GridPosition(1, 1))]);
                Assert.AreEqual(MapRenderer.Unknown, pixels[grid.ToIndex(new GridPosition(2, 0))], "Other fragment's region.");

                map.Collect(level.MapFragments[1]);
                MapRenderer.Render(grid, map, markers, pixels);
                Assert.AreEqual(MapRenderer.Door, pixels[grid.ToIndex(new GridPosition(2, 0))]);
                Assert.AreEqual(MapRenderer.Exit, pixels[grid.ToIndex(new GridPosition(3, 0))]);
                Assert.IsTrue(map.AllCollected);
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }

        // ------------------------------------------------------------ Fakes

        private sealed class MemoryStorage : ISaveStorage
        {
            public string Json;
            public int Writes;

            public string Read() => Json;

            public void Write(string json)
            {
                Json = json;
                Writes++;
            }
        }

        private sealed class Catalog : ILevelCatalog
        {
            private readonly LevelCatalog _asset;

            public Catalog(LevelCatalog asset) => _asset = asset;

            public IReadOnlyList<LevelCatalogEntry> Levels => _asset.Levels;
            public LevelCatalogEntry Find(string levelId) => _asset.Find(levelId);
        }
    }
}
