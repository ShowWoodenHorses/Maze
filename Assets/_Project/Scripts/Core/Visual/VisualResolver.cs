using System.Collections.Generic;
using Maze.Core.Grid;
using Maze.Core.Level;

namespace Maze.Core.Visual
{
    public enum VisualSource
    {
        None = 0,
        Override = 1,
        Assignment = 2,
        Default = 3,
    }

    /// <summary>
    /// Final visual for a cell or object, by priority: designer override > saved assignment > set default (ТЗ §28).
    /// Pure lookup, no randomness: this is what runtime uses.
    /// </summary>
    public static class VisualResolver
    {
        public static VisualChoice ResolveCell(LevelData level, GridPosition position) => ResolveCell(level, position, out _);

        public static VisualChoice ResolveCell(LevelData level, GridPosition position, out VisualSource source)
        {
            var data = level.VisualData;
            if (data.TryGetCellOverride(position, out var choice) && !choice.IsEmpty)
            {
                source = VisualSource.Override;
                return choice;
            }

            var geometry = level.Geometry;
            choice = data.GetCellAssignment(geometry.ToIndex(position));
            if (!choice.IsEmpty)
            {
                source = VisualSource.Assignment;
                return choice;
            }

            var kind = VisualKinds.ForCell(geometry.GetCell(position));
            var defaultId = DefaultVariantId(level, kind);
            if (!string.IsNullOrEmpty(defaultId))
            {
                source = VisualSource.Default;
                var rotation = kind == VisualKind.Wall
                    ? WallShapes.Classify(new CellVisualContext(geometry, position).WallConnections).Rotation
                    : 0;
                return new VisualChoice(defaultId, rotation);
            }

            source = VisualSource.None;
            return VisualChoice.None;
        }

        public static VisualChoice ResolveObject(LevelData level, LevelEntityData entity) => ResolveObject(level, entity, out _);

        public static VisualChoice ResolveObject(LevelData level, LevelEntityData entity, out VisualSource source)
        {
            var data = level.VisualData;
            if (data.TryGetObjectOverride(entity.Id, out var choice) && !choice.IsEmpty)
            {
                source = VisualSource.Override;
                return choice;
            }

            choice = data.GetObjectAssignment(entity.Id);
            if (!choice.IsEmpty)
            {
                source = VisualSource.Assignment;
                return choice;
            }

            if (VisualKinds.TryGetForEntity(entity, out var kind, out _))
            {
                var defaultId = DefaultVariantId(level, kind);
                if (!string.IsNullOrEmpty(defaultId))
                {
                    source = VisualSource.Default;
                    return new VisualChoice(defaultId, ObjectOrientation.For(kind, level.Geometry, entity.Position));
                }
            }

            source = VisualSource.None;
            return VisualChoice.None;
        }

        // Explicit Unity null checks: '?.' would bypass UnityEngine.Object's missing-reference check.
        private static string DefaultVariantId(LevelData level, VisualKind kind)
        {
            var theme = level.VisualTheme;
            if (theme == null)
                return null;

            var set = theme.GetSet(kind);
            return set == null ? null : set.DefaultVariantId;
        }

        /// <summary>Visual distribution diagnostics (ТЗ §93): resolved variant id -> number of cells of that kind.</summary>
        public static Dictionary<string, int> CountCellVariants(LevelData level, VisualKind kind)
        {
            var counts = new Dictionary<string, int>();
            var geometry = level.Geometry;
            for (var i = 0; i < geometry.CellCount; i++)
            {
                var position = geometry.ToPosition(i);
                if (VisualKinds.ForCell(geometry.GetCell(position)) != kind)
                    continue;

                var id = ResolveCell(level, position).VariantId ?? string.Empty;
                counts.TryGetValue(id, out var count);
                counts[id] = count + 1;
            }

            return counts;
        }
    }
}
