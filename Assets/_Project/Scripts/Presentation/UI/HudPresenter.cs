using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Core.Definitions;
using Maze.Core.Grid;
using Maze.Core.Level;
using Maze.Core.Visual;
using Maze.Gameplay.Combat;
using Maze.Gameplay.Level;
using Maze.Gameplay.Map;
using Maze.Gameplay.Pickups;
using Maze.Gameplay.Player;
using Maze.Gameplay.Weapons;
using Maze.Gameplay.Zombies;
using Maze.Presentation.Visual;
using UnityEngine;

namespace Maze.Presentation.UI
{
    /// <summary>
    /// Shows the level's player state on the HUD (load stage InitializeUI): HP, map fragments, zombies killed, keys in
    /// their doors' colours, weapon slots (the on-screen cluster or the desktop slots), the Use target, short messages
    /// about interactions and a red pulse at the screen edges when the player is hurt. The HUD lives in the Bootstrap
    /// scene; this presenter lives with the level. Counters change on gameplay events; the weapon slots and the Use
    /// target are read every frame (reload, where the player stands), the views repaint only on a change.
    /// </summary>
    public sealed class HudPresenter : ILevelLoadStep, ILevelLateTickable, IDisposable
    {
        private readonly UIRoot _ui;
        private readonly LevelData _level;
        private readonly PlayerHealth _health;
        private readonly PlayerInventory _inventory;
        private readonly WeaponSystem _weapons;
        private readonly PlayerInteraction _interaction;
        private readonly MapSystem _map;
        private readonly PlayerSystem _player;
        private readonly PickupSystem _pickups;
        private readonly ZombieSystem _zombies;
        private readonly WeaponVisualCatalog _visuals;
        private readonly List<Color> _keyColors = new List<Color>();
        private int _lastHealth;
        private bool _bound;

        public HudPresenter(UIRoot ui, LevelData level, PlayerHealth health, PlayerInventory inventory,
            WeaponSystem weapons, PlayerInteraction interaction, MapSystem map, PlayerSystem player,
            PickupSystem pickups, ZombieSystem zombies, WeaponVisualCatalog visuals)
        {
            _ui = ui;
            _level = level;
            _health = health;
            _inventory = inventory;
            _weapons = weapons;
            _interaction = interaction;
            _map = map;
            _player = player;
            _pickups = pickups;
            _zombies = zombies;
            _visuals = visuals;
        }

        public LevelLoadStage Stage => LevelLoadStage.InitializeUI;

        public UniTask ExecuteAsync(CancellationToken cancellation)
        {
            if (!_bound)
            {
                _health.Changed += OnHealthChanged;
                _inventory.KeysChanged += RefreshKeys;
                _interaction.Interacted += OnInteracted;
                _map.FragmentCollected += OnFragmentCollected;
                _player.CellChanged += OnPlayerCellChanged;
                _zombies.Spawned += OnZombie;
                _zombies.Died += OnZombie;
                _bound = true;
            }

            _ui.Hud.ClearLevelInfo();
            _lastHealth = _health.Current;
            _ui.Hud.SetHealth(_health.Current, _health.Max);
            RefreshCounters();
            RefreshKeys();
            LateTick(0f); // No leftovers of the previous level on the slots.
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            if (!_bound) return;
            _health.Changed -= OnHealthChanged;
            _inventory.KeysChanged -= RefreshKeys;
            _interaction.Interacted -= OnInteracted;
            _map.FragmentCollected -= OnFragmentCollected;
            _player.CellChanged -= OnPlayerCellChanged;
            _zombies.Spawned -= OnZombie;
            _zombies.Died -= OnZombie;
            _bound = false;
            if (_ui != null && _ui.Hud != null)
                _ui.Hud.ClearLevelInfo();
        }

        public void LateTick(float deltaTime)
        {
            var melee = SlotState(WeaponSlot.Melee);
            var ranged = SlotState(WeaponSlot.Ranged);
            _ui.Hud.SetWeapons(melee, ranged);

            var touch = _ui.TouchControls;
            if (touch == null) return;
            touch.SetWeapons(melee, ranged);
            touch.SetUse(_interaction.CurrentTarget() switch
            {
                InteractionTarget.Weapon => UseTarget.PickUp,
                InteractionTarget.Door => UseTarget.Door,
                _ => UseTarget.None,
            });
        }

        private WeaponSlotState SlotState(WeaponSlot slot)
        {
            var weapon = _weapons.Get(slot);
            if (weapon == null) return WeaponSlotState.Empty;
            return new WeaponSlotState
            {
                Equipped = true,
                Active = _weapons.ActiveSlot == slot,
                Icon = _visuals != null ? _visuals.Find(weapon.Definition)?.Icon : null,
                Magazine = slot == WeaponSlot.Ranged ? weapon.Definition.MagazineSize : 0,
                Ammo = weapon.Ammo,
                Reloading = weapon.IsReloading,
                Reload = weapon.ReloadProgress,
            };
        }

        private void OnHealthChanged(int current, int max)
        {
            if (current < _lastHealth)
                _ui.Hud.PulseDamage();
            _lastHealth = current;
            _ui.Hud.SetHealth(current, max);
        }

        private void OnZombie(ZombieRuntime zombie) => RefreshCounters();

        private void RefreshCounters()
        {
            _ui.Hud.SetFragments(_map.CollectedCount, _map.TotalCount);
            _ui.Hud.SetKills(_zombies.KilledCount, _zombies.TotalCount);
        }

        private void RefreshKeys()
        {
            _keyColors.Clear();
            foreach (var key in _inventory.Keys)
            {
                var tag = VisualColorTags.TagOf(_level, VisualKind.Key, key);
                _keyColors.Add(VisualColorTags.TryGetColor(tag, out var color) ? color : Color.white);
            }

            _ui.Hud.SetKeys(_keyColors);
        }

        /// <summary>Weapons are picked up by Interact, not on entering: tell the player how.</summary>
        private void OnPlayerCellChanged(GridPosition from, GridPosition to)
        {
            foreach (var pickup in _pickups.At(to))
                if (pickup.Kind == PickupKind.Weapon)
                {
                    _ui.Hud.ShowMessage($"{WeaponName(pickup.Weapon)}: press Use to pick up");
                    return;
                }
        }

        private void OnFragmentCollected(MapFragmentData fragment)
        {
            RefreshCounters();
            _ui.Hud.ShowMessage($"Map fragment {_map.CollectedCount}/{_map.TotalCount} found");
        }

        private void OnInteracted(InteractionResult result, DoorData door)
        {
            switch (result)
            {
                case InteractionResult.DoorLocked:
                    var color = VisualColorTags.TagOf(_level, VisualKind.Door, door);
                    _ui.Hud.ShowMessage(color != null ? $"Locked: needs the {color} key" : "Locked: needs a key");
                    break;
                case InteractionResult.DoorUnlocked:
                    _ui.Hud.ShowMessage("Door unlocked");
                    break;
                case InteractionResult.DoorBlocked:
                    _ui.Hud.ShowMessage("Something is in the doorway");
                    break;
                case InteractionResult.WeaponTaken:
                    if (_weapons.Active != null)
                        _ui.Hud.ShowMessage($"Picked up {WeaponName(_weapons.Active)}");
                    break;
            }
        }

        private static string WeaponName(WeaponRuntime weapon)
        {
            var id = string.IsNullOrEmpty(weapon.Definition.Id) ? weapon.Definition.name : weapon.Definition.Id;
            return id.Replace('_', ' ');
        }
    }
}
