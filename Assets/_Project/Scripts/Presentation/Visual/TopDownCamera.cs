using UnityEngine;

namespace Maze.Presentation.Visual
{
    /// <summary>
    /// Top-down game camera in the Game scene. Never rotates around Y, so screen up is always grid North
    /// (input relies on it). Frames the whole level while it loads, then follows the player.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownCamera : MonoBehaviour
    {
        [SerializeField, Range(30f, 90f)] private float _pitch = 60f;
        [SerializeField, Min(1f)] private float _margin = 1.05f;

        [Tooltip("Distance from the followed point along the view direction. ~13 shows the 11x11 visible area.")]
        [SerializeField, Min(1f)] private float _followDistance = 13f;

        [SerializeField, Min(0f)] private float _followSmoothTime = 0.12f;
        [SerializeField] private Camera _camera;

        private Vector3 _velocity;

        public Camera Camera => _camera;

        /// <summary>Places the camera so the whole <paramref name="bounds"/> fits the view.</summary>
        public void Frame(Bounds bounds)
        {
            transform.rotation = Quaternion.Euler(_pitch, 0f, 0f);

            var halfVertical = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            var halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * _camera.aspect);
            var radius = bounds.extents.magnitude * _margin;
            var distance = radius / Mathf.Sin(Mathf.Min(halfVertical, halfHorizontal));

            transform.position = bounds.center - transform.forward * distance;
            _camera.farClipPlane = Mathf.Max(_camera.farClipPlane, distance + radius * 2f);
        }

        /// <summary>Keeps <paramref name="target"/> in the centre of the view; smoothed unless <paramref name="snap"/>.</summary>
        public void Follow(Vector3 target, float deltaTime, bool snap)
        {
            transform.rotation = Quaternion.Euler(_pitch, 0f, 0f);
            var desired = target - transform.forward * _followDistance;
            if (snap || deltaTime <= 0f || _followSmoothTime <= 0f)
            {
                transform.position = desired;
                _velocity = Vector3.zero;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, _followSmoothTime,
                Mathf.Infinity, deltaTime);
        }
    }
}
