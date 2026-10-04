using Maze.Core.Common;
using Maze.Core.Grid;
using Maze.Core.Level;
using UnityEngine;

namespace Maze.Core.Visual
{
    /// <summary>
    /// Deterministic weighted selection. Used only at level creation time (Generate New / Regenerate Visuals);
    /// runtime never calls it. The choice for a cell or object depends only on VisualSeed, its key and the set,
    /// so re-assigning a single cell gives the same result as re-assigning the whole level.
    /// </summary>
    public static class VisualSelector
    {
        public static ulong CellKey(int visualSeed, VisualKind kind, int cellIndex) =>
            StableHash.Combine(StableHash.Combine((ulong)(uint)visualSeed, (ulong)kind), (ulong)(uint)cellIndex);

        public static ulong ObjectKey(int visualSeed, VisualKind kind, string entityId) =>
            StableHash.Combine(StableHash.Combine((ulong)(uint)visualSeed, (ulong)kind), StableHash.Of(entityId));

        /// <summary>Weighted pick among variants matching category/definition with weight > 0. Null if none.</summary>
        public static VisualVariant Pick(VisualSet set, VisualCategory category, ScriptableObject definition, ulong key)
        {
            if (set == null || category == VisualCategory.Special)
                return null;

            ulong total = 0;
            foreach (var variant in set.Variants)
                if (variant.Weight > 0 && variant.Matches(category, definition))
                    total += (ulong)variant.Weight;

            if (total == 0)
                return null;

            var target = ((StableHash.Mix(key) >> 32) * total) >> 32;
            foreach (var variant in set.Variants)
            {
                if (variant.Weight <= 0 || !variant.Matches(category, definition))
                    continue;

                if (target < (ulong)variant.Weight)
                    return variant;

                target -= (ulong)variant.Weight;
            }

            return null;
        }

        /// <summary>Pick, falling back to the set's explicit default variant. Never picks anything else silently.</summary>
        public static VisualVariant PickOrDefault(VisualSet set, VisualCategory category, ScriptableObject definition, ulong key)
        {
            if (set == null)
                return null;

            return Pick(set, category, definition, key) ?? set.FindVariant(set.DefaultVariantId);
        }
    }

    public static class VisualKinds
    {
        /// <summary>Wall cells use the Wall set; Floor and Door cells use the Floor set (floor under the door).</summary>
        public static VisualKind ForCell(CellType cellType) => cellType == CellType.Wall ? VisualKind.Wall : VisualKind.Floor;

        /// <summary>False for entities without a visual (player starts).</summary>
        public static bool TryGetForEntity(LevelEntityData entity, out VisualKind kind, out ScriptableObject definition)
        {
            definition = null;
            switch (entity)
            {
                case DoorData _: kind = VisualKind.Door; return true;
                case ExitData _: kind = VisualKind.Exit; return true;
                case KeyData _: kind = VisualKind.Key; return true;
                case MedkitData _: kind = VisualKind.Medkit; return true;
                case MapFragmentData _: kind = VisualKind.MapFragment; return true;
                case WeaponPickupData weapon:
                    kind = VisualKind.Weapon;
                    definition = weapon.Definition;
                    return true;
                case ZombieSpawnData zombie:
                    kind = VisualKind.Zombie;
                    definition = zombie.Definition;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }
    }
}
