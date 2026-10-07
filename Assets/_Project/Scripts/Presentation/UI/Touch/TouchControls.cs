using Maze.Application.Save;
using UnityEngine;

namespace Maze.Presentation.UI.Touch
{
    /// <summary>
    /// Root of the on-screen controls (ТЗ §64): a separate overlay canvas under the application UI, so sizes are
    /// physical rather than a share of the screen. One canvas unit = 0.1 mm × <see cref="ControlsSettings.Size"/>,
    /// limited so the layout (<see cref="_designHeight"/>) always fits the safe area of small screens.
    /// Applies <see cref="ControlsSettings"/>: stick response, size, opacity, mirrored layout.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class TouchControls : MonoBehaviour
    {
        private const float UnitsPerInch = 254f;
        private const float FallbackDpi = 160f;

        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TouchStick _stick;

        [Tooltip("Stick zone and buttons; mirrored horizontally for the left-handed layout.")]
        [SerializeField] private RectTransform[] _mirrored;

        [Tooltip("Height (canvas units) the layout needs.")]
        [SerializeField] private float _designHeight = 540f;

        private Placement[] _rightHanded;
        private float _size = 1f;

        public void SetShown(bool shown)
        {
            if (gameObject.activeSelf == shown) return;
            if (shown) UpdateScale();
            gameObject.SetActive(shown);
        }

        public void Apply(ControlsSettings settings)
        {
            _size = settings.Size;
            if (_group != null) _group.alpha = settings.Opacity;
            SetLeftHanded(settings.LeftHanded);
            UpdateScale();
            if (_stick != null)
                _stick.Configure(settings.StickDeadZone, StickResponse.ExponentFor(settings.StickSensitivity),
                    settings.FloatingStick, settings.LeftHanded);
        }

        private void Update() => UpdateScale();

        /// <summary>Pixels per canvas unit; cheap, so checked every frame (resolution or DPI may change).</summary>
        private void UpdateScale()
        {
            if (_canvas == null) return;
            var dpi = Screen.dpi > 0f ? Screen.dpi : FallbackDpi;
            var scale = dpi / UnitsPerInch * _size;
            var height = Screen.safeArea.height;
            if (height > 0f) scale = Mathf.Min(scale, height / _designHeight);
            if (!Mathf.Approximately(_canvas.scaleFactor, scale))
                _canvas.scaleFactor = scale;
        }

        private void SetLeftHanded(bool leftHanded)
        {
            if (_mirrored == null) return;
            if (_rightHanded == null)
            {
                _rightHanded = new Placement[_mirrored.Length];
                for (var i = 0; i < _mirrored.Length; i++)
                    _rightHanded[i] = new Placement(_mirrored[i]);
            }

            for (var i = 0; i < _mirrored.Length; i++)
                _rightHanded[i].ApplyTo(_mirrored[i], leftHanded);
        }

        private readonly struct Placement
        {
            private readonly Vector2 _anchorMin;
            private readonly Vector2 _anchorMax;
            private readonly Vector2 _pivot;
            private readonly Vector2 _position;
            private readonly Vector2 _size;

            public Placement(RectTransform rect)
            {
                _anchorMin = rect.anchorMin;
                _anchorMax = rect.anchorMax;
                _pivot = rect.pivot;
                _position = rect.anchoredPosition;
                _size = rect.sizeDelta;
            }

            public void ApplyTo(RectTransform rect, bool mirrored)
            {
                if (mirrored)
                {
                    rect.anchorMin = new Vector2(1f - _anchorMax.x, _anchorMin.y);
                    rect.anchorMax = new Vector2(1f - _anchorMin.x, _anchorMax.y);
                    rect.pivot = new Vector2(1f - _pivot.x, _pivot.y);
                    rect.anchoredPosition = new Vector2(-_position.x, _position.y);
                }
                else
                {
                    rect.anchorMin = _anchorMin;
                    rect.anchorMax = _anchorMax;
                    rect.pivot = _pivot;
                    rect.anchoredPosition = _position;
                }

                rect.sizeDelta = _size;
            }
        }
    }
}
