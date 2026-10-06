using UnityEngine;

namespace Maze.Presentation.Visual
{
    /// <summary>
    /// Animator contract of the player prefab (parameter names). The controller is built by
    /// Maze → Dev → Build Player Animations; <see cref="PlayerViewPresenter"/> drives it. Every parameter is optional:
    /// a controller without one simply does not get it.
    /// </summary>
    public static class PlayerAnimatorParameters
    {
        /// <summary>Float 0..1: movement speed relative to the maximum.</summary>
        public const string Speed = "Speed";
        /// <summary>Int: weapon in hands, see <see cref="WeaponNone"/>, <see cref="WeaponMelee"/>, <see cref="WeaponRanged"/>.</summary>
        public const string Weapon = "Weapon";
        /// <summary>Trigger: melee attack.</summary>
        public const string Attack = "Attack";
        /// <summary>Int: which melee attack animation to play (cycles 0..<see cref="AttackVariants"/>-1).</summary>
        public const string AttackIndex = "AttackIndex";
        /// <summary>Float: playback multiplier of melee attacks, 1 / cooldown (attack states last 1 s at 1).</summary>
        public const string AttackSpeed = "AttackSpeed";
        /// <summary>Trigger: a shot.</summary>
        public const string Shoot = "Shoot";
        /// <summary>Bool: the ranged weapon in hands is automatic.</summary>
        public const string Automatic = "Automatic";
        /// <summary>Bool: the weapon in hands is reloading.</summary>
        public const string Reloading = "Reloading";
        /// <summary>Float: playback multiplier of the reload, 1 / reload time (the reload state lasts 1 s at 1).</summary>
        public const string ReloadSpeed = "ReloadSpeed";
        /// <summary>Trigger: the player took damage.</summary>
        public const string Hit = "Hit";
        /// <summary>Trigger: a successful Interact (door, weapon pickup).</summary>
        public const string Use = "Use";
        /// <summary>Bool: the player is dead.</summary>
        public const string Dead = "Dead";

        public const int WeaponNone = 0;
        public const int WeaponMelee = 1;
        public const int WeaponRanged = 2;

        public const int AttackVariants = 3;

        /// <summary>Layer with reload, hit and use reactions over the upper body.</summary>
        public const string UpperBodyLayer = "UpperBody";

        public static readonly int SpeedHash = Animator.StringToHash(Speed);
        public static readonly int WeaponHash = Animator.StringToHash(Weapon);
        public static readonly int AttackHash = Animator.StringToHash(Attack);
        public static readonly int AttackIndexHash = Animator.StringToHash(AttackIndex);
        public static readonly int AttackSpeedHash = Animator.StringToHash(AttackSpeed);
        public static readonly int ShootHash = Animator.StringToHash(Shoot);
        public static readonly int AutomaticHash = Animator.StringToHash(Automatic);
        public static readonly int ReloadingHash = Animator.StringToHash(Reloading);
        public static readonly int ReloadSpeedHash = Animator.StringToHash(ReloadSpeed);
        public static readonly int HitHash = Animator.StringToHash(Hit);
        public static readonly int UseHash = Animator.StringToHash(Use);
        public static readonly int DeadHash = Animator.StringToHash(Dead);
    }
}
