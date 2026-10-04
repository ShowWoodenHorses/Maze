using System;
using System.Linq;
using Maze.Core.Level;
using Maze.Core.Validation;
using Maze.Core.Visual;

namespace Maze.Editor.LevelDesigner
{
    /// <summary>
    /// Runtime validator plus checks that need the AssetDatabase: a prefab reference can be well-formed
    /// (valid GUID) yet point to an asset that no longer exists.
    /// </summary>
    internal static class EditorLevelValidator
    {
        public static ValidationReport Validate(LevelData level) => LevelValidator.Validate(level, CheckPrefabAssetsExist);

        private static void CheckPrefabAssetsExist(LevelData level, ValidationReport report)
        {
            var theme = level.VisualTheme;
            if (theme == null)
                return;

            var sets = Enum.GetValues(typeof(VisualKind)).Cast<VisualKind>()
                .Select(theme.GetSet)
                .Where(set => set != null)
                .Distinct();

            foreach (var set in sets)
            foreach (var variant in set.Variants)
            {
                if (variant.Prefab == null || !variant.Prefab.RuntimeKeyIsValid())
                    continue; // already reported by LevelValidator

                if (variant.Prefab.editorAsset == null)
                    report.Add(ValidationSeverity.Error, ValidationCategory.Visual, ValidationCodes.BrokenPrefabReference,
                        $"Variant '{variant.Id}' in set '{set.name}' references a prefab that does not exist in the project.");
            }
        }
    }
}
