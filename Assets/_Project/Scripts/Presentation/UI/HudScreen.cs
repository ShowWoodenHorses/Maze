using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Maze.Presentation.UI
{
    public sealed class HudScreen : UIScreen
    {
        [SerializeField] private Text _levelName;
        [SerializeField] private Button _pauseButton;

        [Tooltip("On-screen stick and buttons (ТЗ §64, Android). They drive gamepad controls through the Input System.")]
        [SerializeField] private GameObject _touchControls;

        public event Action PauseClicked;

        /// <summary>Touch controls are shown on mobile and on any device with a touchscreen.</summary>
        public static bool IsTouchAvailable => UnityEngine.Application.isMobilePlatform || Touchscreen.current != null;

        private void Awake()
        {
            Bind(_pauseButton, () => PauseClicked?.Invoke());
            if (_touchControls != null)
                _touchControls.SetActive(IsTouchAvailable);
        }

        public void SetLevelName(string levelName) => _levelName.text = levelName;
    }
}
