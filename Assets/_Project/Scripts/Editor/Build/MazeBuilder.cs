using System.IO;
using System.Linq;
using Maze.Core.Level;
using Maze.Editor.Dev;
using Maze.Editor.LevelDesigner;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Maze.Editor.Build
{
    /// <summary>
    /// Player builds (ТЗ §1–2: WebGL 2 and Android): checks the catalog levels, builds Addressables content for the
    /// active platform, then the player into <c>Builds/&lt;Platform&gt;</c> (git-ignored). The platform must be active:
    /// otherwise the menu only switches to it (a long reimport) and has to be run again.
    /// Development builds show the debug buttons and let any level start; release builds do not.
    /// </summary>
    internal static class MazeBuilder
    {
        private const string Output = "Builds";

        [MenuItem("Maze/Build/WebGL (Development)")]
        public static void WebGLDevelopment() => Build(BuildTarget.WebGL, development: true);

        [MenuItem("Maze/Build/WebGL (Release)")]
        public static void WebGLRelease() => Build(BuildTarget.WebGL, development: false);

        [MenuItem("Maze/Build/Android APK (Development)")]
        public static void AndroidDevelopment() => Build(BuildTarget.Android, development: true);

        [MenuItem("Maze/Build/Android APK (Release)")]
        public static void AndroidRelease() => Build(BuildTarget.Android, development: false);

        /// <summary>Builds the player. Returns the report summary, or a problem description when nothing was built.</summary>
        public static string Build(BuildTarget target, bool development)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
                return Fail($"Switched the active platform to {target}. Run the build again once the reimport is done.");
            }

            var problem = CheckLevels();
            if (problem != null) return Fail(problem);

            ConfigurePlatform(target);

            // Addressables content must be built for the active platform before the player.
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null) return Fail("No Addressables settings: run Build / Sync in the Level Designer first.");

            // One copy of every shared dependency: a definition copied into two bundles is two different objects at
            // runtime (weapons in hands were not found on device), and duplicates only make the build bigger.
            var isolated = LevelSync.IsolateDuplicates(settings);
            if (isolated > 0)
                Debug.Log($"[Maze] {isolated} asset(s) used by several Addressables groups are kept in '{LevelSync.SharedDependenciesGroup}'.");
            var duplicates = LevelSync.FindDuplicates(settings);
            if (duplicates.Count > 0)
                return Fail("Assets are still copied into several Addressables bundles: " + string.Join(", ", duplicates.Take(10)));
            AddressableAssetSettings.CleanPlayerContent(settings.ActivePlayerDataBuilder);
            AddressableAssetSettings.BuildPlayerContent(out var content);
            if (!string.IsNullOrEmpty(content.Error)) return Fail("Addressables build failed: " + content.Error);

            var location = target == BuildTarget.Android
                ? Path.Combine(Output, "Android", development ? "Maze-dev.apk" : "Maze.apk")
                : Path.Combine(Output, development ? "WebGL-dev" : "WebGL");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { RuntimeScenesBuilder.BootstrapPath, RuntimeScenesBuilder.GamePath },
                locationPathName = location,
                target = target,
                targetGroup = group,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            var text = $"{target} {(development ? "development" : "release")} build: {summary.result}, " +
                       $"{summary.totalErrors} error(s), {summary.totalWarnings} warning(s), " +
                       $"{SizeOf(location) / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0}s → {Path.GetFullPath(location)}";
            if (summary.result == BuildResult.Succeeded) Debug.Log("[Maze] " + text);
            else Debug.LogError("[Maze] " + text);
            return text;
        }

        /// <summary>Settings the builds rely on; kept in code so a fresh checkout builds the same way.</summary>
        private static void ConfigurePlatform(BuildTarget target)
        {
            if (target == BuildTarget.WebGL)
            {
                // Without it a Gzip build only runs on a server that sends Content-Encoding (any static server works).
                PlayerSettings.WebGL.decompressionFallback = true;
            }

            // The game is landscape only (both landscape sides, rotates between them).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        /// <summary>Every catalog level must be valid: the game refuses to start an invalid one.</summary>
        private static string CheckLevels()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelSync.CatalogPath);
            if (catalog == null || catalog.Levels.Count == 0)
                return "The level catalog is empty: run Build / Sync for at least one level.";

            var problems = catalog.Levels
                .Select(entry => (entry, level: AssetDatabase.FindAssets($"t:{nameof(LevelData)} {entry.LevelId}")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<LevelData>)
                    .FirstOrDefault(l => l != null && l.name == entry.LevelId)))
                .Select(x => x.level == null
                    ? $"'{x.entry.LevelId}' not found"
                    : EditorLevelValidator.Validate(x.level) is var report && !report.IsValid
                        ? $"'{x.entry.LevelId}' has {report.ErrorCount} validation error(s)"
                        : null)
                .Where(p => p != null)
                .ToList();
            return problems.Count == 0 ? null : "Catalog levels are not ready: " + string.Join("; ", problems);
        }

        /// <summary>Size of what ships: the APK file or the WebGL folder (the report also counts IL2CPP symbol backups).</summary>
        private static long SizeOf(string location)
        {
            if (File.Exists(location)) return new FileInfo(location).Length;
            if (!Directory.Exists(location)) return 0;
            return new DirectoryInfo(location).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }

        private static string Fail(string problem)
        {
            Debug.LogWarning("[Maze] Build: " + problem);
            return problem;
        }
    }
}
