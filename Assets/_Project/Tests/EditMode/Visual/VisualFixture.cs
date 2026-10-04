using System;
using System.Collections.Generic;
using Maze.Core.Definitions;
using Maze.Core.Level;
using Maze.Core.Visual;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Maze.Tests.EditMode.Visual
{
    /// <summary>Level with a fully populated test theme. Dispose destroys every created asset.</summary>
    internal sealed class VisualFixture : IDisposable
    {
        private readonly List<Object> _created = new List<Object>();

        public VisualFixture(int width = 20, int height = 20, int mazeSeed = 1, int visualSeed = 1, float loopDensity = 0.2f)
        {
            Level = Create<LevelData>();
            Level.Generation.Width = width;
            Level.Generation.Height = height;
            Level.Generation.MazeSeed = mazeSeed;
            Level.Generation.VisualSeed = visualSeed;
            Level.Generation.LoopDensity = loopDensity;

            Pistol = Create<WeaponDefinition>();
            Knife = Create<WeaponDefinition>();

            Theme = Create<VisualTheme>();
            Floor = AddSet(VisualKind.Floor, null,
                new VisualVariant("floor_01", 80),
                new VisualVariant("floor_02", 10),
                new VisualVariant("floor_03", 10),
                new VisualVariant("floor_unused", 0),
                new VisualVariant("floor_special_blood", 5, VisualCategory.Special));
            Wall = AddSet(VisualKind.Wall, "wall_default",
                new VisualVariant("wall_default", 1),
                new VisualVariant("wall_straight_a", 70, VisualCategory.Straight),
                new VisualVariant("wall_straight_b", 30, VisualCategory.Straight),
                new VisualVariant("wall_corner", 1, VisualCategory.Corner),
                new VisualVariant("wall_t", 1, VisualCategory.TJunction),
                new VisualVariant("wall_end", 1, VisualCategory.End),
                new VisualVariant("wall_cross", 1, VisualCategory.Cross));
            Door = AddSet(VisualKind.Door, null, new VisualVariant("door_01", 1), new VisualVariant("door_02", 1));
            Exit = AddSet(VisualKind.Exit, null, new VisualVariant("exit_01", 1));
            Weapon = AddSet(VisualKind.Weapon, null,
                new VisualVariant("pistol_black", 1, definition: Pistol),
                new VisualVariant("pistol_red", 1, definition: Pistol),
                new VisualVariant("knife_01", 1, definition: Knife));

            Level.VisualTheme = Theme;
        }

        public LevelData Level { get; }
        public VisualTheme Theme { get; }
        public VisualSet Floor { get; }
        public VisualSet Wall { get; }
        public VisualSet Door { get; }
        public VisualSet Exit { get; }
        public VisualSet Weapon { get; }
        public WeaponDefinition Pistol { get; }
        public WeaponDefinition Knife { get; }

        public VisualSet AddSet(VisualKind kind, string defaultVariantId, params VisualVariant[] variants)
        {
            var set = Create<VisualSet>();
            set.Kind = kind;
            set.DefaultVariantId = defaultVariantId;
            set.MutableVariants.AddRange(variants);
            Theme.SetSet(kind, set);
            return set;
        }

        public T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _created.Add(asset);
            return asset;
        }

        public void Dispose()
        {
            foreach (var asset in _created)
                Object.DestroyImmediate(asset);
        }
    }
}
