using System;
using UnityEngine;
using UnityEngine.UI;

namespace Maze.Presentation.UI
{
    /// <summary>Fullscreen map (ТЗ §60). The level's <c>MapPresenter</c> supplies the texture; gameplay is paused while open.</summary>
    public sealed class MapScreen : UIScreen
    {
        [SerializeField] private RawImage _image;
        [SerializeField] private AspectRatioFitter _fitter;
        [SerializeField] private Text _caption;
        [SerializeField] private Button _closeButton;

        public event Action CloseClicked;

        private void Awake()
        {
            Bind(_closeButton, () => CloseClicked?.Invoke());
        }

        /// <summary>Shows <paramref name="map"/> (one pixel per cell) keeping the level's aspect ratio.</summary>
        public void SetMap(Texture map, string caption)
        {
            _image.texture = map;
            _image.enabled = map != null;
            if (map != null && _fitter != null)
                _fitter.aspectRatio = (float)map.width / map.height;
            _caption.text = caption;
        }

        public void Clear() => SetMap(null, string.Empty);
    }
}
