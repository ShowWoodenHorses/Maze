using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Assets;
using Maze.Core.Definitions;
using Maze.Core.Visual;
using Maze.Gameplay.Combat;
using Maze.Gameplay.Level;
using Maze.Gameplay.Player;
using Maze.Gameplay.Visibility;
using Maze.Gameplay.Weapons;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;

namespace Maze.Presentation.Visual
{
    /// <summary>
    /// Views of combat (ТЗ §79, §83): pooled bullets that follow <see cref="BulletSystem"/>, short impact effects
    /// where bullets stop and a swing effect for melee attacks. Display only; hidden outside the player's sight
    /// (ТЗ §54). Prefabs come from <see cref="CombatVisualDefinition"/>; a missing prefab means no such effect.
    /// The pools are filled while loading (<see cref="PrefilledBullets"/>, ...), so the first shots create nothing.
    /// </summary>
    public sealed class CombatViewPresenter : ILevelLoadStep, ILevelLateTickable, IViewWarmup, IDisposable
    {
        private const float BulletHeight = 0.7f;
        private const float SwingDistance = 0.6f;
        public const int PrefilledBullets = 8;
        public const int PrefilledImpacts = 4;
        public const int PrefilledSwings = 2;

        private readonly CombatVisualDefinition _visual;
        private readonly IAssetOwner _assets;
        private readonly LevelViewRoot _root;
        private readonly BulletSystem _bullets;
        private readonly PlayerCombat _combat;
        private readonly PlayerSystem _player;
        private readonly VisibilitySystem _visibility;

        private readonly Dictionary<Bullet, GameObject> _bulletViews = new Dictionary<Bullet, GameObject>();
        private readonly List<(GameObject View, ObjectPool<GameObject> Pool, float Left)> _effects =
            new List<(GameObject, ObjectPool<GameObject>, float)>();
        private readonly List<GameObject> _prefill = new List<GameObject>();
        private readonly List<GameObject> _warmup = new List<GameObject>();

        private ObjectPool<GameObject> _bulletPool;
        private ObjectPool<GameObject> _impactPool;
        private ObjectPool<GameObject> _swingPool;
        private bool _subscribed;

        public CombatViewPresenter(CombatVisualDefinition visual, IAssetOwner assets, LevelViewRoot root, BulletSystem bullets,
            PlayerCombat combat, PlayerSystem player, VisibilitySystem visibility)
        {
            _visual = visual;
            _assets = assets;
            _root = root;
            _bullets = bullets;
            _combat = combat;
            _player = player;
            _visibility = visibility;
        }

        public LevelLoadStage Stage => LevelLoadStage.BuildVisuals;

        public int ActiveBulletViews => _bulletViews.Count;

        public async UniTask ExecuteAsync(CancellationToken cancellation)
        {
            var (bullet, impact, swing) = await UniTask.WhenAll(
                Load(_visual.Bullet, cancellation), Load(_visual.Impact, cancellation), Load(_visual.MeleeSwing, cancellation));
            cancellation.ThrowIfCancellationRequested();

            _bulletPool = CreatePool(bullet, "Bullet");
            _impactPool = CreatePool(impact, "Impact");
            _swingPool = CreatePool(swing, "MeleeSwing");
            Prefill(_bulletPool, PrefilledBullets);
            Prefill(_impactPool, PrefilledImpacts);
            Prefill(_swingPool, PrefilledSwings);

            if (!_subscribed)
            {
                _bullets.Spawned += OnBulletSpawned;
                _bullets.Ended += OnBulletEnded;
                _combat.Attacked += OnAttacked;
                _subscribed = true;
            }
        }

