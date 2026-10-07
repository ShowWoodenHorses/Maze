using System;
using System.Collections.Generic;
using System.Linq;
using Maze.Core.Definitions;
using Maze.Core.Level;
using Maze.Core.Visual;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Maze.Editor.LevelDesigner
{
    /// <summary>
    /// Build / Sync (ТЗ §30, §84): fixes the current state of the level for runtime. Validates, makes the level
    /// and its visual prefabs Addressable, adds the level to the <see cref="LevelCatalog"/> (main menu), saves.
    /// Never re-randomizes anything.
    /// </summary>
    internal static class LevelSync
    {
        public const string LevelsGroup = "Maze Levels";
        public const string VisualsGroup = "Maze Visuals";
        public const string SharedGroup = "Maze Shared";
        public const string SharedDependenciesGroup = "Maze Shared Dependencies";
        public const string LevelAddressPrefix = "Levels/";
        public const string CatalogPath = "Assets/_Project/Data/Levels/LevelCatalog.asset";

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

            var catalog = GetOrCreateCatalog(settings, levelsGroup);
            if (catalog.AddOrUpdate(level.name, levelEntry.address, level.Settings.DisplayName))
                EditorUtility.SetDirty(catalog);

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

            var sharedProblem = SyncShared(settings);
            if (sharedProblem != null)
                Debug.LogWarning("[Maze] Build/Sync: " + sharedProblem);

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Maze] Build/Sync '{level.name}': address '{levelEntry.address}', {added} visual prefab(s) made Addressable, " +
                      $"{report.ErrorCount} error(s), {report.WarningCount} warning(s).");
            return true;
        }

        /// <summary>
        /// Keeps the definitions shared by all levels Addressable at their fixed addresses: <see cref="PlayerDefinition"/>,
        /// <see cref="PlayerVisualDefinition"/> and its prefab, <see cref="CombatVisualDefinition"/> and its prefabs,
        /// <see cref="WeaponVisualCatalog"/> and the held weapon prefabs.
        /// Returns a problem description or null.
        /// </summary>
        public static string SyncShared(AddressableAssetSettings settings)
        {
            var sharedGroup = GetOrCreateGroup(settings, SharedGroup);
            var player = EnsureSingleAddressable<PlayerDefinition>(settings, sharedGroup, PlayerDefinition.Address);
            var visual = EnsureSingleAddressable<PlayerVisualDefinition>(settings, sharedGroup, PlayerVisualDefinition.Address);
            if (player == null || visual == null)
                return "Player definition or player visual is missing: run Maze → Dev → Create Placeholder Player or create them " +
                       "(Create → Maze → Definitions → Player, Create → Maze → Visual → Player Visual). The game cannot start a level without them.";

            if (visual.Prefab == null || !visual.Prefab.RuntimeKeyIsValid())
                return $"'{visual.name}' has no prefab.";

            EnsurePrefabAddressable(settings, sharedGroup, visual.Prefab);

            var combat = EnsureSingleAddressable<CombatVisualDefinition>(settings, sharedGroup, CombatVisualDefinition.Address);
            if (combat == null)
                return "Combat visual is missing: run Maze → Dev → Build Combat Effects or create one " +
                       "(Create → Maze → Visual → Combat Visual). The game cannot start without it.";
            foreach (var prefab in combat.Prefabs)
                EnsurePrefabAddressable(settings, sharedGroup, prefab);

            var weapons = EnsureSingleAddressable<WeaponVisualCatalog>(settings, sharedGroup, WeaponVisualCatalog.Address);
            if (weapons == null)
                return "Weapon visuals are missing: run Maze → Dev → Build Weapons or create them " +
                       "(Create → Maze → Visual → Weapon Visuals). The game cannot start without them.";
            foreach (var weapon in weapons.Weapons)
                if (weapon != null)
                    EnsurePrefabAddressable(settings, sharedGroup, weapon.HeldPrefab);

            EditorUtility.SetDirty(settings);
            return null;
        }

        private static void EnsurePrefabAddressable(AddressableAssetSettings settings, AddressableAssetGroup group, AssetReference prefab)
        {
            if (prefab != null && prefab.RuntimeKeyIsValid() && settings.FindAssetEntry(prefab.AssetGUID) == null)
                settings.CreateOrMoveEntry(prefab.AssetGUID, group);
        }

        private static T EnsureSingleAddressable<T>(AddressableAssetSettings settings, AddressableAssetGroup group, string address)
            where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 0)
                return null;
            if (guids.Length > 1)
                Debug.LogWarning($"[Maze] {guids.Length} {typeof(T).Name} assets found; '{AssetDatabase.GUIDToAssetPath(guids[0])}' is used.");

            var entry = settings.FindAssetEntry(guids[0]) ?? settings.CreateOrMoveEntry(guids[0], group);
            entry.address = address;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>The project's single <see cref="LevelCatalog"/>, created on first sync and kept Addressable.</summary>
        public static LevelCatalog GetOrCreateCatalog(AddressableAssetSettings settings, AddressableAssetGroup levelsGroup)
        {
            var guid = AssetDatabase.FindAssets("t:" + nameof(LevelCatalog)).FirstOrDefault();
            LevelCatalog catalog;
            if (guid != null)
            {
                catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(AssetDatabase.GUIDToAssetPath(guid));
            }
            else
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                guid = AssetDatabase.AssetPathToGUID(CatalogPath);
                Debug.Log($"[Maze] Created level catalog at {CatalogPath}.");
            }

            var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, levelsGroup);
            if (entry.address != LevelCatalog.Address)
            {
                entry.address = LevelCatalog.Address;
                EditorUtility.SetDirty(settings);
            }

            return catalog;
        }

        /// <summary>
        /// Assets that would be copied into more than one bundle (a non-Addressable dependency of several groups:
        /// weapon definitions used by levels and by the weapon catalog, models shared by the player and zombies, the
        /// Synty atlas…), from the Addressables "Check Duplicate Bundle Dependencies" analysis.
        /// </summary>
        public static List<string> FindDuplicates(AddressableAssetSettings settings)
        {
            var rule = new CheckBundleDupeDependencies();
            var paths = new HashSet<string>();
            foreach (var result in rule.RefreshAnalysis(settings))
            {
                // "Group:bundle:Assets/…" — the asset path is the last part.
                var parts = result.resultName.Split(':');
                var path = parts[parts.Length - 1];
                if (parts.Length >= 3 && path.StartsWith("Assets/", StringComparison.Ordinal) && !AssetDatabase.IsValidFolder(path))
                    paths.Add(path);
            }

            return paths.OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Makes every duplicated dependency an explicit entry of <see cref="SharedDependenciesGroup"/>, so a build has
        /// one copy of it: smaller, and the same object everywhere (a definition copied into two bundles would be two
        /// different objects at runtime). The group is rebuilt from scratch: an asset that is no longer shared (e.g. a
        /// model replaced by extracted meshes) leaves it and is not built any more. Returns the group's entry count.
        /// </summary>
        public static int IsolateDuplicates(AddressableAssetSettings settings)
        {
            var group = settings.FindGroup(SharedDependenciesGroup);
            if (group != null)
                foreach (var stale in group.entries.ToList())
                    group.RemoveAssetEntry(stale, false);

            var duplicates = FindDuplicates(settings);
            if (duplicates.Count == 0)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                return 0;
            }

            group = group != null ? group : GetOrCreateGroup(settings, SharedDependenciesGroup);
            foreach (var path in duplicates)
            {
                var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
                entry.address = path;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return duplicates.Count;
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
