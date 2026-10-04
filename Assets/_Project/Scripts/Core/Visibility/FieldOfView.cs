using System;
using System.Collections.Generic;
using Maze.Core.Grid;

namespace Maze.Core.Visibility
{
    /// <summary>
    /// Cells the player can see (ТЗ §53–55): a square window of <c>2·radius+1</c> cells around the player's cell,
    /// limited by line of sight. Reused between computations, so recalculation does not allocate.
    /// <para>
    /// Rules. The player stands somewhere in their cell, so a cell is visible when any line from a point of the
    /// origin cell to a point of the target cell is clear (centre and four inset corners on both sides).
    /// An opaque cell (wall, closed door) is visible when such a line reaches it, or when it touches (8 directions)
    /// a visible transparent cell — the walls enclosing visible floor are always shown, without holes at corners.
    /// Cells outside the grid are never visible.
    /// </para>
    /// </summary>
    public sealed class FieldOfView
    {
        /// <summary>5 cells in every direction → 11×11 (ТЗ §53).</summary>
        public const int DefaultRadius = 5;

        /// <summary>Sample points are this far from the cell centre: inside the cell, off its exact corners.</summary>
        private const float Inset = 0.45f;

        private static readonly float[] SampleX = { 0f, -Inset, Inset, -Inset, Inset };
        private static readonly float[] SampleY = { 0f, -Inset, -Inset, Inset, Inset };

        private readonly bool[] _visible;
        private readonly List<GridPosition> _cells = new List<GridPosition>();
        private readonly List<GridPosition> _opaqueCandidates = new List<GridPosition>();

        public FieldOfView(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), $"Invalid grid size {width}x{height}.");

            Width = width;
            Height = height;
            _visible = new bool[width * height];
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>False until the first <see cref="Compute{T}"/>.</summary>
        public bool HasResult { get; private set; }

        public GridPosition Origin { get; private set; }
        public int Radius { get; private set; }

        /// <summary>Visible cells of the last computation, in no particular order.</summary>
        public IReadOnlyList<GridPosition> VisibleCells => _cells;

        public bool IsVisible(GridPosition cell) =>
            cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height && _visible[cell.Y * Width + cell.X];

        /// <summary>Square window around <paramref name="origin"/>, clipped to the grid.</summary>
        public static GridRect Window(GridPosition origin, int radius, int width, int height)
        {
            var minX = Math.Max(0, origin.X - radius);
            var minY = Math.Max(0, origin.Y - radius);
            var maxX = Math.Min(width - 1, origin.X + radius);
            var maxY = Math.Min(height - 1, origin.Y + radius);
            return maxX < minX || maxY < minY ? new GridRect(0, 0, 0, 0) : new GridRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        public void Compute<T>(GridPosition origin, int radius, in T opacity) where T : IGridOpacity
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius), radius, null);

            foreach (var cell in _cells)
                _visible[cell.Y * Width + cell.X] = false;
            _cells.Clear();
            _opaqueCandidates.Clear();

            Origin = origin;
            Radius = radius;
            HasResult = true;

            var window = Window(origin, radius, Width, Height);
            for (var y = window.Y; y < window.YMax; y++)
            for (var x = window.X; x < window.XMax; x++)
            {
                var cell = new GridPosition(x, y);
                if (cell == origin || HasLine(origin, cell, opacity))
                    MarkVisible(cell);
                else if (opacity.IsOpaque(cell))
                    _opaqueCandidates.Add(cell);
            }

            // Walls touching visible floor: checked against the transparent cells found above only.
            foreach (var cell in _opaqueCandidates)
                if (TouchesVisibleTransparent(cell, opacity))
                    MarkVisible(cell);
        }

        private void MarkVisible(GridPosition cell)
        {
            _visible[cell.Y * Width + cell.X] = true;
            _cells.Add(cell);
        }

        private bool TouchesVisibleTransparent<T>(GridPosition cell, in T opacity) where T : IGridOpacity
        {
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var neighbour = new GridPosition(cell.X + dx, cell.Y + dy);
                if (IsVisible(neighbour) && !opacity.IsOpaque(neighbour))
                    return true;
            }

            return false;
        }

        private static bool HasLine<T>(GridPosition from, GridPosition to, in T opacity) where T : IGridOpacity
        {
            for (var i = 0; i < SampleX.Length; i++)
            for (var j = 0; j < SampleX.Length; j++)
                if (GridLineOfSight.IsClear(
                        from.X + SampleX[i], from.Y + SampleY[i],
                        to.X + SampleX[j], to.Y + SampleY[j], opacity))
                    return true;

            return false;
        }
    }
}
