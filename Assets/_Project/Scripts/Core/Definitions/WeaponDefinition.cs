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

        [Header("Melee")]
        [Tooltip("Attacks per second.")]
        [SerializeField, Min(0.01f)] private float _attackSpeed = 1f;

        [Header("Ranged")]
        [Tooltip("Seconds between shots.")]
        [SerializeField, Min(0.01f)] private float _fireInterval = 0.3f;
        [SerializeField, Min(1)] private int _magazineSize = 10;
        [Tooltip("Automatic reload time in seconds when the magazine is empty. Ammo reserve is infinite.")]
        [SerializeField, Min(0f)] private float _reloadTime = 1.5f;
        [SerializeField] private FireMode _fireMode;

        public string Id => _id;
        public WeaponSlot Slot => _slot;
        public float Damage => _damage;
        public float AttackSpeed => _attackSpeed;
        public float FireInterval => _fireInterval;
        public int MagazineSize => _magazineSize;
        public float ReloadTime => _reloadTime;
        public FireMode FireMode => _fireMode;

        /// <summary>For tests and tools.</summary>
        internal void Configure(string id, WeaponSlot slot, int magazineSize = 10)
        {
            _id = id;
            _slot = slot;
            _magazineSize = magazineSize;
        }
    }
}
