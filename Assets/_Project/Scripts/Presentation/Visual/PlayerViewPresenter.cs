using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Assets;
using Maze.Core.Definitions;
using Maze.Core.Visual;
using Maze.Gameplay.Combat;
using Maze.Gameplay.Level;
using Maze.Gameplay.Player;
using Maze.Gameplay.Weapons;
using UnityEngine;

namespace Maze.Presentation.Visual
{
    /// <summary>
    /// Player view (ТЗ §79: PlayerRuntime → PlayerView): instantiates the player prefab after the player spawned and
    /// mirrors gameplay state every frame — position, facing, Animator "Speed" — and keeps the camera on the player.
    /// Attacks fire Animator triggers "Attack" (melee) and "Shoot" (ranged) when the controller has them.
    /// Must be registered after <see cref="PlayerSystem"/> (same load stage, registration order).
    /// </summary>
    public sealed class PlayerViewPresenter : ILevelLoadStep, ILevelLateTickable, IDisposable
    {
        private const float TurnSpeed = 900f; // degrees per second
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        private static readonly int ShootTrigger = Animator.StringToHash("Shoot");

        private readonly PlayerSystem _player;
        private readonly PlayerVisualDefinition _visual;
        private readonly IAssetOwner _assets;
        private readonly LevelViewRoot _root;
        private readonly TopDownCamera _camera;
        private readonly PlayerCombat _combat;

        private GameObject _view;
        private Animator _animator;
        private bool _hasSpeed;
        private bool _hasAttackTrigger;
        private bool _hasShootTrigger;

        public PlayerViewPresenter(PlayerSystem player, PlayerVisualDefinition visual, IAssetOwner assets,
            LevelViewRoot root, TopDownCamera camera, PlayerCombat combat)
        {
            _combat = combat;
            _player = player;
            _visual = visual;
            _assets = assets;
            _root = root;
            _camera = camera;
        }

        public LevelLoadStage Stage => LevelLoadStage.SpawnPlayer;

        public GameObject View => _view;

        public async UniTask ExecuteAsync(CancellationToken cancellation)
        {
            if (!_player.IsSpawned)
                throw new InvalidOperationException($"{nameof(PlayerViewPresenter)} must run after {nameof(PlayerSystem)} spawned the player.");

            var prefab = await _assets.LoadAsync<GameObject>(_visual.Prefab, cancellation);
            cancellation.ThrowIfCancellationRequested();

            _view = UnityEngine.Object.Instantiate(prefab, _root.transform);
            _view.name = "Player";
            var speedAnimator = FindSpeedAnimator(_view);
            _hasSpeed = speedAnimator != null;
            _animator = speedAnimator ?? _view.GetComponentInChildren<Animator>();
            _hasAttackTrigger = HasTrigger(_animator, AttackTrigger);
            _hasShootTrigger = HasTrigger(_animator, ShootTrigger);
            _combat.Attacked += OnAttacked;
            Sync(0f, snap: true);
        }

        public void LateTick(float deltaTime) => Sync(deltaTime, snap: false);

        public void Dispose()
        {
            _combat.Attacked -= OnAttacked;
            UnityObjects.Destroy(_view);
            _view = null;
            _animator = null;
        }

        private void Sync(float deltaTime, bool snap)
        {
            if (_view == null) return;

            var position = new Vector3(_player.Position.x, 0f, _player.Position.y);
            var facing = Quaternion.LookRotation(new Vector3(_player.Facing.x, 0f, _player.Facing.y), Vector3.up);
            var transform = _view.transform;
            transform.localPosition = position;
            transform.localRotation = snap
                ? facing
                : Quaternion.RotateTowards(transform.localRotation, facing, TurnSpeed * deltaTime);

            if (_hasSpeed)
                _animator.SetFloat(SpeedParameter, _player.SpeedFactor);

            _camera.Follow(_root.transform.TransformPoint(position), deltaTime, snap);
        }

        private void OnAttacked(WeaponRuntime weapon, Vector2 direction)
        {
            if (_animator == null) return;
            if (weapon.Slot == WeaponSlot.Melee && _hasAttackTrigger) _animator.SetTrigger(AttackTrigger);
            else if (weapon.Slot == WeaponSlot.Ranged && _hasShootTrigger) _animator.SetTrigger(ShootTrigger);
        }

        private static bool HasTrigger(Animator animator, int hash)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Trigger)
                    return true;
            return false;
        }

        /// <summary>The first Animator with a float "Speed" parameter, or null (animation is optional).</summary>
        private static Animator FindSpeedAnimator(GameObject view)
        {
            var animator = view.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
                return null;

            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == SpeedParameter && parameter.type == AnimatorControllerParameterType.Float)
                    return animator;

            return null;
        }
    }
}
