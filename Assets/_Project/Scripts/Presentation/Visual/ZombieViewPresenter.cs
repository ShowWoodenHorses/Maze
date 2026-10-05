using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Core.Common;
using Maze.Core.Level;
using Maze.Core.Visual;
using Maze.Gameplay.Level;
using Maze.Gameplay.Zombies;
using UnityEngine;

namespace Maze.Presentation.Visual
{
    /// <summary>
    /// Zombie views (ТЗ §41, §79): the saved visual of each zombie, mirroring position and facing every frame.
    /// Moves the view's cell in <see cref="EntityViewRegistry"/>, so visibility follows the zombie. Animator
    /// parameters, when the controller has them: float "Speed", triggers "Attack", "Hit". A killed zombie's view
    /// is removed. Must be registered after <see cref="ZombieSystem"/> (same load stage, registration order).
    /// </summary>
    public sealed class ZombieViewPresenter : ILevelLoadStep, ILevelLateTickable, IDisposable
    {
        private const float TurnSpeed = 540f; // degrees per second
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        private static readonly int HitTrigger = Animator.StringToHash("Hit");

        private readonly LevelData _level;
        private readonly ZombieSystem _zombies;
        private readonly LevelVisualSystem _visuals;
        private readonly EntityViewRegistry _views;
        private readonly LevelViewRoot _root;
        private readonly Dictionary<ZombieRuntime, Binding> _bound = new Dictionary<ZombieRuntime, Binding>();
        private bool _subscribed;

        public ZombieViewPresenter(LevelData level, ZombieSystem zombies, LevelVisualSystem visuals, EntityViewRegistry views,
            LevelViewRoot root)
        {
            _level = level;
            _zombies = zombies;
            _visuals = visuals;
            _views = views;
            _root = root;
        }

        public LevelLoadStage Stage => LevelLoadStage.SpawnZombies;

        public UniTask ExecuteAsync(CancellationToken cancellation)
        {
            foreach (var zombie in _zombies.Zombies)
                if (zombie.IsAlive)
                    Bind(zombie);

            if (!_subscribed)
            {
                _zombies.Spawned += Bind;
                _zombies.Died += OnDied;
                _zombies.Attacked += OnAttacked;
                _subscribed = true;
            }

            return UniTask.CompletedTask;
        }

        public void LateTick(float deltaTime)
        {
            foreach (var pair in _bound)
            {
                var zombie = pair.Key;
                var view = pair.Value.View;
                if (view.GameObject == null) continue;

                var transform = view.GameObject.transform;
                transform.localPosition = new Vector3(zombie.Position.x, 0f, zombie.Position.y);
                if (zombie.Facing.sqrMagnitude > 0f)
                {
                    var facing = Quaternion.LookRotation(new Vector3(zombie.Facing.x, 0f, zombie.Facing.y), Vector3.up);
                    transform.localRotation = Quaternion.RotateTowards(transform.localRotation, facing, TurnSpeed * deltaTime);
                }

                if (view.Cell != zombie.Cell)
                    _views.Move(view, zombie.Cell);
                if (pair.Value.HasSpeed)
                    pair.Value.Animator.SetFloat(SpeedParameter, zombie.SpeedFactor);
            }
        }

        public void Dispose()
        {
            if (_subscribed)
            {
                _zombies.Spawned -= Bind;
                _zombies.Died -= OnDied;
                _zombies.Attacked -= OnAttacked;
                _subscribed = false;
            }

            foreach (var zombie in _bound.Keys)
                zombie.Damaged -= OnDamaged;
            _bound.Clear();
        }

        private void Bind(ZombieRuntime zombie)
        {
            if (_bound.ContainsKey(zombie) || _visuals.EntityViews == null)
                return;

            var view = _visuals.EntityViews.Create(zombie.Id, VisualKind.Zombie, VisualResolver.ResolveObject(_level, zombie.Spawn),
                zombie.Cell, _root.transform);
            if (view == null)
            {
                GameLog.Warning(LogChannel.Visual, $"Zombie '{zombie.Id}' has no visual.");
                return;
            }

            // Facing comes from gameplay, not from the saved visual rotation.
            view.GameObject.transform.localRotation =
                Quaternion.LookRotation(new Vector3(zombie.Facing.x, 0f, zombie.Facing.y), Vector3.up);
            _views.Add(view);
            var animator = view.GameObject.GetComponentInChildren<Animator>();
            _bound[zombie] = new Binding
            {
                View = view,
                Animator = animator,
                HasSpeed = HasParameter(animator, SpeedParameter, AnimatorControllerParameterType.Float),
                HasAttack = HasParameter(animator, AttackTrigger, AnimatorControllerParameterType.Trigger),
                HasHit = HasParameter(animator, HitTrigger, AnimatorControllerParameterType.Trigger),
            };
            zombie.Damaged += OnDamaged;
        }

        private void OnDied(ZombieRuntime zombie)
        {
            zombie.Damaged -= OnDamaged;
            if (_bound.Remove(zombie))
                _views.Remove(zombie.Id);
        }

        private void OnAttacked(ZombieRuntime zombie)
        {
            if (_bound.TryGetValue(zombie, out var bound) && bound.HasAttack)
                bound.Animator.SetTrigger(AttackTrigger);
        }

        private void OnDamaged(ZombieRuntime zombie, float damage)
        {
            if (_bound.TryGetValue(zombie, out var bound) && bound.HasHit)
                bound.Animator.SetTrigger(HitTrigger);
        }

        private sealed class Binding
        {
            public EntityView View;
            public Animator Animator;
            public bool HasSpeed;
            public bool HasAttack;
            public bool HasHit;
        }

        private static bool HasParameter(Animator animator, int hash, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == hash && parameter.type == type)
                    return true;
            return false;
        }
    }
}
