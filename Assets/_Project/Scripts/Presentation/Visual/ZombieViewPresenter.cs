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
using Z = Maze.Presentation.Visual.ZombieAnimatorParameters;

namespace Maze.Presentation.Visual
{
    /// <summary>
    /// Zombie views (ТЗ §41, §79): the saved visual of each zombie, mirroring position and facing every frame.
    /// Moves the view's cell in <see cref="EntityViewRegistry"/>, so visibility follows the zombie. The Animator only
    /// shows state (<see cref="ZombieAnimatorParameters"/>, each optional): standing / walking (patrol, return) /
    /// roaring before a chase (Alert), running (chase) with the step rate matched to the real speed (<see cref="LocomotionAnimation"/>), attacks (three variants in turn, as long as the attack interval), death.
    /// A random idle variant (per zombie, deterministic) is picked whenever the zombie stops. A killed zombie's view
    /// stays for <see cref="CorpseTime"/> to play the death, then is removed.
    /// Must be registered after <see cref="ZombieSystem"/> (same load stage, registration order).
    /// </summary>
    public sealed class ZombieViewPresenter : ILevelLoadStep, ILevelLateTickable, IDisposable
    {
        /// <summary>Seconds a killed zombie's view stays (death animation), then it is removed.</summary>
        public const float CorpseTime = 4f;

        private const float TurnSpeed = 540f; // degrees per second
        private const float MovingThreshold = 0.1f;
        private static readonly int IdleState = Animator.StringToHash("Idle");

        private readonly LevelData _level;
        private readonly ZombieSystem _zombies;
        private readonly LevelVisualSystem _visuals;
        private readonly EntityViewRegistry _views;
        private readonly LevelViewRoot _root;
        private readonly Dictionary<ZombieRuntime, Binding> _bound = new Dictionary<ZombieRuntime, Binding>();
        private readonly List<Corpse> _corpses = new List<Corpse>();
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
                var bound = pair.Value;
                var view = bound.View;
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
                SyncAnimator(zombie, bound);
            }

            for (var i = _corpses.Count - 1; i >= 0; i--)
            {
                var corpse = _corpses[i];
                corpse.TimeLeft -= deltaTime;
                if (corpse.TimeLeft > 0f)
                {
                    _corpses[i] = corpse;
                    continue;
                }

                _views.Remove(corpse.EntityId);
                _corpses.RemoveAt(i);
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
            _corpses.Clear(); // their views are destroyed with the registry
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

            var bound = new Binding
            {
                View = view,
                Animator = view.GameObject.GetComponentInChildren<Animator>(),
                Random = new DeterministicRandom(unchecked((int)StableHash.Of(zombie.Id))),
            };
            bound.CacheParameters();
            _bound[zombie] = bound;
            zombie.Damaged += OnDamaged;

            if (bound.HasParameters)
            {
                var definition = zombie.Definition;
                bound.SetFloat(Z.WalkPlaybackHash, LocomotionAnimation.Playback(definition.MoveSpeed, bound.WalkGroundSpeed));
                bound.SetFloat(Z.RunPlaybackHash, LocomotionAnimation.Playback(definition.ChaseSpeed, bound.RunGroundSpeed));
                bound.SetFloat(Z.AttackSpeedHash, 1f / Mathf.Max(definition.AttackInterval, 0.01f));
                bound.SetFloat(Z.IdleVariantHash, bound.Random.NextInt(Z.IdleVariants));
                // Zombies standing next to each other should not breathe in sync.
                if (bound.Animator.HasState(0, IdleState))
                    bound.Animator.Play(IdleState, 0, (float)bound.Random.NextDouble());
            }
        }

        private static void SyncAnimator(ZombieRuntime zombie, Binding bound)
        {
            if (!bound.HasParameters) return;

            var moving = zombie.SpeedFactor > MovingThreshold;
            if (bound.WasMoving && !moving)
                bound.SetFloat(Z.IdleVariantHash, bound.Random.NextInt(Z.IdleVariants));
            bound.WasMoving = moving;

            bound.SetFloat(Z.SpeedHash, zombie.SpeedFactor);
            bound.SetBool(Z.ChasingHash, zombie.State == ZombieState.Chase);
            bound.SetBool(Z.AlertHash, zombie.State == ZombieState.Alert);
        }

        private void OnDied(ZombieRuntime zombie)
        {
            zombie.Damaged -= OnDamaged;
            if (!_bound.TryGetValue(zombie, out var bound))
                return;

            _bound.Remove(zombie);
            if (!bound.Has(Z.DeadHash))
            {
                _views.Remove(zombie.Id);
                return;
            }

            bound.SetFloat(Z.SpeedHash, 0f);
            bound.SetBool(Z.DeadHash, true);
            _corpses.Add(new Corpse { EntityId = zombie.Id, TimeLeft = CorpseTime });
        }

        private void OnAttacked(ZombieRuntime zombie)
        {
            if (!_bound.TryGetValue(zombie, out var bound)) return;
            bound.SetInt(Z.AttackIndexHash, bound.AttackCounter++ % Z.AttackVariants);
            bound.SetTrigger(Z.AttackHash);
        }

        private void OnDamaged(ZombieRuntime zombie, float damage)
        {
            if (_bound.TryGetValue(zombie, out var bound))
                bound.SetTrigger(Z.HitHash);
        }

        private struct Corpse
        {
            public string EntityId;
            public float TimeLeft;
        }

        private sealed class Binding
        {
            private readonly HashSet<int> _parameters = new HashSet<int>();

            public EntityView View;
            public Animator Animator;
            public DeterministicRandom Random;
            public int AttackCounter;
            public bool WasMoving;
            public float WalkGroundSpeed;
            public float RunGroundSpeed;

            public bool HasParameters => _parameters.Count > 0;

            /// <summary>Once per view: <c>animator.parameters</c> allocates.</summary>
            public void CacheParameters()
            {
                if (Animator == null || Animator.runtimeAnimatorController == null) return;
                foreach (var parameter in Animator.parameters)
                {
                    _parameters.Add(parameter.nameHash);
                    // Data the builder measured from the clips, stored as parameter defaults.
                    if (parameter.nameHash == Z.WalkGroundSpeedHash) WalkGroundSpeed = parameter.defaultFloat;
                    else if (parameter.nameHash == Z.RunGroundSpeedHash) RunGroundSpeed = parameter.defaultFloat;
                }
            }

            public bool Has(int hash) => _parameters.Contains(hash);

            public void SetFloat(int hash, float value)
            {
                if (Has(hash)) Animator.SetFloat(hash, value);
            }

            public void SetInt(int hash, int value)
            {
                if (Has(hash)) Animator.SetInteger(hash, value);
            }

            public void SetBool(int hash, bool value)
            {
                if (Has(hash)) Animator.SetBool(hash, value);
            }

            public void SetTrigger(int hash)
            {
                if (Has(hash)) Animator.SetTrigger(hash);
            }
        }
    }
}
