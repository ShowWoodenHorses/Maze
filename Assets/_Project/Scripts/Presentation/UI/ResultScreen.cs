using System;
using UnityEngine;
using UnityEngine.UI;

namespace Maze.Presentation.UI
{
    public sealed class ResultScreen : UIScreen
    {
        [SerializeField] private Text _title;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _menuButton;

        public event Action RetryClicked;
        public event Action MenuClicked;

        private void Awake()
        {
            Bind(_retryButton, () => RetryClicked?.Invoke());
            Bind(_menuButton, () => MenuClicked?.Invoke());
        }

        public void SetTitle(string title) => _title.text = title;
    }
}
