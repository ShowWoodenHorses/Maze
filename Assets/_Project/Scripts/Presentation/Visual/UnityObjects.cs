namespace Maze.Presentation.Visual
{
    internal static class UnityObjects
    {
        /// <summary>Destroy in play mode, DestroyImmediate in edit mode (EditMode tests, tools).</summary>
        public static void Destroy(UnityEngine.Object target)
        {
            if (target == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
