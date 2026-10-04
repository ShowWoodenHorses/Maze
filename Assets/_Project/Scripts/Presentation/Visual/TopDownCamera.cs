using UnityEngine;

namespace Maze.Presentation.Visual
{
    /// <summary>
    /// Top-down game camera in the Game scene. For now it frames the whole level; following the player comes
    /// with the player stage.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownCamera : MonoBehaviour
    {
        [SerializeField, Range(30f, 90f)] private float _pitch = 60f;
        [SerializeField, Min(1f)] private float _margin = 1.05f;
        [SerializeField] private Camera _camera;

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
    }
}
