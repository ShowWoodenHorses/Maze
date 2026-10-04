using System;
using UnityEngine;
using UnityEngine.UI;

namespace Maze.Presentation.UI
{
    public sealed class HudScreen : UIScreen
    {
        [SerializeField] private Text _levelName;
        [SerializeField] private Button _pauseButton;

        public event Action PauseClicked;

        private void Awake() => Bind(_pauseButton, () => PauseClicked?.Invoke());

        public void SetLevelName(string levelName) => _levelName.text = levelName;
    }
}
