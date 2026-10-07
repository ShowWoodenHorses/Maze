using System;
using UnityEngine;

namespace Maze.Core.Visual
{
    /// <summary>Bundle of visual sets reused by many levels, e.g. DungeonTheme, LaboratoryTheme (ТЗ §21).</summary>
    [CreateAssetMenu(fileName = "VisualTheme", menuName = "Maze/Visual/Visual Theme")]
    public sealed class VisualTheme : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private VisualSet _floor;
        [SerializeField] private VisualSet _wall;
        [SerializeField] private VisualSet _door;
        [SerializeField] private VisualSet _exit;
        [SerializeField] private VisualSet _key;
        [SerializeField] private VisualSet _medkit;
        [SerializeField] private VisualSet _weapon;
        [SerializeField] private VisualSet _zombie;
        [SerializeField] private VisualSet _mapFragment;

        [Tooltip("Animated fog over hidden cells (shader Maze/Fog). Empty = no fog: hidden cells are simply not drawn.")]
        [SerializeField] private Material _fogMaterial;

        [SerializeField] private ThemeLighting _lighting = new ThemeLighting();

        [SerializeField] private ThemeFootprints _footprints = new ThemeFootprints();

        public string Id => _id;

        public Material FogMaterial { get => _fogMaterial; internal set => _fogMaterial = value; }

        public ThemeLighting Lighting => _lighting ??= new ThemeLighting();

        public ThemeFootprints Footprints => _footprints ??= new ThemeFootprints();

        public VisualSet GetSet(VisualKind kind)
        {
            switch (kind)
            {
                case VisualKind.Floor: return _floor;
                case VisualKind.Wall: return _wall;
                case VisualKind.Door: return _door;
                case VisualKind.Exit: return _exit;
                case VisualKind.Key: return _key;
                case VisualKind.Medkit: return _medkit;
                case VisualKind.Weapon: return _weapon;
                case VisualKind.Zombie: return _zombie;
                case VisualKind.MapFragment: return _mapFragment;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        internal void SetSet(VisualKind kind, VisualSet set)
        {
            switch (kind)
            {
                case VisualKind.Floor: _floor = set; break;
                case VisualKind.Wall: _wall = set; break;
                case VisualKind.Door: _door = set; break;
                case VisualKind.Exit: _exit = set; break;
                case VisualKind.Key: _key = set; break;
                case VisualKind.Medkit: _medkit = set; break;
                case VisualKind.Weapon: _weapon = set; break;
                case VisualKind.Zombie: _zombie = set; break;
                case VisualKind.MapFragment: _mapFragment = set; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }
}
