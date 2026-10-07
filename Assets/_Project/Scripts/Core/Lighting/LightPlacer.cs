using System.Collections.Generic;
using Maze.Core.Common;
using Maze.Core.Grid;
using Maze.Core.Level;
using UnityEngine;

namespace Maze.Core.Lighting
{
    /// <summary>
    /// Auto placement of light sources (editor, level creation): like torches on walls — a floor cell next to a wall,
    /// the light shifted towards it. Deterministic by VisualSeed: candidates (cell, wall side) are visited in hash
    /// order and taken while no other light is closer than the spacing given by
    /// <see cref="LevelGenerationSettings.LightDensity"/>; the kind of light is a weighted pick from the theme's
    /// <see cref="Visual.ThemeLighting.LightPresets"/>. Hand-placed lights stay (and keep the others away from them).
    /// </summary>
    internal static class LightPlacer
    {
        /// <summary>Shift from the cell centre towards the wall.</summary>
        public const float WallOffset = 0.42f;

        /// <summary>Distance between auto placed lights at density → 0 and at density 1, cells.</summary>
        public const float SparseSpacing = 9f;
        public const float DenseSpacing = 3f;

        private static readonly ulong Salt = StableHash.Of("light");

        public static void PlaceAll(LevelData level, bool keepManual)
        {
            var lights = level.MutableLights;
            lights.RemoveAll(light => light.IsGenerated || !keepManual);

            var presets = level.VisualTheme != null ? level.VisualTheme.Lighting.LightPresets : null;
            var density = level.Generation.LightDensity;
            if (presets == null || TotalWeight(presets) <= 0f || density <= 0f)
                return;

            var spacing = Mathf.Lerp(SparseSpacing, DenseSpacing, Mathf.Clamp01(density));
            var seed = StableHash.Combine(Salt, unchecked((ulong)level.Generation.VisualSeed));
            var candidates = Candidates(level.Geometry, seed);
            candidates.Sort((a, b) => a.Key.CompareTo(b.Key));

            var taken = new List<Vector2>();
            var usedCells = new HashSet<GridPosition>();
            foreach (var light in lights)
            {
                taken.Add(light.Point);
                usedCells.Add(light.Cell);
            }

            foreach (var candidate in candidates)
            {
                if (usedCells.Contains(candidate.Cell))
                    continue;

                var side = candidate.Side.ToOffset();
                var offset = new Vector2(side.X, side.Y) * WallOffset;
                var point = new Vector2(candidate.Cell.X, candidate.Cell.Y) + offset;
                if (IsNearAny(point, taken, spacing))
                    continue;

                var preset = Pick(presets, StableHash.Combine(candidate.Key, 1UL));
                lights.Add(new LightSourceData(level.CreateUniqueId(LightSourceData.IdPrefix), candidate.Cell, offset,
                    preset.Color, preset.Radius, preset.Intensity, preset.Flicker, isGenerated: true));
                taken.Add(point);
                usedCells.Add(candidate.Cell);
            }
        }

        private static List<Candidate> Candidates(LevelGeometry geometry, ulong seed)
        {
            var result = new List<Candidate>();
            for (var i = 0; i < geometry.CellCount; i++)
            {
                var cell = geometry.ToPosition(i);
                if (geometry.GetCell(cell) != CellType.Floor)
                    continue;

                foreach (var side in DirectionExtensions.All)
                {
                    var neighbour = cell + side.ToOffset();
                    // Outside the grid counts as wall.
                    if (geometry.IsInside(neighbour) && geometry.GetCell(neighbour) != CellType.Wall)
                        continue;
                    result.Add(new Candidate(StableHash.Combine(seed, (ulong)(i * 4 + (int)side)), cell, side));
                }
            }

            return result;
        }

        private static bool IsNearAny(Vector2 point, List<Vector2> taken, float spacing)
        {
            var limit = spacing * spacing;
            foreach (var other in taken)
                if ((other - point).sqrMagnitude < limit)
                    return true;
            return false;
        }

        private static float TotalWeight(List<LightPreset> presets)
        {
            var total = 0f;
            foreach (var preset in presets)
                if (preset != null && preset.Weight > 0f)
                    total += preset.Weight;
            return total;
        }

        private static LightPreset Pick(List<LightPreset> presets, ulong key)
        {
            var target = (float)((key >> 11) * (1.0 / (1UL << 53))) * TotalWeight(presets);
            LightPreset last = null;
            foreach (var preset in presets)
            {
                if (preset == null || preset.Weight <= 0f) continue;
                last = preset;
                target -= preset.Weight;
                if (target < 0f) return preset;
            }

            return last;
        }

        private readonly struct Candidate
        {
            public Candidate(ulong key, GridPosition cell, Direction side)
            {
                Key = key;
                Cell = cell;
                Side = side;
            }

            public ulong Key { get; }
            public GridPosition Cell { get; }
            public Direction Side { get; }
        }
    }
}
