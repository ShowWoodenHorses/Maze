using UnityEngine;

namespace Maze.Presentation.UI
{
    /// <summary>Application UI canvas in the Bootstrap scene: references to every screen, wired in the scene.</summary>
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] private MainMenuScreen _mainMenu;
        [SerializeField] private LoadingScreen _loading;
        [SerializeField] private ErrorScreen _error;
        [SerializeField] private HudScreen _hud;
        [SerializeField] private PauseScreen _pause;
        [SerializeField] private ResultScreen _result;

        public MainMenuScreen MainMenu => _mainMenu;
        public LoadingScreen Loading => _loading;
        public ErrorScreen Error => _error;
        public HudScreen Hud => _hud;
        public PauseScreen Pause => _pause;
        public ResultScreen Result => _result;
    }
}
