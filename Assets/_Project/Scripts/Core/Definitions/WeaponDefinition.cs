using UnityEngine;

namespace Maze.Core.Definitions
{
    public enum WeaponSlot
    {
        Melee = 0,
        Ranged = 1,
    }

    public enum FireMode
    {
        Single = 0,
        Automatic = 1,
    }

    /// <summary>
    /// Gameplay parameters of a weapon. Visual representation is defined separately
    /// (WeaponVisualDefinition), never here.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Maze/Definitions/Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private WeaponSlot _slot;
        [SerializeField, Min(0f)] private float _damage = 10f;

        [Tooltip("How far the attack is heard by zombies, in cells (ТЗ §71).")]
        [SerializeField, Min(0f)] private float _soundRadius = 4f;

        [Header("Melee")]
        [Tooltip("Attacks per second.")]
        [SerializeField, Min(0.01f)] private float _attackSpeed = 1f;
        [Tooltip("Reach from the player's centre to the target's edge, in cells.")]
        [SerializeField, Min(0.1f)] private float _meleeRange = 1.2f;
        [Tooltip("Width of the hit sector in front of the player, degrees.")]
        [SerializeField, Range(10f, 360f)] private float _meleeArc = 100f;

        [Header("Ranged")]
        [Tooltip("Seconds between shots.")]
        [SerializeField, Min(0.01f)] private float _fireInterval = 0.3f;
        [SerializeField, Min(1)] private int _magazineSize = 10;
        [Tooltip("Automatic reload time in seconds when the magazine is empty. Ammo reserve is infinite.")]
        [SerializeField, Min(0f)] private float _reloadTime = 1.5f;
        [SerializeField] private FireMode _fireMode;
        [Tooltip("Bullet speed, cells per second.")]
        [SerializeField, Min(1f)] private float _projectileSpeed = 20f;

        public string Id => _id;
        public WeaponSlot Slot => _slot;
        public float Damage => _damage;
        public float SoundRadius => _soundRadius;
        public float AttackSpeed => _attackSpeed;
        public float MeleeRange => _meleeRange;
        public float MeleeArc => _meleeArc;
        public float FireInterval => _fireInterval;
        public int MagazineSize => _magazineSize;
        public float ReloadTime => _reloadTime;
        public FireMode FireMode => _fireMode;
        public float ProjectileSpeed => _projectileSpeed;

        /// <summary>Seconds between attacks: melee 1 / AttackSpeed, ranged FireInterval.</summary>
        public float Cooldown => _slot == WeaponSlot.Melee ? 1f / _attackSpeed : _fireInterval;

        /// <summary>For tests and tools.</summary>
        internal void Configure(string id, WeaponSlot slot, int magazineSize = 10)
        {
            _id = id;
            _slot = slot;
            _magazineSize = magazineSize;
        }

        internal void ConfigureCombat(float damage, float attackSpeed = 1f, float fireInterval = 0.3f, float reloadTime = 1.5f,
            FireMode fireMode = FireMode.Single, float projectileSpeed = 20f, float meleeRange = 1.2f, float meleeArc = 100f,
            float soundRadius = 4f)
        {
            _damage = damage;
            _attackSpeed = attackSpeed;
            _fireInterval = fireInterval;
            _reloadTime = reloadTime;
            _fireMode = fireMode;
            _projectileSpeed = projectileSpeed;
            _meleeRange = meleeRange;
            _meleeArc = meleeArc;
            _soundRadius = soundRadius;
        }
    }
}
