using System;
using UnityEngine;

namespace Maze.Gameplay.Player
{
    /// <summary>
    /// Gameplay input abstraction (ТЗ §64). Implemented by the application's input service; gameplay never reads
    /// devices directly. The camera does not rotate, so screen up is always grid North.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>Desired movement: x = East, y = North, magnitude 0..1.</summary>
        Vector2 Move { get; }

        /// <summary>Pointer position in screen pixels when <see cref="LookIsPointer"/>, otherwise a stick direction.</summary>
        Vector2 Look { get; }

        bool LookIsPointer { get; }

        /// <summary>True while the attack control is held (automatic weapons).</summary>
        bool AttackHeld { get; }

        event Action AttackPressed;
        event Action InteractPressed;
        event Action SwitchMeleePressed;
        event Action SwitchRangedPressed;
        event Action OpenMapPressed;
    }
}
