using System;
using Maze.Application.Save;
using Maze.Gameplay.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Maze.Presentation.UI
{
    /// <summary>Controls settings (touch controls, aiming), opened from the main menu and the pause screen. Changes apply at once.</summary>
    public sealed class SettingsScreen : UIScreen
    {
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
        [Tooltip("Cycles Free / 8 / 4 aim directions.")]
        [SerializeField] private Button _aimModeButton;
        [SerializeField] private Text _aimModeLabel;
        [SerializeField] private Toggle _aimAssist;
        [SerializeField] private Toggle _showFps;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _backButton;

        public event Action<ControlsSettings> Changed;
        public event Action ResetClicked;

        /// <summary>The "Show FPS counter" toggle was switched (not part of the controls).</summary>
        public event Action<bool> ShowFpsChanged;
        public event Action BackClicked;

        private AimMode _aimMode;

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
            Bind(_aimModeButton, OnAimModeClicked);
            Bind(_resetButton, () => ResetClicked?.Invoke());
            Bind(_backButton, () => BackClicked?.Invoke());
        }

        public void SetShowFps(bool show)
        {
            if (_showFps != null) _showFps.SetIsOnWithoutNotify(show);
        }

        public void SetValues(ControlsSettings settings)
        {
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

        private ControlsSettings Read() => new ControlsSettings
        {
            StickDeadZone = _deadZone.value,
            StickSensitivity = _sensitivity.value,
            FloatingStick = _floatingStick.isOn,
            Size = _size.value,
            Opacity = _opacity.value,
            LeftHanded = _leftHanded.isOn,
            AimMode = _aimMode,
            AimAssist = _aimAssist.isOn,
        };

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

        private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

        private static void Range(Slider slider, float min, float max)
        {
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
        }
    }
}
