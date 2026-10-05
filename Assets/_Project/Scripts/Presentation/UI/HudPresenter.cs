using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Core.Definitions;
using Maze.Core.Level;
using Maze.Core.Visual;
using Maze.Gameplay.Level;
using Maze.Gameplay.Player;
using Maze.Gameplay.Weapons;

namespace Maze.Presentation.UI
{
    /// <summary>
    /// Shows the level's player state on the HUD (load stage InitializeUI): HP, weapon slots, keys, and short
    /// messages about interactions. The HUD lives in the Bootstrap scene; this presenter lives with the level.
    /// </summary>
    public sealed class HudPresenter : ILevelLoadStep, IDisposable
    {
        private readonly UIRoot _ui;
        private readonly LevelData _level;
        private readonly PlayerHealth _health;
        private readonly PlayerInventory _inventory;
        private readonly WeaponSystem _weapons;
        private readonly PlayerInteraction _interaction;
        private readonly StringBuilder _text = new StringBuilder();
        private bool _bound;

        public HudPresenter(UIRoot ui, LevelData level, PlayerHealth health, PlayerInventory inventory,
            WeaponSystem weapons, PlayerInteraction interaction)
        {
            _ui = ui;
            _level = level;
            _health = health;
            _inventory = inventory;
            _weapons = weapons;
            _interaction = interaction;
        }

        public LevelLoadStage Stage => LevelLoadStage.InitializeUI;

        public UniTask ExecuteAsync(CancellationToken cancellation)
        {
            if (!_bound)
            {
                _health.Changed += OnHealthChanged;
                _inventory.KeysChanged += Refresh;
                _weapons.Changed += Refresh;
                _interaction.Interacted += OnInteracted;
                _bound = true;
            }

            _ui.Hud.ClearLevelInfo();
            Refresh();
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            if (!_bound) return;
            _health.Changed -= OnHealthChanged;
            _inventory.KeysChanged -= Refresh;
            _weapons.Changed -= Refresh;
            _interaction.Interacted -= OnInteracted;
            _bound = false;
            if (_ui != null && _ui.Hud != null)
                _ui.Hud.ClearLevelInfo();
        }

        private void OnHealthChanged(int current, int max) => Refresh();

        private void Refresh()
        {
            _text.Clear();
            _text.Append("HP ").Append(_health.Current).Append('/').Append(_health.Max).Append('\n');
            AppendSlot("Melee", WeaponSlot.Melee);
            _text.Append("   ");
            AppendSlot("Ranged", WeaponSlot.Ranged);
            _text.Append('\n').Append("Keys: ");
            if (_inventory.Keys.Count == 0)
                _text.Append('—');
            for (var i = 0; i < _inventory.Keys.Count; i++)
            {
                if (i > 0) _text.Append(", ");
                _text.Append(ColorOf(VisualKind.Key, _inventory.Keys[i]) ?? _inventory.Keys[i].Id);
            }

            _ui.Hud.SetStatus(_text.ToString());
        }

        private void AppendSlot(string label, WeaponSlot slot)
        {
            var weapon = _weapons.Get(slot);
            var active = _weapons.ActiveSlot == slot;
            _text.Append(label).Append(": ");
            if (weapon == null)
            {
                _text.Append('—');
                return;
            }

            if (active) _text.Append('[');
            _text.Append(WeaponName(weapon));
            if (slot == WeaponSlot.Ranged)
                _text.Append(' ').Append(weapon.Ammo).Append('/').Append(weapon.Definition.MagazineSize);
            if (active) _text.Append(']');
        }

        private void OnInteracted(InteractionResult result, DoorData door)
        {
            switch (result)
            {
                case InteractionResult.DoorLocked:
                    var color = ColorOf(VisualKind.Door, door);
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

        private static string WeaponName(WeaponRuntime weapon) =>
            string.IsNullOrEmpty(weapon.Definition.Id) ? weapon.Definition.name : weapon.Definition.Id;

        /// <summary>Colour tag of the object's saved visual (key/door pair colour), or null.</summary>
        private string ColorOf(VisualKind kind, LevelEntityData entity)
        {
            var set = _level.VisualTheme != null ? _level.VisualTheme.GetSet(kind) : null;
            var variant = set?.FindVariant(VisualResolver.ResolveObject(_level, entity).VariantId);
            return variant != null && variant.HasColor ? variant.ColorTag : null;
        }
    }
}
