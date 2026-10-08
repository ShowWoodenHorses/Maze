using System;
using Maze.Application.Save;
using Maze.Gameplay.Combat;
using Maze.Presentation.UI.Style;
using TMPro;
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
    /// Settings, opened from the main menu and the pause screen: tabs (<see cref="SettingsTab"/>; the open one in the
    /// accent, underlined), one page shown at a time, rows "caption — control" with the value on the right; aim
    /// directions are three segments. "Reset tab" resets the shown page. Changes apply at once.
    /// </summary>
    public sealed class SettingsScreen : UIScreen
    {
        [SerializeField] private UiStyle _style;

        [Tooltip("In SettingsTab order.")]
        [SerializeField] private Button[] _tabButtons;
        [Tooltip("Tab captions, in SettingsTab order.")]
        [SerializeField] private TMP_Text[] _tabLabels;
        [Tooltip("Underlines of the open tab, in SettingsTab order.")]
        [SerializeField] private GameObject[] _tabUnderlines;
        [Tooltip("In SettingsTab order.")]
        [SerializeField] private GameObject[] _pages;

        [SerializeField] private Slider _deadZone;
        [SerializeField] private TMP_Text _deadZoneLabel;
        [SerializeField] private Slider _sensitivity;
        [SerializeField] private TMP_Text _sensitivityLabel;
        [SerializeField] private Slider _size;
        [SerializeField] private TMP_Text _sizeLabel;
        [SerializeField] private Slider _opacity;
        [SerializeField] private TMP_Text _opacityLabel;
        [SerializeField] private Toggle _floatingStick;
        [SerializeField] private Toggle _leftHanded;
        [SerializeField] private Button _editLayoutButton;
        [Tooltip("Aim direction segments: Free, Eight, Four (AimMode order); the chosen one in the accent.")]
        [SerializeField] private UiButton[] _aimButtons;
        [SerializeField] private Toggle _aimAssist;
        [SerializeField] private Slider _musicVolume;
        [SerializeField] private TMP_Text _musicVolumeLabel;
        [SerializeField] private Slider _sfxVolume;
        [SerializeField] private TMP_Text _sfxVolumeLabel;
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
            for (var i = 0; i < _aimButtons.Length; i++)
            {
                var mode = (AimMode)i;
                Bind(_aimButtons[i], () => OnAimModeClicked(mode));
            }
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
            for (var i = 0; i < _tabLabels.Length; i++)
                _tabLabels[i].color = i == (int)tab ? _style.Accent : _style.MutedText;
            for (var i = 0; i < _tabUnderlines.Length; i++)
                _tabUnderlines[i].SetActive(i == (int)tab);
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

        private void OnAimModeClicked(AimMode mode)
        {
            if (_aimMode == mode) return;
            _aimMode = mode;
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
            _deadZoneLabel.text = Percent(settings.StickDeadZone);
            _sensitivityLabel.text = Percent(settings.StickSensitivity);
            _sizeLabel.text = Percent(settings.Size);
            _opacityLabel.text = Percent(settings.Opacity);
            for (var i = 0; i < _aimButtons.Length; i++)
                _aimButtons[i].Accent = i == (int)settings.AimMode;
        }

        private void OnVolumesEdited()
        {
            UpdateVolumeLabels();
            VolumesChanged?.Invoke(_musicVolume.value, _sfxVolume.value);
        }

        private void UpdateVolumeLabels()
        {
            _musicVolumeLabel.text = Percent(_musicVolume.value);
            _sfxVolumeLabel.text = Percent(_sfxVolume.value);
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
