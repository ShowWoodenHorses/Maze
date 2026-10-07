using UnityEngine;

namespace Maze.Presentation.Map
{
    /// <summary>Kinds of icons on the map (ТЗ §60).</summary>
    public enum MapIconKind
    {
        Start,
        Door,
        LockedDoor,
        Exit,
        MapFragment,
        Player,
    }

    /// <summary>
    /// Icons of the map screen: hand-drawn sprites (white shapes with a dark outline, tinted by <c>*Color</c>) and how
    /// they are placed. Made by Maze → Dev → Build Map Icons; the sprites may be replaced by any others without code.
    /// Referenced by the map screen in the Bootstrap scene.
    /// </summary>
    [CreateAssetMenu(menuName = "Maze/Map Icons", fileName = "MapIcons")]
    public sealed class MapIconSet : ScriptableObject
    {
        [Header("Sprites (pointing up / unrotated)")]
        public Sprite Start;
        [Tooltip("Door plank across a passage going north-south; turned for east-west passages.")]
        public Sprite Door;
        [Tooltip("Locked door: tinted with the colour of its key pair.")]
        public Sprite LockedDoor;
        public Sprite Exit;
        public Sprite MapFragment;
        [Tooltip("Points up (north); turned to the player's facing.")]
        public Sprite Player;

        [Header("Colours (tint)")]
        public Color StartColor = new Color(0.35f, 0.6f, 1f);
        public Color DoorColor = new Color(0.78f, 0.52f, 0.28f);
        [Tooltip("Locked door whose pair has no colour.")]
        public Color LockedDoorColor = new Color(0.85f, 0.85f, 0.85f);
        public Color ExitColor = new Color(0.35f, 0.9f, 0.45f);
        public Color MapFragmentColor = new Color(1f, 0.88f, 0.55f);
        public Color PlayerColor = new Color(1f, 0.45f, 0.2f);

        [Header("Placement")]
        [Tooltip("Icon size in cells (icons are bigger than a cell).")]
        [Min(0.5f)] public float Size = 1.8f;
        [Min(0.5f)] public float PlayerSize = 1.6f;
        [Tooltip("Smallest icon on screen, canvas units (big levels have tiny cells).")]
        [Min(0f)] public float MinScreenSize = 26f;
        [Tooltip("Largest random turn of an icon, degrees (stable per object id): a careless hand-drawn map.")]
        [Range(0f, 45f)] public float MaxTilt = 12f;
        [Tooltip("Largest random shift of an icon, cells.")]
        [Range(0f, 0.5f)] public float MaxShift = 0.15f;

        public Sprite SpriteOf(MapIconKind kind) => kind switch
        {
            MapIconKind.Start => Start,
            MapIconKind.Door => Door,
            MapIconKind.LockedDoor => LockedDoor,
            MapIconKind.Exit => Exit,
            MapIconKind.MapFragment => MapFragment,
            _ => Player,
        };

        public Color ColorOf(MapIconKind kind) => kind switch
        {
            MapIconKind.Start => StartColor,
            MapIconKind.Door => DoorColor,
            MapIconKind.LockedDoor => LockedDoorColor,
            MapIconKind.Exit => ExitColor,
            MapIconKind.MapFragment => MapFragmentColor,
            _ => PlayerColor,
        };
    }
}
