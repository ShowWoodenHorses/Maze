using UnityEngine;

namespace Maze.Presentation.UI.Touch
{
    /// <summary>
    /// Fits a RectTransform into <see cref="Screen.safeArea"/> (notches, rounded corners). Its parent must cover the
    /// whole screen. Follows resolution changes.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _appliedArea;
        private Vector2Int _appliedScreen;

        private void OnEnable() => Apply();

        private void Update() => Apply();

        private void Apply()
        {
            var area = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen.x <= 0 || screen.y <= 0) return;
            if (area == _appliedArea && screen == _appliedScreen) return;
            _appliedArea = area;
            _appliedScreen = screen;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            rect.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
