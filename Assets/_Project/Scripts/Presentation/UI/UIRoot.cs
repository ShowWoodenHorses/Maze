using System;
using Maze.Presentation.UI.Touch;
using UnityEngine;
using UnityEngine.EventSystems;

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
        [SerializeField] private ConfirmExitScreen _confirmExit;
        [SerializeField] private MapScreen _map;
        [SerializeField] private SettingsScreen _settings;

        [Tooltip("On-screen controls: a separate canvas with physical sizes, shown by the HUD.")]
        [SerializeField] private TouchControls _touchControls;

        [Tooltip("Frames per second in the bottom-right corner, over every screen; shown when the setting is on.")]
        [SerializeField] private FpsCounter _fpsCounter;

        [Tooltip("The scene's EventSystem: tells whether the pointer is over the UI (clicks there are not attacks).")]
        [SerializeField] private EventSystem _eventSystem;

        public MainMenuScreen MainMenu => _mainMenu;
        public LoadingScreen Loading => _loading;
        public ErrorScreen Error => _error;
        public HudScreen Hud => _hud;
        public PauseScreen Pause => _pause;
        public ResultScreen Result => _result;
        public ConfirmExitScreen ConfirmExit => _confirmExit;
        public MapScreen Map => _map;
        public SettingsScreen Settings => _settings;
        public TouchControls TouchControls => _touchControls;
        public FpsCounter FpsCounter => _fpsCounter;
        public EventSystem EventSystem => _eventSystem;

        /// <summary>A control asked for its sound (<see cref="UiSound"/>).</summary>
        public event Action<UiSoundKind> SoundRequested;

        public void RequestSound(UiSoundKind kind) => SoundRequested?.Invoke(kind);
    }
}
