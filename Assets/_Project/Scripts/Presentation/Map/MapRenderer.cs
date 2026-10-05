using System;
using System.Collections.Generic;
using Maze.Core.Grid;
using Maze.Gameplay.Map;
using UnityEngine;

namespace Maze.Presentation.Map
{
    /// <summary>
    /// Draws the map (ТЗ §60) into pixels, one pixel per cell (row-major from the south-west corner, as a texture).
    /// The source of geometry is <see cref="LevelGrid"/>; only regions of collected fragments are drawn:
    /// walls, passages, doors, plus markers (doors' colours, exits, starts). Never the player, zombies or items,
    /// and independent of current visibility.
    /// </summary>
    public static class MapRenderer
    {
        public static readonly Color32 Unknown = new Color32(24, 26, 32, 255);
        public static readonly Color32 Floor = new Color32(196, 188, 168, 255);
        public static readonly Color32 Wall = new Color32(64, 66, 74, 255);
        public static readonly Color32 Door = new Color32(150, 98, 50, 255);
        public static readonly Color32 Exit = new Color32(60, 200, 90, 255);
        public static readonly Color32 Start = new Color32(70, 130, 240, 255);

        /// <param name="markers">Colours of special cells (doors with a key colour, exits, starts); drawn over the cell type.</param>
        /// <param name="pixels">Width × Height of the grid.</param>
        public static void Render(LevelGrid grid, MapSystem map, IReadOnlyDictionary<GridPosition, Color32> markers,
            Color32[] pixels)
        {
            if (pixels == null || pixels.Length != grid.CellCount)
                throw new ArgumentException($"Expected {grid.CellCount} pixels.", nameof(pixels));

            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Unknown;

            foreach (var fragment in map.Fragments)
            {
                if (!map.IsCollected(fragment)) continue;

                var region = fragment.Region;
                var xMin = Math.Max(region.X, 0);
                var yMin = Math.Max(region.Y, 0);
                var xMax = Math.Min(region.XMax, grid.Width);
                var yMax = Math.Min(region.YMax, grid.Height);
                for (var y = yMin; y < yMax; y++)
                for (var x = xMin; x < xMax; x++)
                {
                    var cell = new GridPosition(x, y);
                    pixels[grid.ToIndex(cell)] = markers != null && markers.TryGetValue(cell, out var marker)
                        ? marker
                        : ColorOf(grid.GetCell(cell));
                }
            }
        }

        public static Color32 ColorOf(CellType type)
        {
            switch (type)
            {
                case CellType.Floor: return Floor;
                case CellType.Door: return Door;
                default: return Wall;
            }
        }
    }
}
