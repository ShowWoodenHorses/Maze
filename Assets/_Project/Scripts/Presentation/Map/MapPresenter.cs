using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Services;
using Maze.Core.Grid;
using Maze.Core.Level;
using Maze.Core.Visual;
using Maze.Gameplay.Doors;
using Maze.Gameplay.Level;
using Maze.Gameplay.Map;
using Maze.Gameplay.Player;
using Maze.Presentation.UI;
using Maze.Presentation.Visual;
using UnityEngine;

namespace Maze.Presentation.Map
{
    /// <summary>
    /// Owns the map texture of the level (one pixel per cell) and the icons over it, and shows them on
    /// <see cref="MapScreen"/> (load stage InitializeUI). The texture is redrawn only when a fragment is collected;
    /// icons are refreshed when the map opens and when a toggle changes, never per frame.
    /// Icons: the start the player began at, doors (locked: a lock in the colour of its key pair), exits — only in
    /// collected regions; map fragments not collected yet — everywhere ("Map pieces" toggle); the player — always
    /// ("Player" toggle). The toggles live in <see cref="SettingsService"/>. The texture is destroyed with the level.
    /// </summary>
    public sealed class MapPresenter : ILevelLoadStep, IDisposable
    {
        private readonly UIRoot _ui;
        private readonly LevelData _level;
        private readonly LevelGrid _grid;
        private readonly MapSystem _map;
        private readonly PlayerSystem _player;
        private readonly DoorSystem _doors;
        private readonly SettingsService _settings;
        private readonly List<MapIcon> _icons = new List<MapIcon>();
        private readonly Dictionary<string, Color> _pairColors = new Dictionary<string, Color>(StringComparer.Ordinal);
        private Texture2D _texture;
        private Color32[] _pixels;
        private bool _bound;

        public MapPresenter(UIRoot ui, LevelData level, LevelGrid grid, MapSystem map, PlayerSystem player,
            DoorSystem doors, SettingsService settings)
        {
            _ui = ui;
            _level = level;
            _grid = grid;
            _map = map;
            _player = player;
            _doors = doors;
            _settings = settings;
        }

        public LevelLoadStage Stage => LevelLoadStage.InitializeUI;

        /// <summary>The current map texture (for tests); null before loading and after disposal.</summary>
        public Texture2D Texture => _texture;

        /// <summary>
        /// Icons as last refreshed (for tests), in order: start, doors, exits, map fragments (as in the level), player.
        /// </summary>
        public IReadOnlyList<MapIcon> Icons => _icons;

        private MapIconSet Set => _ui.Map.Icons;

