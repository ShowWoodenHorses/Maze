using Maze.Core.Grid;

namespace Maze.Core.Visual
{
    /// <summary>
    /// Visual layers of a cell. Every cell (wall, floor, door) has a Floor layer, so the floor is visually
    /// continuous under thin walls; Wall cells additionally have a Wall layer on top.
    /// Purely visual: gameplay semantics come from <see cref="CellType"/> only.
    /// </summary>
    public enum CellLayer
    {
        Floor = 0,
        Wall = 1,
    }

    public static class CellLayers
    {
        public static readonly CellLayer[] All = { CellLayer.Floor, CellLayer.Wall };

        public static bool Exists(CellType cellType, CellLayer layer) => layer == CellLayer.Floor || cellType == CellType.Wall;

        public static VisualKind Kind(CellLayer layer) => layer == CellLayer.Floor ? VisualKind.Floor : VisualKind.Wall;
    }
}
