using System;
using System.Linq;
using Maze.Core.Level;
using Maze.Core.Visual;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Maze.Editor.LevelDesigner
{
    /// <summary>
    /// Build / Sync (ТЗ §30, §84): fixes the current state of the level for runtime. Validates, makes the level
    /// and its visual prefabs Addressable, saves. Never re-randomizes anything.
    /// </summary>
    internal static class LevelSync
    {
        public const string LevelsGroup = "Maze Levels";
        public const string VisualsGroup = "Maze Visuals";
        public const string LevelAddressPrefix = "Levels/";

        public static bool Sync(LevelData level)
        {
            var report = EditorLevelValidator.Validate(level);
            if (!report.IsValid && !EditorUtility.DisplayDialog("Build / Sync",
                    $"The level has {report.ErrorCount} validation error(s) and is not ready.\n\nSync it anyway?",
                    "Sync Anyway", "Cancel"))
                return false;

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var levelsGroup = GetOrCreateGroup(settings, LevelsGroup);
            var visualsGroup = GetOrCreateGroup(settings, VisualsGroup);

            var levelGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level));
            var levelEntry = settings.CreateOrMoveEntry(levelGuid, levelsGroup);
            levelEntry.address = LevelAddressPrefix + level.name;

            var added = 0;
            var theme = level.VisualTheme;
            if (theme != null)
            {
                var prefabGuids = Enum.GetValues(typeof(VisualKind)).Cast<VisualKind>()
                    .Select(theme.GetSet)
                    .Where(set => set != null)
                    .SelectMany(set => set.Variants)
                    .Where(v => v.Prefab != null && v.Prefab.RuntimeKeyIsValid())
                    .Select(v => v.Prefab.AssetGUID)
                    .Distinct();

                // Prefabs already in some group are left where the team put them.
                foreach (var guid in prefabGuids)
                    if (settings.FindAssetEntry(guid) == null && !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                    {
                        settings.CreateOrMoveEntry(guid, visualsGroup);
                        added++;
                    }
            }

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Maze] Build/Sync '{level.name}': address '{levelEntry.address}', {added} visual prefab(s) made Addressable, " +
                      $"{report.ErrorCount} error(s), {report.WarningCount} warning(s).");
            return true;
        }

        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name);
            return group != null
                ? group
                : settings.CreateGroup(name, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        }
    }
}
