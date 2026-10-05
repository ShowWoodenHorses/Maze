using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Maze.Core.Visual
{
    /// <summary>
    /// Shared combat visuals, Addressable at <see cref="Address"/>. Display only (ТЗ §79): prefabs are pooled and
    /// never decide hits. Any reference may be empty — that effect is simply not shown.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatVisual", menuName = "Maze/Visual/Combat Visual")]
    public sealed class CombatVisualDefinition : ScriptableObject
    {
        public const string Address = "Combat/Visual";

        [Tooltip("Flying bullet, oriented along +Z. Pivot at the bullet centre.")]
        [SerializeField] private AssetReferenceGameObject _bullet;

        [Tooltip("Short effect where a bullet stops (wall, door, target).")]
        [SerializeField] private AssetReferenceGameObject _impact;

        [Tooltip("Short effect of a melee attack in front of the player, oriented along +Z.")]
        [SerializeField] private AssetReferenceGameObject _meleeSwing;

        [Tooltip("Seconds the impact and swing effects stay before returning to the pool.")]
        [SerializeField, Min(0.02f)] private float _effectDuration = 0.15f;

        public AssetReferenceGameObject Bullet { get => _bullet; internal set => _bullet = value; }
        public AssetReferenceGameObject Impact { get => _impact; internal set => _impact = value; }
        public AssetReferenceGameObject MeleeSwing { get => _meleeSwing; internal set => _meleeSwing = value; }
        public float EffectDuration => _effectDuration;
    }
}
