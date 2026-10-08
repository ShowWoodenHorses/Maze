using System;
using Maze.Application.Save;
using Maze.Gameplay.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Maze.Presentation.UI
{
    /// <summary>Pages of the settings screen.</summary>
    public enum SettingsTab
    {
        /// <summary>On-screen controls: stick, size, opacity, layout.</summary>
        Controls,

        /// <summary>Aim directions, auto-aim.</summary>
        Shooting,

        /// <summary>Everything else: music and sound volumes, FPS counter.</summary>
        Other,
    }

    /// <summary>
    /// Settings, opened from the main menu and the pause screen: tabs (<see cref="SettingsTab"/>), one page shown at a
    /// time; "Reset to defaults" resets the shown page. Changes apply at once.
    /// </summary>
    public sealed class SettingsScreen : UIScreen
    {
        private static readonly Color TabColor = new Color(0.22f, 0.24f, 0.28f, 1f);
        private static readonly Color ActiveTabColor = new Color(0.42f, 0.46f, 0.55f, 1f);

        [Tooltip("In SettingsTab order.")]
        [SerializeField] private Button[] _tabButtons;
        [Tooltip("In SettingsTab order.")]
        [SerializeField] private GameObject[] _pages;

        [SerializeField] private Slider _deadZone;
        [SerializeField] private Text _deadZoneLabel;
        [SerializeField] private Slider _sensitivity;
        [SerializeField] private Text _sensitivityLabel;
        [SerializeField] private Slider _size;
        [SerializeField] private Text _sizeLabel;
        [SerializeField] private Slider _opacity;
        [SerializeField] private Text _opacityLabel;
        [SerializeField] private Toggle _floatingStick;
        [SerializeField] private Toggle _leftHanded;
        [SerializeField] private Button _editLayoutButton;
        [Tooltip("Cycles Free / 8 / 4 aim directions.")]
        [SerializeField] private Button _aimModeButton;
        [SerializeField] private Text _aimModeLabel;
        [SerializeField] private Toggle _aimAssist;
        [SerializeField] private Slider _musicVolume;
        [SerializeField] private Text _musicVolumeLabel;
        [SerializeField] private Slider _sfxVolume;
        [SerializeField] private Text _sfxVolumeLabel;
        [SerializeField] private Toggle _showFps;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _backButton;

        public event Action<ControlsSettings> Changed;
        /// <summary>"Reset to defaults" on the shown page.</summary>
        public event Action<SettingsTab> ResetClicked;

        /// <summary>"Edit button layout": drag the on-screen controls.</summary>
        public event Action EditLayoutClicked;

        /// <summary>(music, sounds) volume sliders moved, 0..1 (not part of the controls).</summary>
        public event Action<float, float> VolumesChanged;

        /// <summary>The "Show FPS counter" toggle was switched (not part of the controls).</summary>
        public event Action<bool> ShowFpsChanged;
        public event Action BackClicked;

        private AimMode _aimMode;

        public SettingsTab Tab { get; private set; }

        private void Awake()
        {
            Range(_deadZone, ControlsSettings.MinDeadZone, ControlsSettings.MaxDeadZone);
            Range(_sensitivity, 0f, 1f);
            Range(_size, ControlsSettings.MinSize, ControlsSettings.MaxSize);
            Range(_opacity, ControlsSettings.MinOpacity, ControlsSettings.MaxOpacity);
            foreach (var slider in new[] { _deadZone, _sensitivity, _size, _opacity })
                slider.onValueChanged.AddListener(_ => OnEdited());
            _floatingStick.onValueChanged.AddListener(_ => OnEdited());
            _leftHanded.onValueChanged.AddListener(_ => OnEdited());
            _aimAssist.onValueChanged.AddListener(_ => OnEdited());
            if (_showFps != null) _showFps.onValueChanged.AddListener(value => ShowFpsChanged?.Invoke(value));
            foreach (var slider in new[] { _musicVolume, _sfxVolume })
            {
                if (slider == null) continue;
                Range(slider, 0f, 1f);
                slider.onValueChanged.AddListener(_ => OnVolumesEdited());
            }
            Bind(_aimModeButton, OnAimModeClicked);
            Bind(_resetButton, () => ResetClicked?.Invoke(Tab));
            Bind(_editLayoutButton, () => EditLayoutClicked?.Invoke());
            Bind(_backButton, () => BackClicked?.Invoke());
            for (var i = 0; i < _tabButtons.Length; i++)
            {
                var tab = (SettingsTab)i;
                Bind(_tabButtons[i], () => ShowTab(tab));
            }

            ShowTab(SettingsTab.Controls);
        }

        public void ShowTab(SettingsTab tab)
        {
            Tab = tab;
            for (var i = 0; i < _pages.Length; i++)
                _pages[i].SetActive(i == (int)tab);
            for (var i = 0; i < _tabButtons.Length; i++)
                _tabButtons[i].targetGraphic.color = i == (int)tab ? ActiveTabColor : TabColor;
        }

        public void SetShowFps(bool show)
        {
            if (_showFps != null) _showFps.SetIsOnWithoutNotify(show);
        }

        public void SetVolumes(float music, float sfx)
        {
            if (_musicVolume == null || _sfxVolume == null) return;
            _musicVolume.SetValueWithoutNotify(music);
            _sfxVolume.SetValueWithoutNotify(sfx);
            UpdateVolumeLabels();
        }

        public void SetValues(ControlsSettings settings)
        {
            _values = settings;
            _deadZone.SetValueWithoutNotify(settings.StickDeadZone);
            _sensitivity.SetValueWithoutNotify(settings.StickSensitivity);
            _size.SetValueWithoutNotify(settings.Size);
            _opacity.SetValueWithoutNotify(settings.Opacity);
            _floatingStick.SetIsOnWithoutNotify(settings.FloatingStick);
            _leftHanded.SetIsOnWithoutNotify(settings.LeftHanded);
            _aimAssist.SetIsOnWithoutNotify(settings.AimAssist);
            _aimMode = settings.AimMode;
            UpdateLabels(settings);
        }

        private ControlsSettings _values;

        private ControlsSettings Read()
        {
            var settings = _values; // Keeps what the screen does not edit (the layout).
            settings.StickDeadZone = _deadZone.value;
            settings.StickSensitivity = _sensitivity.value;
            settings.FloatingStick = _floatingStick.isOn;
            settings.Size = _size.value;
            settings.Opacity = _opacity.value;
            settings.LeftHanded = _leftHanded.isOn;
            settings.AimMode = _aimMode;
            settings.AimAssist = _aimAssist.isOn;
            return settings;
        }

        private void OnAimModeClicked()
        {
            _aimMode = _aimMode switch
            {
                AimMode.Free => AimMode.Eight,
                AimMode.Eight => AimMode.Four,
                _ => AimMode.Free,
            };
            OnEdited();
        }

        private void OnEdited()
        {
            var settings = Read();
            _values = settings;
            UpdateLabels(settings);
            Changed?.Invoke(settings);
        }

        private void UpdateLabels(ControlsSettings settings)
        {
            _deadZoneLabel.text = "Stick dead zone: " + Percent(settings.StickDeadZone);
            _sensitivityLabel.text = "Stick sensitivity: " + Percent(settings.StickSensitivity);
            _sizeLabel.text = "Controls size: " + Percent(settings.Size);
            _opacityLabel.text = "Controls opacity: " + Percent(settings.Opacity);
            _aimModeLabel.text = settings.AimMode switch
            {
                AimMode.Eight => "Aim: 8 directions",
                AimMode.Four => "Aim: 4 directions",
                _ => "Aim: free",
            };
        }

        private void OnVolumesEdited()
        {
            UpdateVolumeLabels();
            VolumesChanged?.Invoke(_musicVolume.value, _sfxVolume.value);
        }

        private void UpdateVolumeLabels()
        {
            _musicVolumeLabel.text = "Music volume: " + Percent(_musicVolume.value);
            _sfxVolumeLabel.text = "Sound volume: " + Percent(_sfxVolume.value);
        }

        private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

        private static void Range(Slider slider, float min, float max)
        {
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
        }
    }
}
