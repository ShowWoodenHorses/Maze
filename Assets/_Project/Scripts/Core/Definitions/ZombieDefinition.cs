using UnityEngine;

namespace Maze.Core.Definitions
{
    public enum ZombieDetectionType
    {
        VisionOnly = 0,
        HearingOnly = 1,
        VisionAndHearing = 2,
    }

    /// <summary>Gameplay behaviour of a zombie type. Appearance is defined by ZombieVisualSet, never here.</summary>
    [CreateAssetMenu(fileName = "ZombieDefinition", menuName = "Maze/Definitions/Zombie")]
    public sealed class ZombieDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private ZombieDetectionType _detectionType;

        [Header("Vision (VisionOnly)")]
        [SerializeField, Range(1f, 360f)] private float _visionAngle = 45f;
        [SerializeField, Min(0f)] private float _visionRange = 5f;

        [Header("Hearing (HearingOnly)")]
        [SerializeField, Min(0f)] private float _hearingRadius = 5f;

        [Header("Vision + Hearing")]
        [SerializeField, Min(0f)] private float _detectionRadius = 4f;

        [Header("Combat & Movement")]
        [SerializeField, Min(1f)] private float _maxHp = 30f;
        [SerializeField, Min(0f)] private float _damage = 10f;
        [Tooltip("Seconds between attacks.")]
        [SerializeField, Min(0.01f)] private float _attackInterval = 1f;
        [Tooltip("Cells per second.")]
        [SerializeField, Min(0.01f)] private float _moveSpeed = 1.5f;

        public string Id => _id;
        public ZombieDetectionType DetectionType => _detectionType;
        public float VisionAngle => _visionAngle;
        public float VisionRange => _visionRange;
        public float HearingRadius => _hearingRadius;
        public float DetectionRadius => _detectionRadius;
        public float MaxHp => _maxHp;
        public float Damage => _damage;
        public float AttackInterval => _attackInterval;
        public float MoveSpeed => _moveSpeed;
    }
}
