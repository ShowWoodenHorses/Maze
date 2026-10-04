using System.Collections.Generic;
using Maze.Core.Grid;
using Maze.Core.Level;

namespace Maze.Core.Visual
{
    /// <summary>
    /// Creates saved visual assignments from the level's VisualTheme and VisualSeed (ТЗ §18–29, §95–96).
    /// Editor/level-creation only. Overrides are never touched unless explicitly cleared.
    /// </summary>
    internal static class VisualAssigner
    {
        /// <summary>"Regenerate Visuals": geometry and objects stay, assignments are recreated.</summary>
        public static void AssignAll(LevelData level, bool clearOverrides)
        {
            if (clearOverrides)
                level.VisualData.ClearOverrides();

            AssignAllCells(level);
            AssignAllObjects(level);
        }

        public static void AssignAllCells(LevelData level)
        {
            var geometry = level.Geometry;
            level.VisualData.ResetCellAssignments(geometry.CellCount);
            for (var i = 0; i < geometry.CellCount; i++)
                level.VisualData.SetCellAssignment(i, ChooseCell(level, geometry.ToPosition(i)));
        }

        public static void AssignAllObjects(LevelData level)
        {
            var ids = new HashSet<string>();
            foreach (var entity in level.AllEntities())
            {
                ids.Add(entity.Id);
                AssignObject(level, entity);
            }

            level.VisualData.RetainObjects(ids);
        }

        /// <summary>
        /// Re-assigns a cell whose geometry changed, plus everything whose shape depends on it:
        /// the 8 surrounding cells and objects standing on them (door/exit orientation).
        /// </summary>
        public static void ReassignAround(LevelData level, GridPosition center)
        {
            var geometry = level.Geometry;
            if (level.VisualData.CellAssignmentCount != geometry.CellCount)
            {
                AssignAll(level, clearOverrides: false);
                return;
            }

            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var p = new GridPosition(center.X + dx, center.Y + dy);
                if (!geometry.IsInside(p))
                    continue;

                level.VisualData.SetCellAssignment(geometry.ToIndex(p), ChooseCell(level, p));
                foreach (var entity in level.AllEntities())
                    if (entity.Position == p)
                        AssignObject(level, entity);
            }
        }

        public static void AssignObject(LevelData level, LevelEntityData entity)
        {
            if (VisualKinds.TryGetForEntity(entity, out _, out _))
                level.VisualData.SetObjectAssignment(entity.Id, ChooseObject(level, entity));
        }

        public static VisualChoice ChooseCell(LevelData level, GridPosition position)
        {
            var theme = level.VisualTheme;
            if (theme == null)
                return VisualChoice.None;

            var geometry = level.Geometry;
            var context = new CellVisualContext(geometry, position);
            var kind = VisualKinds.ForCell(context.CellType);
            var category = VisualCategory.General;
            var rotation = 0;

            if (kind == VisualKind.Wall)
                (category, rotation) = WallShapes.Classify(context.WallConnections);

            var key = VisualSelector.CellKey(level.Generation.VisualSeed, kind, geometry.ToIndex(position));
            var variant = VisualSelector.PickOrDefault(theme.GetSet(kind), category, null, key);
            return variant == null ? VisualChoice.None : new VisualChoice(variant.Id, rotation);
        }

        public static VisualChoice ChooseObject(LevelData level, LevelEntityData entity)
        {
            var theme = level.VisualTheme;
            if (theme == null || !VisualKinds.TryGetForEntity(entity, out var kind, out var definition))
                return VisualChoice.None;

            var key = VisualSelector.ObjectKey(level.Generation.VisualSeed, kind, entity.Id);
            var variant = VisualSelector.PickOrDefault(theme.GetSet(kind), VisualCategory.General, definition, key);
            return variant == null
                ? VisualChoice.None
                : new VisualChoice(variant.Id, ObjectOrientation.For(kind, level.Geometry, entity.Position));
        }
    }

    /// <summary>Orientation derived from geometry (not random). Prefabs are authored facing North.</summary>
    public static class ObjectOrientation
    {
        public static int For(VisualKind kind, LevelGeometry geometry, GridPosition position)
        {
            if (!geometry.IsInside(position))
                return 0;

            var context = new CellVisualContext(geometry, position);
            switch (kind)
            {
                // Rotation 0: door blocks a North-South passage (walls on East and West).
                case VisualKind.Door:
                    return context.East != CellType.Floor && context.West != CellType.Floor ? 0 : 1;

                // Exit faces the first open side, so in a dead end it faces the corridor.
                case VisualKind.Exit:
                    foreach (var direction in DirectionExtensions.All)
                        if (context.Get(direction) != CellType.Wall)
                            return (int)direction;
                    return 0;

                default:
                    return 0;
            }
        }
    }
}
