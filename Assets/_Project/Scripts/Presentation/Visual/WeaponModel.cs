using UnityEngine;

namespace Maze.Presentation.Visual
{
    /// <summary>
    /// Marks of a weapon model (display only, set up by Maze → Dev → Build Weapons): the muzzle of a gun (shots,
    /// flashes) and the far end of a melee weapon (swing trails). Pivot is the grip, the weapon points along +Z.
    /// </summary>
    public sealed class WeaponModel : MonoBehaviour
    {
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Transform _tip;

        /// <summary>End of the barrel, facing along it; null for melee.</summary>
        public Transform Muzzle => _muzzle;

        /// <summary>Far end of the blade or head; null for guns.</summary>
        public Transform Tip => _tip;
    }
}
