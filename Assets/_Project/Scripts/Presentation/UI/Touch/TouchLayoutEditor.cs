using System;
using Maze.Application.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Maze.Presentation.UI.Touch
{
    /// <summary>
    /// Layout editing of the on-screen controls (opened from the settings): the player drags the stick and buttons
    /// (<see cref="TouchLayoutHandle"/>) and sets the size of the selected one with a slider. Every change is raised
    /// as <see cref="Changed"/> (the presenter previews it through the settings, which re-applies
    /// <see cref="TouchControls"/>); <see cref="Done"/> ends editing. A panel with its own background on the
    /// <see cref="TouchControls"/> canvas.
    /// </summary>
    public sealed class TouchLayoutEditor : MonoBehaviour
    {
        private static readonly Color Selected = new Color(1f, 0.8f, 0.25f, 0.9f);

        [SerializeField] private TouchControls _controls;

        [Tooltip("Full-screen dim behind the controls while editing; a tap on it clears the selection.")]
        [SerializeField] private GameObject _background;

        [SerializeField] private Slider _size;
        [SerializeField] private Text _sizeLabel;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _doneButton;

        private TouchLayout _layout;
        private TouchElement _selected;
        private Image _highlighted;
        private Color _highlightedColor;
        private Vector2 _grabOffset;

        /// <summary>The edited layout (right-handed, normalized); raised on every move, resize and reset.</summary>
        public event Action<TouchLayout> Changed;

        public event Action Done;

        public bool IsEditing { get; private set; }

        private void Awake()
        {
            _size.minValue = TouchPlacement.MinScale;
            _size.maxValue = TouchPlacement.MaxScale;
            _size.wholeNumbers = false;
            _size.onValueChanged.AddListener(OnSizeChanged);
            _resetButton.onClick.AddListener(OnReset);
            _doneButton.onClick.AddListener(() => Done?.Invoke());
        }

        public void Begin(TouchLayout layout)
        {
            _layout = layout;
            IsEditing = true;
            gameObject.SetActive(true);
            if (_background != null) _background.SetActive(true);
            _controls.SetEditing(true);
            Select(TouchElement.Attack);
        }

        public void End()
        {
            if (!IsEditing) return;
            IsEditing = false;
            ClearHighlight();
            _controls.SetEditing(false);
            if (_background != null) _background.SetActive(false);
            gameObject.SetActive(false);
        }

        internal void OnHandleDown(TouchElement element, PointerEventData eventData)
        {
            Select(element);
            _grabOffset = ToArea(eventData, out var point) ? _controls.DisplayedCenter(element) - point : Vector2.zero;
        }

        internal void OnHandleDrag(TouchElement element, PointerEventData eventData)
        {
            if (element != _selected || !ToArea(eventData, out var point)) return;
            var placement = _layout[element];
            var shown = _controls.ClampDisplayed(element, point + _grabOffset, placement.Scale);
            placement.Moved = true;
            placement.Position = _controls.ToLayout(shown);
            _layout[element] = placement;
            Changed?.Invoke(_layout);
        }

        private void OnSizeChanged(float scale)
        {
            var placement = _layout[_selected];
            placement.Scale = scale;
            if (placement.Moved)
            {
                // Growing near an edge: keep the whole control on screen.
                var shown = _controls.ClampDisplayed(_selected, _controls.ToLayout(placement.Position), scale);
                placement.Position = _controls.ToLayout(shown);
            }

            _layout[_selected] = placement;
            UpdateSizeLabel();
            Changed?.Invoke(_layout);
        }

        private void OnReset()
        {
            _layout = TouchLayout.Default;
            Changed?.Invoke(_layout);
            Select(_selected);
        }

        private void Select(TouchElement element)
        {
            _selected = element;
            ClearHighlight();
            _highlighted = _controls.ElementRect(element).GetComponent<Image>();
            if (_highlighted != null)
            {
                _highlightedColor = _highlighted.color;
                _highlighted.color = Selected;
            }

            _size.SetValueWithoutNotify(_layout[element].Scale);
            UpdateSizeLabel();
        }

        private void ClearHighlight()
        {
            if (_highlighted != null) _highlighted.color = _highlightedColor;
            _highlighted = null;
        }

        private void UpdateSizeLabel() =>
            _sizeLabel.text = _selected + " size: " + Mathf.RoundToInt(_layout[_selected].Scale * 100f) + "%";

        /// <summary>Pointer position normalized to the controls' safe area.</summary>
        private bool ToArea(PointerEventData eventData, out Vector2 point)
        {
            var area = _controls.Area;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out var local))
            {
                point = default;
                return false;
            }

            var size = area.rect.size;
            point = size.x > 0f && size.y > 0f ? (local - area.rect.min) / size : Vector2.zero;
            return true;
        }
    }
}
