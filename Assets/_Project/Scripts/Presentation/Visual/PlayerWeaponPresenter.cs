using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Assets;
using Maze.Core.Common;
using Maze.Core.Definitions;
using Maze.Core.Level;
using Maze.Core.Visual;
using Maze.Gameplay.Level;
using Maze.Gameplay.Weapons;
using UnityEngine;

namespace Maze.Presentation.Visual
{
    /// <summary>
    /// The weapon in the player's hands (ТЗ §39: WeaponRuntime → WeaponView). On load, the held prefabs of the
    /// weapons lying in this level are loaded (<see cref="WeaponVisualCatalog"/>, level asset owner) — nothing is
    /// loaded during play. The active weapon is shown, the other slot hidden. A melee weapon sits rigidly in the
    /// right hand (<see cref="CharacterWeaponRig.MeleeSocket"/>); a gun is posed every frame after animation: grip in
    /// the right palm, barrel toward the left one. Must be registered after <see cref="PlayerViewPresenter"/>
    /// (same load stage and late tick: the player is placed first).
    /// </summary>
    public sealed class PlayerWeaponPresenter : ILevelLoadStep, ILevelLateTickable, IDisposable
    {
        private readonly LevelData _level;
        private readonly PlayerViewPresenter _playerView;
        private readonly WeaponSystem _weapons;
        private readonly WeaponVisualCatalog _catalog;
        private readonly IAssetOwner _assets;
        private readonly Dictionary<WeaponDefinition, GameObject> _prefabs = new Dictionary<WeaponDefinition, GameObject>();
        private readonly Dictionary<WeaponDefinition, GameObject> _instances = new Dictionary<WeaponDefinition, GameObject>();

        private CharacterWeaponRig _rig;
        private GameObject _shown;
        private bool _shownIsGun;
        private bool _subscribed;

        public PlayerWeaponPresenter(LevelData level, PlayerViewPresenter playerView, WeaponSystem weapons,
            WeaponVisualCatalog catalog, IAssetOwner assets)
        {
            _level = level;
            _playerView = playerView;
            _weapons = weapons;
            _catalog = catalog;
            _assets = assets;
        }

        public LevelLoadStage Stage => LevelLoadStage.SpawnPlayer;

        /// <summary>The weapon model now in the player's hands, or null.</summary>
        public GameObject Shown => _shown;

        public async UniTask ExecuteAsync(CancellationToken cancellation)
        {
            _rig = _playerView.View != null ? _playerView.View.GetComponentInChildren<CharacterWeaponRig>() : null;
            if (_rig == null || !_rig.IsValid)
            {
                GameLog.Warning(LogChannel.Visual, "The player prefab has no weapon rig: weapons are not shown in hands.");
                return;
            }

            foreach (var pickup in _level.Weapons)
            {
                var definition = pickup.Definition;
                if (definition == null || _prefabs.ContainsKey(definition)) continue;

                var visual = _catalog != null ? _catalog.Find(definition) : null;
                if (visual?.HeldPrefab == null || !visual.HeldPrefab.RuntimeKeyIsValid())
                {
                    GameLog.Warning(LogChannel.Visual, $"Weapon '{definition.Id}' has no held look.");
                    _prefabs[definition] = null;
                    continue;
                }

                _prefabs[definition] = await _assets.LoadAsync<GameObject>(visual.HeldPrefab, cancellation);
                cancellation.ThrowIfCancellationRequested();
            }

            _weapons.Changed += Refresh;
            _subscribed = true;
            Refresh();
        }

        public void LateTick(float deltaTime)
        {
            if (_shown != null && _shownIsGun)
                _shown.transform.rotation = _rig.TwoHandedRotation();
        }

        public void Dispose()
        {
            if (_subscribed)
            {
                _weapons.Changed -= Refresh;
                _subscribed = false;
            }

            // The models are children of the player view, destroyed with it.
            _instances.Clear();
            _prefabs.Clear();
            _shown = null;
        }

        private void Refresh()
        {
            var weapon = _weapons.Active;
            var next = weapon != null ? GetOrCreate(weapon.Definition) : null;
            if (next == _shown) return;

            if (_shown != null) _shown.SetActive(false);
            _shown = next;
            _shownIsGun = weapon != null && weapon.Slot == WeaponSlot.Ranged;
            if (_shown == null) return;

            _shown.SetActive(true);
            LateTick(0f);
        }

        private GameObject GetOrCreate(WeaponDefinition definition)
        {
            if (definition == null) return null;
            if (_instances.TryGetValue(definition, out var instance)) return instance;

            _prefabs.TryGetValue(definition, out var prefab);
            if (prefab == null)
            {
                _instances[definition] = null;
                return null;
            }

            var parent = definition.Slot == WeaponSlot.Ranged ? _rig.PalmRight : _rig.MeleeSocket;
            instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = "Weapon " + definition.Id;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            _instances[definition] = instance;
            return instance;
        }
    }
}
