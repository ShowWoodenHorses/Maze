using System;
using System.Linq;
using Maze.Core.Definitions;
using Maze.Core.Level;
using Maze.Core.Visual;
using UnityEditor;
using UnityEditor.AddressableAssets;
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
                return "Combat visual is missing: run Maze → Dev → Create Placeholder Combat Visuals or create one " +
                       "(Create → Maze → Visual → Combat Visual). The game cannot start without it.";
            EnsurePrefabAddressable(settings, sharedGroup, combat.Bullet);
            EnsurePrefabAddressable(settings, sharedGroup, combat.Impact);
            EnsurePrefabAddressable(settings, sharedGroup, combat.MeleeSwing);

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

        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name);
            return group != null
                ? group
                : settings.CreateGroup(name, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        }
    }
}
