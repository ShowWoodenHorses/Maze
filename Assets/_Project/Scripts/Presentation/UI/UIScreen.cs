using System;
using UnityEngine;
using UnityEngine.UI;

namespace Maze.Presentation.UI
{
    /// <summary>
    /// Base of application screens. Screens are passive views: they show data and raise events;
    /// <see cref="ScreenRouter"/> decides which screen is visible and what a click does.
    /// </summary>
    public abstract class UIScreen : MonoBehaviour
    {
        public bool IsVisible => gameObject.activeSelf;

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        protected static void Bind(Button button, Action action)
        {
            if (button != null)
                button.onClick.AddListener(() => action?.Invoke());
        }
    }
}
