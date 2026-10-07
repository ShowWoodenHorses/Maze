using System;
using Maze.Presentation.UI.Touch;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Maze.Presentation.UI
{
    public sealed class HudScreen : UIScreen
    {
        private const float MessageSeconds = 2f;

        [SerializeField] private Text _levelName;
        [SerializeField] private Text _status;
        [SerializeField] private Text _message;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _mapButton;

        [Tooltip("On-screen stick and buttons (ТЗ §64, Android): a separate canvas, shown while the HUD is. " +
                 "They drive gamepad controls through the Input System.")]
        [SerializeField] private TouchControls _touchControls;

        private float _messageHideTime;

        /// <summary>A real touch was seen on a platform that is not mobile (e.g. a desktop browser on a touchscreen).</summary>
        private static bool _touchSeen;

        public event Action PauseClicked;
        public event Action MapClicked;

        /// <summary>
        /// Touch controls are shown at once on mobile (including mobile browsers). Elsewhere a touchscreen device may be
        /// reported without one (desktop browsers expose touch support), so they appear only after a real touch.
        /// </summary>
        public static bool IsTouchAvailable => UnityEngine.Application.isMobilePlatform;

        private void Awake()
        {
            Bind(_pauseButton, () => PauseClicked?.Invoke());
            Bind(_mapButton, () => MapClicked?.Invoke());
        }

        private void OnEnable() => RefreshTouchControls();

        private void OnDisable()
        {
            if (_touchControls != null) _touchControls.SetShown(false);
        }

        private void Update()
        {
            if (!IsTouchAvailable && !_touchSeen)
            {
                var touch = Touchscreen.current;
                if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                {
                    _touchSeen = true;
                    RefreshTouchControls();
                }
            }

            if (_message != null && _message.text.Length > 0 && Time.unscaledTime >= _messageHideTime)
                _message.text = string.Empty;
        }

        private void RefreshTouchControls()
        {
            if (_touchControls != null)
                _touchControls.SetShown(isActiveAndEnabled && (IsTouchAvailable || _touchSeen));
        }

        public void SetLevelName(string levelName) => _levelName.text = levelName;

        /// <summary>HP, weapons, keys.</summary>
        public void SetStatus(string status)
        {
            if (_status != null) _status.text = status;
        }

        /// <summary>A short message (e.g. "Locked: needs the red key") that disappears by itself.</summary>
        public void ShowMessage(string message)
        {
            if (_message == null) return;
            _message.text = message;
            _messageHideTime = Time.unscaledTime + MessageSeconds;
        }

        public void ClearLevelInfo()
        {
            SetStatus(string.Empty);
            if (_message != null) _message.text = string.Empty;
        }
    }
}
