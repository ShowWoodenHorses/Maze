using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Assets;
using Maze.Core.Visual;
using Maze.Gameplay.Level;
using Maze.Gameplay.Player;
using UnityEngine;

namespace Maze.Presentation.Visual
{
    /// <summary>
    /// Player view (ТЗ §79: PlayerRuntime → PlayerView): instantiates the player prefab after the player spawned and
    /// mirrors gameplay state every frame — position, facing, Animator "Speed" — and keeps the camera on the player.
    /// Must be registered after <see cref="PlayerSystem"/> (same load stage, registration order).
    /// </summary>
    public sealed class PlayerViewPresenter : ILevelLoadStep, ILevelLateTickable, IDisposable
    {
        private const float TurnSpeed = 900f; // degrees per second
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");

        private readonly PlayerSystem _player;
        private readonly PlayerVisualDefinition _visual;
        private readonly IAssetOwner _assets;
        private readonly LevelViewRoot _root;
        private readonly TopDownCamera _camera;

        private GameObject _view;
        private Animator _animator;

        public PlayerViewPresenter(PlayerSystem player, PlayerVisualDefinition visual, IAssetOwner assets,
            LevelViewRoot root, TopDownCamera camera)
        {
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
            _animator = FindSpeedAnimator(_view);
            Sync(0f, snap: true);
        }

        public void LateTick(float deltaTime) => Sync(deltaTime, snap: false);

        public void Dispose()
        {
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

            if (_animator != null)
                _animator.SetFloat(SpeedParameter, _player.SpeedFactor);

            _camera.Follow(_root.transform.TransformPoint(position), deltaTime, snap);
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
