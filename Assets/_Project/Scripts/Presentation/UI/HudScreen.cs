using System;
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

        [Tooltip("On-screen stick and buttons (ТЗ §64, Android). They drive gamepad controls through the Input System.")]
        [SerializeField] private GameObject _touchControls;

        private float _messageHideTime;

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
            if (_touchControls != null)
                _touchControls.SetActive(IsTouchAvailable);
        }

        private void Update()
        {
            if (_touchControls != null && !_touchControls.activeSelf)
            {
                var touch = Touchscreen.current;
                if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                    _touchControls.SetActive(true);
            }

            if (_message != null && _message.text.Length > 0 && Time.unscaledTime >= _messageHideTime)
                _message.text = string.Empty;
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