        public UniTask ExecuteAsync(CancellationToken cancellation)
        {
            if (_texture == null)
            {
                _texture = new Texture2D(_grid.Width, _grid.Height, TextureFormat.RGBA32, false)
                {
                    name = "Map " + _level.name,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                _pixels = new Color32[_grid.CellCount];
                CollectPairColors();
            }

            if (!_bound)
            {
                _map.FragmentCollected += OnFragmentCollected;
                _ui.Map.Opened += RefreshIcons;
                _ui.Map.ShowPlayerChanged += OnShowPlayerChanged;
                _ui.Map.ShowFragmentsChanged += OnShowFragmentsChanged;
                _bound = true;
            }

            _ui.Map.SetToggles(_settings.MapShowPlayer, _settings.MapShowFragments);
            Redraw();
            RefreshIcons(); // Creates the icon images now, while loading.
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            if (_bound)
            {
                _map.FragmentCollected -= OnFragmentCollected;
                if (_ui != null && _ui.Map != null)
                {
                    _ui.Map.Opened -= RefreshIcons;
                    _ui.Map.ShowPlayerChanged -= OnShowPlayerChanged;
                    _ui.Map.ShowFragmentsChanged -= OnShowFragmentsChanged;
                }

                _bound = false;
            }

            if (_ui != null && _ui.Map != null)
                _ui.Map.Clear();
            UnityObjects.Destroy(_texture);
            _texture = null;
        }

        private void OnFragmentCollected(MapFragmentData fragment)
        {
            Redraw();
            RefreshIcons();
        }

        private void OnShowPlayerChanged(bool show)
        {
            _settings.MapShowPlayer = show;
            RefreshIcons();
        }

        private void OnShowFragmentsChanged(bool show)
        {
            _settings.MapShowFragments = show;
            RefreshIcons();
        }

        private void Redraw()
        {
            MapRenderer.Render(_grid, _map, _pixels);
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
            _ui.Map.SetMap(_texture, Caption());
        }

        private string Caption()
        {
            if (_map.TotalCount == 0) return "This level has no map";
            if (_map.CollectedCount == 0) return $"No map fragments found yet (0/{_map.TotalCount})";
            return $"Map fragments: {_map.CollectedCount}/{_map.TotalCount}";
        }

        private void RefreshIcons()
        {
            var set = Set;
            if (set == null || _texture == null) return;
            _icons.Clear();

            // The start the player began at.
            var starts = _level.PlayerStarts;
            var startIndex = _player.StartIndex;
            if (startIndex >= 0 && startIndex < starts.Count)
                AddObjectIcon(set, MapIconKind.Start, starts[startIndex].Id, starts[startIndex].Position, 0f, set.StartColor);
            else
                _icons.Add(new MapIcon { Kind = MapIconKind.Start }); // Not spawned yet: keeps the icon count (images made at loading).

            foreach (var door in _level.Doors)
            {
                if (_doors.IsLocked(door.Id))
                {
                    var color = _pairColors.TryGetValue(door.Id, out var pair) ? pair : set.LockedDoorColor;
                    AddObjectIcon(set, MapIconKind.LockedDoor, door.Id, door.Position, 0f, color);
                }
                else
                {
                    AddObjectIcon(set, MapIconKind.Door, door.Id, door.Position,
                        MapIconLayout.DoorRotation(_grid, door.Position), set.DoorColor);
                }
            }

            foreach (var exit in _level.Exits)
                AddObjectIcon(set, MapIconKind.Exit, exit.Id, exit.Position, 0f, set.ExitColor);

            var showFragments = _settings.MapShowFragments;
            foreach (var fragment in _level.MapFragments)
            {
                var icon = ObjectIcon(set, MapIconKind.MapFragment, fragment.Id, fragment.Position, 0f, set.MapFragmentColor);
                icon.Visible = showFragments && !_map.IsCollected(fragment); // Everywhere: a hint where to go.
                _icons.Add(icon);
            }

            _icons.Add(new MapIcon
            {
                Kind = MapIconKind.Player,
                Center = MapIconLayout.CenterOf(_player.Position),
                Rotation = MapIconLayout.PlayerRotation(_player.Facing),
                Size = set.PlayerSize,
                Color = set.PlayerColor,
                Visible = _settings.MapShowPlayer && _player.IsSpawned, // Always, also outside collected regions.
            });

            _ui.Map.SetIcons(_icons);
        }

        /// <summary>An icon of a level object, shown only in collected regions.</summary>
        private void AddObjectIcon(MapIconSet set, MapIconKind kind, string id, GridPosition cell, float rotation, Color color)
        {
            var icon = ObjectIcon(set, kind, id, cell, rotation, color);
            icon.Visible = _map.IsRevealed(cell);
            _icons.Add(icon);
        }

        private static MapIcon ObjectIcon(MapIconSet set, MapIconKind kind, string id, GridPosition cell, float rotation, Color color) =>
            new MapIcon
            {
                Kind = kind,
                Center = MapIconLayout.CenterOf(cell) + MapIconLayout.Shift(id, set.MaxShift),
                Rotation = rotation + MapIconLayout.Tilt(id, set.MaxTilt),
                Size = set.Size,
                Color = color,
            };

        private void CollectPairColors()
        {
            _pairColors.Clear();
            foreach (var door in _level.Doors)
                if (door.RequiresKey &&
                    VisualColorTags.TryGetColor(VisualColorTags.TagOf(_level, VisualKind.Door, door), out var color))
                    _pairColors[door.Id] = color;
        }
    }
}
