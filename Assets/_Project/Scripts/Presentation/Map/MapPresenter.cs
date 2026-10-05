using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Core.Grid;
using Maze.Core.Level;
using Maze.Core.Visual;
using Maze.Gameplay.Level;
using Maze.Gameplay.Map;
using Maze.Presentation.UI;
using Maze.Presentation.Visual;
using UnityEngine;

namespace Maze.Presentation.Map
{
    /// <summary>
    /// Owns the map texture of the level (one pixel per cell) and shows it on <see cref="MapScreen"/> (load stage
    /// InitializeUI). Redrawn only when a fragment is collected, never per frame. The texture is destroyed with the level.
    /// </summary>
    public sealed class MapPresenter : ILevelLoadStep, IDisposable
    {
        private readonly UIRoot _ui;
        private readonly LevelData _level;
        private readonly LevelGrid _grid;
        private readonly MapSystem _map;
        private readonly Dictionary<GridPosition, Color32> _markers = new Dictionary<GridPosition, Color32>();
        private Texture2D _texture;
        private Color32[] _pixels;
        private bool _bound;

        public MapPresenter(UIRoot ui, LevelData level, LevelGrid grid, MapSystem map)
        {
            _ui = ui;
            _level = level;
            _grid = grid;
            _map = map;
        }

        public LevelLoadStage Stage => LevelLoadStage.InitializeUI;

        /// <summary>The current map texture (for tests); null before loading and after disposal.</summary>
        public Texture2D Texture => _texture;

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
                CollectMarkers();
            }

            if (!_bound)
            {
                _map.FragmentCollected += OnFragmentCollected;
                _bound = true;
            }

            Redraw();
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            if (_bound)
            {
                _map.FragmentCollected -= OnFragmentCollected;
                _bound = false;
            }

            if (_ui != null && _ui.Map != null)
                _ui.Map.Clear();
            UnityObjects.Destroy(_texture);
            _texture = null;
        }

        private void OnFragmentCollected(MapFragmentData fragment) => Redraw();

        private void Redraw()
        {
            MapRenderer.Render(_grid, _map, _markers, _pixels);
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

        private void CollectMarkers()
        {
            _markers.Clear();
            foreach (var door in _level.Doors)
                if (door.RequiresKey &&
                    VisualColorTags.TryGetColor(VisualColorTags.TagOf(_level, VisualKind.Door, door), out var color))
                    _markers[door.Position] = color;
            foreach (var exit in _level.Exits)
                _markers[exit.Position] = MapRenderer.Exit;
            foreach (var start in _level.PlayerStarts)
                _markers[start.Position] = MapRenderer.Start;
        }
    }
}