        public void LateTick(float deltaTime)
        {
            foreach (var pair in _bulletViews)
            {
                var bullet = pair.Key;
                var view = pair.Value;
                Place(view.transform, bullet.Position, bullet.Direction);
                view.SetActive(_visibility.IsVisible(Cell(bullet.Position)));
            }

            for (var i = _effects.Count - 1; i >= 0; i--)
            {
                var effect = _effects[i];
                effect.Left -= deltaTime;
                if (effect.Left > 0f)
                {
                    _effects[i] = effect;
                    continue;
                }

                effect.Pool.Release(effect.View);
                _effects.RemoveAt(i);
            }
        }

        public void CollectWarmup(List<GameObject> objects) => objects.AddRange(_warmup);

        public void Dispose()
        {
            if (_subscribed)
            {
                _bullets.Spawned -= OnBulletSpawned;
                _bullets.Ended -= OnBulletEnded;
                _combat.Attacked -= OnAttacked;
                _subscribed = false;
            }

            foreach (var view in _bulletViews.Values)
                UnityObjects.Destroy(view);
            _bulletViews.Clear();
            foreach (var effect in _effects)
                UnityObjects.Destroy(effect.View);
            _effects.Clear();
            _warmup.Clear();

            _bulletPool?.Dispose();
            _impactPool?.Dispose();
            _swingPool?.Dispose();
            _bulletPool = _impactPool = _swingPool = null;
        }

        private UniTask<GameObject> Load(AssetReferenceGameObject reference, CancellationToken cancellation) =>
            reference != null && reference.RuntimeKeyIsValid()
                ? _assets.LoadAsync<GameObject>(reference, cancellation)
                : UniTask.FromResult<GameObject>(null);

        private ObjectPool<GameObject> CreatePool(GameObject prefab, string name)
        {
            if (prefab == null)
                return null;

            return new ObjectPool<GameObject>(
                () =>
                {
                    var instance = UnityEngine.Object.Instantiate(prefab, _root.transform);
                    instance.name = name;
                    return instance;
                },
                view => view.SetActive(true),
                view => view.SetActive(false),
                UnityObjects.Destroy,
                collectionCheck: false, defaultCapacity: 8);
        }

        private void Prefill(ObjectPool<GameObject> pool, int count)
        {
            if (pool == null) return;
            for (var i = 0; i < count; i++)
                _prefill.Add(pool.Get());
            _warmup.Add(_prefill[0]); // one of each kind is enough to draw
            foreach (var view in _prefill)
                pool.Release(view);
            _prefill.Clear();
        }

        private void OnBulletSpawned(Bullet bullet)
        {
            if (_bulletPool == null) return;
            var view = _bulletPool.Get();
            Place(view.transform, bullet.Position, bullet.Direction);
            view.SetActive(_visibility.IsVisible(Cell(bullet.Position)));
            _bulletViews[bullet] = view;
        }

        private void OnBulletEnded(Bullet bullet, Vector2 point, BulletEnd reason)
        {
            if (_bulletViews.TryGetValue(bullet, out var view))
            {
                _bulletViews.Remove(bullet);
                _bulletPool.Release(view);
            }

            if (reason == BulletEnd.Blocked || reason == BulletEnd.Hit)
                ShowEffect(_impactPool, point, -bullet.Direction);
        }

        private void OnAttacked(WeaponRuntime weapon, Vector2 direction)
        {
            if (weapon.Slot == WeaponSlot.Melee)
                ShowEffect(_swingPool, _player.Position + direction * SwingDistance, direction);
        }

        private void ShowEffect(ObjectPool<GameObject> pool, Vector2 position, Vector2 direction)
        {
            if (pool == null || !_visibility.IsVisible(Cell(position)))
                return;

            var view = pool.Get();
            Place(view.transform, position, direction);
            _effects.Add((view, pool, _visual.EffectDuration));
        }

        private static void Place(Transform view, Vector2 position, Vector2 direction)
        {
            view.localPosition = new Vector3(position.x, BulletHeight, position.y);
            if (direction.sqrMagnitude > 0f)
                view.localRotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y), Vector3.up);
        }

        private static Core.Grid.GridPosition Cell(Vector2 position) => PlayerMovement.CellOf(position);
    }
}
