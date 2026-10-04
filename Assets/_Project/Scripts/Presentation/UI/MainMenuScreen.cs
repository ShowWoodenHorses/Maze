using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Maze.Presentation.UI
{
    public sealed class MainMenuScreen : UIScreen
    {
        [SerializeField] private RectTransform _levelList;
        [SerializeField] private Button _levelButtonTemplate;
        [SerializeField] private Text _emptyLabel;

        private readonly List<Button> _buttons = new List<Button>();

        public event Action<string> LevelSelected;

        /// <summary>Rebuilds the level buttons: (levelId, display name) pairs in catalog order.</summary>
        public void SetLevels(IReadOnlyList<KeyValuePair<string, string>> levels)
        {
            foreach (var button in _buttons)
                Destroy(button.gameObject);
            _buttons.Clear();

            _levelButtonTemplate.gameObject.SetActive(false);
            _emptyLabel.gameObject.SetActive(levels.Count == 0);

            foreach (var level in levels)
            {
                var button = Instantiate(_levelButtonTemplate, _levelList);
                button.gameObject.name = "Level " + level.Key;
                button.GetComponentInChildren<Text>().text = level.Value;
                var levelId = level.Key;
                button.onClick.AddListener(() => LevelSelected?.Invoke(levelId));
                button.gameObject.SetActive(true);
                _buttons.Add(button);
            }
        }
    }
}
