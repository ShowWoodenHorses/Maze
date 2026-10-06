using UnityEngine;

namespace Maze.Presentation.Visual
{
    /// <summary>
    /// Where a character holds weapons (display only, set up by Maze → Dev → Build Weapons from the animation poses):
    /// <list type="bullet">
    /// <item><see cref="MeleeSocket"/> — child of the right hand; a melee weapon's grip pivot goes here, blade along
    /// its +Z (the sword clips grip consistently, so the weapon stays rigid in the hand);</item>
    /// <item><see cref="PalmRight"/> / <see cref="PalmLeft"/> — palm centres. A two-handed gun is posed every frame:
    /// grip in the right palm, barrel toward the left palm — the rifle clips hold it with both hands, but the right
    /// hand alone does not keep the same angle to the gun across clips.</item>
    /// </list>
    /// </summary>
    public sealed class CharacterWeaponRig : MonoBehaviour
    {
        [SerializeField] private Transform _meleeSocket;
        [SerializeField] private Transform _palmRight;
        [SerializeField] private Transform _palmLeft;

        public Transform MeleeSocket => _meleeSocket;
        public Transform PalmRight => _palmRight;
        public Transform PalmLeft => _palmLeft;

        public bool IsValid => _meleeSocket != null && _palmRight != null && _palmLeft != null;

        /// <summary>World rotation of a two-handed gun now: barrel from the right palm to the left one, kept upright.</summary>
        public Quaternion TwoHandedRotation()
        {
            var forward = _palmLeft.position - _palmRight.position;
            if (forward.sqrMagnitude < 1e-6f)
                return transform.rotation;
            var up = Vector3.ProjectOnPlane(Vector3.up, forward);
            return Quaternion.LookRotation(forward, up.sqrMagnitude > 1e-6f ? up : transform.up);
        }
    }
}
