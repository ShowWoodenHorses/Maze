using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Maze.Presentation.UI
{
    /// <summary>One button of the level list.</summary>
    public readonly struct LevelListItem
    {
        public LevelListItem(string levelId, string label, bool interactable)
        {
            LevelId = levelId;
            Label = label;
            Interactable = interactable;
        }

        public string LevelId { get; }
        public string Label { get; }
        public bool Interactable { get; }
    }

    public sealed class MainMenuScreen : UIScreen
    {
        [SerializeField] private RectTransform _levelList;
        [SerializeField] private Button _levelButtonTemplate;
        [SerializeField] private Text _emptyLabel;
        [SerializeField] private Text _summary;
        [SerializeField] private Button _settingsButton;

        [Header("Debug (development builds only)")]
        [SerializeField] private Button _debugResetProgressButton;

        private readonly List<Button> _buttons = new List<Button>();

        public event Action<string> LevelSelected;
        public event Action SettingsClicked;
        public event Action DebugResetProgressClicked;

        private void Awake()
        {
            Bind(_settingsButton, () => SettingsClicked?.Invoke());
            Bind(_debugResetProgressButton, () => DebugResetProgressClicked?.Invoke());
            if (_debugResetProgressButton != null)
                _debugResetProgressButton.gameObject.SetActive(Debug.isDebugBuild);
        }

        /// <summary>Rebuilds the level buttons in catalog order.</summary>
        public void SetLevels(IReadOnlyList<LevelListItem> levels)
        {
            foreach (var button in _buttons)
                Destroy(button.gameObject);
            _buttons.Clear();

            _levelButtonTemplate.gameObject.SetActive(false);
            _emptyLabel.gameObject.SetActive(levels.Count == 0);

            foreach (var level in levels)
            {
                var button = Instantiate(_levelButtonTemplate, _levelList);
                button.gameObject.name = "Level " + level.LevelId;
                button.GetComponentInChildren<Text>().text = level.Label;
                button.interactable = level.Interactable;
                var levelId = level.LevelId;
                button.onClick.AddListener(() => LevelSelected?.Invoke(levelId));
                button.gameObject.SetActive(true);
                _buttons.Add(button);
            }
        }

        /// <summary>Overall progress line (total stars, kills).</summary>
        public void SetSummary(string summary)
        {
            if (_summary != null) _summary.text = summary;
        }
    }
}
