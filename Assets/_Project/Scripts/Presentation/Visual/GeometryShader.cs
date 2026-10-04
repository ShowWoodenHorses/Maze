using UnityEngine;

namespace Maze.Presentation.Visual
{
    /// <summary>Contract between the combined geometry meshes and the "Maze/Geometry" shader.</summary>
    public static class GeometryShader
    {
        /// <summary>Floor and wall materials must use this shader, otherwise single cells cannot be hidden.</summary>
        public const string Name = "Maze/Geometry";

        /// <summary>Mesh UV channel holding the grid cell (x, y) of every vertex.</summary>
        public const int CellUvChannel = 3;

        public const string VisibilityKeyword = "MAZE_VISIBILITY";

        public static readonly int VisibilityTextureId = Shader.PropertyToID("_MazeVisibility");
        public static readonly int VisibilitySizeId = Shader.PropertyToID("_MazeVisibilitySize");

        public static bool Supports(Material material) =>
            material != null && material.shader != null && material.shader.name == Name;
    }
}
