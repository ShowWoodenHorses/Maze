using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace Maze.Presentation.UI.Touch
{
    /// <summary>
    /// On-screen stick (ТЗ §64) that drives a gamepad stick through the Input System. Lives on an invisible zone
    /// (its side of the screen); the ring rests near the zone's bottom outer corner. Floating: the ring jumps to where
    /// the thumb touches the zone. Fixed: only touches on the ring count. Deflection is measured in canvas units
    /// (physical, see <see cref="TouchControls"/>) and shaped by <see cref="StickResponse"/>.
    /// The ring is anchored to the zone's bottom-left corner.
    /// </summary>
    public sealed class TouchStick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const int NoPointer = int.MinValue;

        /// <summary>A fixed stick also takes touches slightly outside the ring.</summary>
        private const float FixedGrabScale = 1.3f;

        [InputControl(layout = "Vector2")]
        [SerializeField] private string _controlPath = "<Gamepad>/leftStick";

        [SerializeField] private RectTransform _ring;
        [SerializeField] private RectTransform _knob;

        [Tooltip("Ring centre at rest, from the zone's bottom outer corner (canvas units).")]
        [SerializeField] private Vector2 _restFromCorner = new Vector2(220f, 220f);

        [Tooltip("Full travel of the stick (canvas units).")]
        [SerializeField] private float _radius = 100f;

        private float _deadZone = 0.2f;
        private float _exponent = 1.6f;
        private bool _floating = true;
        private bool _mirrored;
        private int _pointerId = NoPointer;

        protected override string controlPathInternal
        {
            get => _controlPath;
            set => _controlPath = value;
        }

        private bool IsHeld => _pointerId != NoPointer;

        private RectTransform Zone => (RectTransform)transform;

        public void Configure(float deadZone, float exponent, bool floating, bool mirrored)
        {
            _deadZone = deadZone;
            _exponent = exponent;
            _floating = floating;
            _mirrored = mirrored;
            if (!IsHeld) ResetToRest();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsHeld || !ToLocal(eventData, out var point)) return;

            if (_floating)
            {
                _ring.anchoredPosition = KeepInside(point);
            }
            else
            {
                var grab = _ring.rect.width * 0.5f * FixedGrabScale;
                if ((point - _ring.anchoredPosition).sqrMagnitude > grab * grab) return;
            }

            _pointerId = eventData.pointerId;
            Deflect(point);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId || !ToLocal(eventData, out var point)) return;
            Deflect(point);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            Release();
            SendValueToControl(Vector2.zero);
        }

        protected override void OnDisable()
        {
            Release();
            base.OnDisable(); // Resets the control.
        }

        private void LateUpdate()
        {
            // The rest point depends on the zone size (safe area, screen size), so it is followed while idle.
            if (!IsHeld) ResetToRest();
        }

        private void Deflect(Vector2 point)
        {
            var offset = point - _ring.anchoredPosition;
            if (_knob != null) _knob.anchoredPosition = Vector2.ClampMagnitude(offset, _radius);
            SendValueToControl(StickResponse.Shape(offset / _radius, _deadZone, _exponent));
        }

        private void Release()
        {
            _pointerId = NoPointer;
            ResetToRest();
        }

        private void ResetToRest()
        {
            if (_ring == null) return;
            var width = Zone.rect.width;
            var rest = _mirrored ? new Vector2(width - _restFromCorner.x, _restFromCorner.y) : _restFromCorner;
            _ring.anchoredPosition = KeepInside(rest);
            if (_knob != null) _knob.anchoredPosition = Vector2.zero;
        }

        /// <summary>Keeps the whole ring inside the zone.</summary>
        private Vector2 KeepInside(Vector2 point)
        {
            var size = Zone.rect.size;
            var half = _ring.rect.width * 0.5f;
            return new Vector2(
                Mathf.Clamp(point.x, half, Mathf.Max(half, size.x - half)),
                Mathf.Clamp(point.y, half, Mathf.Max(half, size.y - half)));
        }

        /// <summary>Point relative to the zone's bottom-left corner.</summary>
        private bool ToLocal(PointerEventData eventData, out Vector2 point)
        {
            var zone = Zone;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, eventData.position, eventData.pressEventCamera, out point))
                return false;
            point -= zone.rect.min;
            return true;
        }
    }
}
