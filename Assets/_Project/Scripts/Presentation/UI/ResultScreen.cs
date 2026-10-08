using System;
using Maze.Gameplay.Level;
using Maze.Presentation.UI.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Maze.Presentation.UI
{
    /// <summary>
    /// Result of a run (ТЗ §87, §89): LEVEL COMPLETE / LEVEL FAILED and the level name; three star slots with what
    /// each is for (exit, all zombies, all map fragments). Earned stars appear one by one with their sounds
    /// (<see cref="RevealNextStar"/> from the audio presenter); without sound they all appear after a while.
    /// Buttons: Menu, Retry and — after a completed level with another one after it — Next (the main action).
    /// </summary>
    public sealed class ResultScreen : UIScreen
    {
        private const float PopTime = 0.28f;
        private const float PopScale = 1.45f;

        /// <summary>Earned stars still hidden this long after the screen opened appear by themselves.</summary>
        private const float RevealFallback = 4f;

        [SerializeField] private UiStyle _style;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _levelName;
        [SerializeField] private Image[] _stars;
        [SerializeField] private TMP_Text[] _starLabels;
        [SerializeField] private Sprite _star;
        [SerializeField] private Sprite _starFilled;
        [SerializeField] private UiButton _retryButton;
        [SerializeField] private UiButton _menuButton;
        [SerializeField] private UiButton _nextButton;

        private readonly bool[] _earned = new bool[LevelResult.MaxStars];
        private readonly float[] _pop = { -1f, -1f, -1f };
        private int _revealed;
        private float _shownTime;

        public event Action RetryClicked;
        public event Action MenuClicked;
        public event Action NextClicked;

        private void Awake()
        {
            Bind(_retryButton, () => RetryClicked?.Invoke());
            Bind(_menuButton, () => MenuClicked?.Invoke());
            Bind(_nextButton, () => NextClicked?.Invoke());
        }

        /// <summary>Fills the screen; earned stars start hidden (empty slots) and are revealed one by one.</summary>
        public void SetResult(bool completed, LevelResult? result, string levelName, bool hasNext)
        {
            _title.text = completed ? "LEVEL COMPLETE" : "LEVEL FAILED";
            if (_levelName != null)
                _levelName.text = string.IsNullOrEmpty(levelName) ? string.Empty : levelName.ToUpperInvariant();

            var r = result ?? default;
            _earned[0] = completed;
            _earned[1] = completed && r.AllZombiesKilled;
            _earned[2] = completed && r.AllFragmentsCollected;
            SetLabel(0, completed ? "EXIT FOUND" : "NO EXIT", _earned[0]);
            SetLabel(1, "ZOMBIES " + r.Kills + "/" + r.TotalZombies, _earned[1]);
            SetLabel(2, "MAP " + r.Fragments + "/" + r.TotalFragments, _earned[2]);

            for (var i = 0; i < _stars.Length; i++)
            {
                _pop[i] = -1f;
                PaintStar(i, false);
            }

            _revealed = 0;
            _shownTime = Time.unscaledTime;

            var next = completed && hasNext;
            if (_nextButton != null) _nextButton.gameObject.SetActive(next);
            // The main action: Next after a win, Retry otherwise.
            if (_retryButton != null) _retryButton.Accent = !next;
        }

        /// <summary>Shows the next earned star with a little pop (one per star sound).</summary>
        public void RevealNextStar()
        {
            for (var i = _revealed; i < _earned.Length; i++)
            {
                _revealed = i + 1;
                if (!_earned[i]) continue;
                PaintStar(i, true);
                _pop[i] = 0f;
                return;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime - _shownTime > RevealFallback && _revealed < _earned.Length)
                while (_revealed < _earned.Length) RevealNextStar();

            for (var i = 0; i < _stars.Length; i++)
            {
                if (_pop[i] < 0f) continue;
                _pop[i] += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(_pop[i] / PopTime);
                var scale = Mathf.Lerp(PopScale, 1f, 1f - (1f - t) * (1f - t));
                _stars[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
                if (t >= 1f) _pop[i] = -1f;
            }
        }

        private void PaintStar(int index, bool filled)
        {
            var image = _stars[index];
            image.sprite = filled ? _starFilled : _star;
            var color = filled ? _style.Accent : _style.Line;
            if (!filled) color.a *= 0.45f;
            image.color = color;
            image.rectTransform.localScale = Vector3.one;
        }

        private void SetLabel(int index, string text, bool earned)
        {
            if (_starLabels == null || index >= _starLabels.Length || _starLabels[index] == null) return;
            _starLabels[index].text = text;
            _starLabels[index].color = earned ? _style.Text : _style.MutedText;
        }
    }
}
