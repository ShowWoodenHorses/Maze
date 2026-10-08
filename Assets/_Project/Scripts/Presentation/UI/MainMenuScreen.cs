using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Maze.Presentation.UI
{
    /// <summary>
    /// Main menu (minimal by the user's choice): the title, Play (opens the level select) and Settings; total stars and
    /// zombies killed at the bottom left; "Debug: reset progress" (development builds) and the version at the bottom
    /// right.
    /// </summary>
    public sealed class MainMenuScreen : UIScreen
    {
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private GameObject _summary;
        [SerializeField] private TMP_Text _stars;
        [SerializeField] private TMP_Text _kills;
        [SerializeField] private TMP_Text _version;

        [Header("Debug (development builds only)")]
        [SerializeField] private Button _debugResetProgressButton;

        public event Action PlayClicked;
        public event Action SettingsClicked;
        public event Action DebugResetProgressClicked;

        private void Awake()
        {
            Bind(_playButton, () => PlayClicked?.Invoke());
            Bind(_settingsButton, () => SettingsClicked?.Invoke());
            Bind(_debugResetProgressButton, () => DebugResetProgressClicked?.Invoke());
            if (_debugResetProgressButton != null)
                _debugResetProgressButton.gameObject.SetActive(Debug.isDebugBuild);
            if (_version != null) _version.text = "v" + UnityEngine.Application.version;
        }

        /// <summary>Overall progress; hidden while the catalog is empty.</summary>
        public void SetSummary(int stars, int maxStars, int kills)
        {
            if (_summary != null) _summary.SetActive(maxStars > 0);
            if (_stars != null) _stars.text = stars + " / " + maxStars;
            if (_kills != null) _kills.text = kills.ToString();
        }

        /// <summary>Play is unavailable without levels (run Build / Sync in the Level Designer).</summary>
        public void SetPlayable(bool playable)
        {
            if (_playButton != null) _playButton.interactable = playable;
        }
    }
}
