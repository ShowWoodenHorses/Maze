using System;
using System.Collections.Generic;
using System.Linq;
using Maze.Core.Grid;
using Maze.Core.Level;
using Maze.Core.Visual;
using UnityEditor;
using UnityEngine;

namespace Maze.Editor.LevelDesigner
{
    /// <summary>Theme, Regenerate Visuals, overrides and distribution diagnostics (ТЗ §93–96).</summary>
    internal sealed class VisualsPanel
    {
        private bool _clearOverrides;
        private bool _showSets = true;
        private bool _showDistribution = true;
        private bool _showOverrides = true;

        public void OnGUI(LevelDesignerState state)
        {
            var level = state.Level;

            EditorGUILayout.LabelField("Visual Theme", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var theme = (VisualTheme)EditorGUILayout.ObjectField("Theme", level.VisualTheme, typeof(VisualTheme), false);
            if (EditorGUI.EndChangeCheck())
            {
                LevelEditorCommands.Modify(level, "Change Visual Theme", () => level.VisualTheme = theme);
                state.Revalidate();
            }

            if (level.VisualTheme == null)
            {
                EditorGUILayout.HelpBox("Assign a VisualTheme (Create > Maze > Visual > Visual Theme).", MessageType.Warning);
                return;
            }

            DrawSets(level.VisualTheme);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Regenerate Visuals", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var visualSeed = EditorGUILayout.IntField("Visual Seed", level.Generation.VisualSeed);
            var randomize = GUILayout.Button("Random Visual Seed");
            if (EditorGUI.EndChangeCheck() || randomize)
            {
                var seed = randomize ? LevelEditorCommands.NewRandomSeed() : visualSeed;
                LevelEditorCommands.Modify(level, "Change Visual Seed", () => level.Generation.VisualSeed = seed);
            }

            _clearOverrides = EditorGUILayout.ToggleLeft("Also clear manual overrides", _clearOverrides);
            if (GUILayout.Button("Regenerate Visuals", GUILayout.Height(26f)) &&
                LevelEditorCommands.RegenerateVisuals(level, _clearOverrides))
                state.Revalidate();

            EditorGUILayout.HelpBox("Changing the Visual Seed does not change the level until you press Regenerate Visuals.",
                MessageType.None);

            DrawOverrides(state);
            DrawDistribution(level);
        }

        private void DrawSets(VisualTheme theme)
        {
            _showSets = EditorGUILayout.Foldout(_showSets, "Visual Sets", true);
            if (!_showSets)
                return;

            using (new EditorGUI.DisabledScope(true))
            {
                foreach (VisualKind kind in Enum.GetValues(typeof(VisualKind)))
                    EditorGUILayout.ObjectField(kind.ToString(), theme.GetSet(kind), typeof(VisualSet), false);
            }

            EditorGUILayout.HelpBox("Edit sets and weights in the Inspector of the theme / set assets.", MessageType.None);
        }

        private void DrawOverrides(LevelDesignerState state)
        {
            var level = state.Level;
            var data = level.VisualData;
            var total = data.CellOverrides.Count + data.ObjectOverrides.Count;

            EditorGUILayout.Space();
            _showOverrides = EditorGUILayout.Foldout(_showOverrides, $"Manual Overrides ({total})", true);
            if (!_showOverrides || total == 0)
                return;

            CellVisualOverride cellToClear = null;
            string objectToClear = null;

            foreach (var entry in data.CellOverrides)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(entry.Position.ToString(), EditorStyles.linkLabel, GUILayout.Width(70f)))
                    state.SelectedCell = entry.Position;
                EditorGUILayout.LabelField($"{entry.Layer}: {entry.Choice}");
                if (GUILayout.Button("Clear", GUILayout.Width(50f)))
                    cellToClear = entry;
                EditorGUILayout.EndHorizontal();
            }

            foreach (var entry in data.ObjectOverrides)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(entry.EntityId, GUILayout.Width(70f));
                EditorGUILayout.LabelField(entry.Choice.ToString());
                if (GUILayout.Button("Clear", GUILayout.Width(50f)))
                    objectToClear = entry.EntityId;
                EditorGUILayout.EndHorizontal();
            }

            if (cellToClear != null)
                ClearOverride(state, () => data.ClearCellOverride(cellToClear.Position, cellToClear.Layer));
            if (objectToClear != null)
                ClearOverride(state, () => data.ClearObjectOverride(objectToClear));
        }

        private static void ClearOverride(LevelDesignerState state, Action clear)
        {
            LevelEditorCommands.Modify(state.Level, "Clear Visual Override", clear);
            state.Revalidate();
            GUIUtility.ExitGUI();
        }

        private void DrawDistribution(LevelData level)
        {
            EditorGUILayout.Space();
            _showDistribution = EditorGUILayout.Foldout(_showDistribution, "Visual Distribution", true);
            if (!_showDistribution)
                return;

            foreach (var layer in CellLayers.All)
            {
                var kind = CellLayers.Kind(layer);
                var set = level.VisualTheme.GetSet(kind);
                if (set == null)
                    continue;

                EditorGUILayout.LabelField(layer == CellLayer.Floor ? "Floor (all cells, incl. under walls)" : "Wall",
                    EditorStyles.miniBoldLabel);
                var counts = VisualResolver.CountCellVariants(level, layer);

                // Percentages are relative to the variant's category, where weights apply.
                foreach (var group in set.Variants.GroupBy(v => v.Category))
                {
                    var categoryCells = group.Sum(v => counts.TryGetValue(v.Id ?? string.Empty, out var c) ? c : 0);
                    var categoryWeight = group.Where(v => v.Weight > 0).Sum(v => v.Weight);

                    foreach (var variant in group)
                    {
                        counts.TryGetValue(variant.Id ?? string.Empty, out var count);
                        var actual = categoryCells > 0 ? 100f * count / categoryCells : 0f;
                        var expected = categoryWeight > 0 && group.Key != VisualCategory.Special ? 100f * variant.Weight / categoryWeight : 0f;
                        var label = group.Key == VisualCategory.General ? variant.Id : $"{variant.Id} [{group.Key}]";
                        EditorGUILayout.LabelField(label, $"{count,5}   {actual,5:0.0}%   (weight {expected:0.0}%)");
                    }
                }

                var unknown = counts.Where(p => set.FindVariant(p.Key) == null).Sum(p => p.Value);
                if (unknown > 0)
                    EditorGUILayout.HelpBox($"{unknown} {kind} cell(s) have no or unknown visual.", MessageType.Warning);
            }
        }
    }
}
