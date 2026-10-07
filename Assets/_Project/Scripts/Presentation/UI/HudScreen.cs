using System;
using Maze.Presentation.UI.Touch;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Maze.Presentation.UI
{
    public sealed class HudScreen : UIScreen
    {
        private const float MessageSeconds = 2f;

        [SerializeField] private Text _levelName;
        [SerializeField] private Text _status;
        [SerializeField] private Text _message;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _mapButton;

        [Tooltip("On-screen stick and buttons (ТЗ §64, Android): a separate canvas, shown while the HUD is. " +
                 "They drive gamepad controls through the Input System.")]
        [SerializeField] private TouchControls _touchControls;

        [Header("Damage pulse")]
        [Tooltip("Full-screen image under the HUD: red at the screen edges when the player is hurt. Its texture is made on start.")]
        [SerializeField] private RawImage _damageFlash;

        [SerializeField] private Color _damageColor = new Color(0.85f, 0.04f, 0.04f, 0.5f);
        [SerializeField, Min(0.05f)] private float _damageTime = 0.6f;

        private const int VignetteSize = 64;
        private const float DamageRise = 0.06f;

        private float _messageHideTime;
        private float _damageElapsed = float.MaxValue;
        private Texture2D _vignette;

        /// <summary>A real touch was seen on a platform that is not mobile (e.g. a desktop browser on a touchscreen).</summary>
        private static bool _touchSeen;

        public event Action PauseClicked;
        public event Action MapClicked;

        /// <summary>
        /// Touch controls are shown at once on mobile (including mobile browsers). Elsewhere a touchscreen device may be
        /// reported without one (desktop browsers expose touch support), so they appear only after a real touch.
        /// </summary>
        public static bool IsTouchAvailable => UnityEngine.Application.isMobilePlatform;

        private void Awake()
        {
            Bind(_pauseButton, () => PauseClicked?.Invoke());
            Bind(_mapButton, () => MapClicked?.Invoke());

            if (_damageFlash != null)
            {
                _vignette = CreateVignette();
                _damageFlash.texture = _vignette;
                _damageFlash.raycastTarget = false;
                _damageFlash.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_vignette != null) Destroy(_vignette);
        }

        private void OnEnable() => RefreshTouchControls();

        private void OnDisable()
        {
            if (_touchControls != null) _touchControls.SetShown(false);
        }

        private void Update()
        {
            if (!IsTouchAvailable && !_touchSeen)
            {
                var touch = Touchscreen.current;
                if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                {
                    _touchSeen = true;
                    RefreshTouchControls();
                }
            }

            if (_message != null && _message.text.Length > 0 && Time.unscaledTime >= _messageHideTime)
                _message.text = string.Empty;

            UpdateDamagePulse();
        }

        /// <summary>A red pulse at the screen edges (the player was hurt). Restarts if already shown.</summary>
        public void PulseDamage()
        {
            if (_damageFlash == null) return;
            _damageElapsed = 0f;
            _damageFlash.enabled = true;
            UpdateDamagePulse();
        }

        private void UpdateDamagePulse()
        {
            if (_damageFlash == null || !_damageFlash.enabled) return;

            if (_damageElapsed >= _damageTime)
            {
                // Off when not shown: a full-screen transparent image still costs fill rate on phones.
                _damageFlash.enabled = false;
                return;
            }

            var strength = _damageElapsed < DamageRise
                ? _damageElapsed / DamageRise
                : 1f - (_damageElapsed - DamageRise) / Mathf.Max(_damageTime - DamageRise, 0.01f);
            strength = Mathf.Clamp01(strength);
            var color = _damageColor;
            color.a *= strength * strength * (3f - 2f * strength);
            _damageFlash.color = color;
            _damageElapsed += Time.unscaledDeltaTime;
        }

        /// <summary>White, transparent in the middle, opaque towards the edges (a rounded-square falloff).</summary>
        private static Texture2D CreateVignette()
        {
            var texture = new Texture2D(VignetteSize, VignetteSize, TextureFormat.RGBA32, false)
            {
                name = "DamageVignette",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[VignetteSize * VignetteSize];
            for (var y = 0; y < VignetteSize; y++)
            for (var x = 0; x < VignetteSize; x++)
            {
                var u = Mathf.Abs((x + 0.5f) / VignetteSize * 2f - 1f);
                var v = Mathf.Abs((y + 0.5f) / VignetteSize * 2f - 1f);
                var edge = Mathf.Pow(Mathf.Pow(u, 4f) + Mathf.Pow(v, 4f), 0.25f);
                var alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1.1f, edge));
                pixels[y * VignetteSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void RefreshTouchControls()
        {
            if (_touchControls != null)
                _touchControls.SetShown(isActiveAndEnabled && (IsTouchAvailable || _touchSeen));
        }

        public void SetLevelName(string levelName) => _levelName.text = levelName;

        /// <summary>HP, weapons, keys.</summary>
        public void SetStatus(string status)
        {
            if (_status != null) _status.text = status;
        }

        /// <summary>A short message (e.g. "Locked: needs the red key") that disappears by itself.</summary>
        public void ShowMessage(string message)
        {
            if (_message == null) return;
            _message.text = message;
            _messageHideTime = Time.unscaledTime + MessageSeconds;
        }

        public void ClearLevelInfo()
        {
            SetStatus(string.Empty);
            if (_message != null) _message.text = string.Empty;
            _damageElapsed = float.MaxValue;
            if (_damageFlash != null) _damageFlash.enabled = false;
        }
    }
}
