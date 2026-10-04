using UnityEngine;

namespace Maze.Core.Definitions
{
    /// <summary>
    /// Gameplay parameters of the player. Shared by all levels, Addressable at <see cref="Address"/>.
    /// Visual representation is defined separately (<see cref="Visual.PlayerVisualDefinition"/>), never here.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerDefinition", menuName = "Maze/Definitions/Player")]
    public sealed class PlayerDefinition : ScriptableObject
    {
        public const string Address = "Player/Definition";

        [Tooltip("Cells (metres) per second at full stick deflection.")]
        [SerializeField, Min(0.1f)] private float _moveSpeed = 3.5f;

        [Tooltip("Half size of the player's square footprint used for collisions with walls and closed doors. " +
                 "Must be below 0.5 so the player fits a one-cell corridor.")]
        [SerializeField, Range(0.05f, 0.45f)] private float _bodyHalfSize = 0.3f;

        [Tooltip("How far (in cells) the player is nudged toward a side passage when sliding along a wall.")]
        [SerializeField, Range(0f, 0.5f)] private float _cornerAssist = 0.35f;

        [SerializeField, Min(1)] private int _maxHealth = 100;

        public float MoveSpeed => _moveSpeed;
        public float BodyHalfSize => _bodyHalfSize;
        public float CornerAssist => _cornerAssist;
        public int MaxHealth => _maxHealth;

        /// <summary>For tests and tools.</summary>
        internal void Configure(float moveSpeed, float bodyHalfSize, float cornerAssist)
        {
            _moveSpeed = moveSpeed;
            _bodyHalfSize = bodyHalfSize;
            _cornerAssist = cornerAssist;
        }
    }
}
