using System;
using System.Linq;
using Maze.Core.Authoring;
using Maze.Core.Definitions;
using Maze.Core.Grid;
using Maze.Core.Level;
using Maze.Core.Visual;
using UnityEditor;
using UnityEngine;

namespace Maze.Editor.LevelDesigner
{
    /// <summary>
    /// Inspector for the selected cell and object: visual overrides (ТЗ §27, §94), ids, key/door links,
    /// definitions, patrols and map fragment regions.
    /// </summary>
    internal sealed class SelectionInspector
    {
        private static readonly string[] RotationNames = { "0°", "90°", "180°", "270°" };
        private static readonly int[] RotationValues = { 0, 1, 2, 3 };

        private readonly LevelDesignerState _state;
        private readonly EditToolController _tools;
        private readonly Action<string> _notify;

        public SelectionInspector(LevelDesignerState state, EditToolController tools, Action<string> notify)
        {
            _state = state;
            _tools = tools;
            _notify = notify;
        }

        private LevelData Level => _state.Level;

        public void OnGUI()
        {
            if (!_state.SelectedCell.HasValue || !Level.Geometry.IsInside(_state.SelectedCell.Value))
                return;

            var cell = _state.SelectedCell.Value;
            EditorGUILayout.LabelField($"Cell {cell}  ·  {Level.Geometry.GetCell(cell)}", EditorStyles.boldLabel);
            DrawCellVisual(cell);

            var entities = Level.AllEntities().Where(e => e.Position == cell).ToList();
            if (entities.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                foreach (var entity in entities)
                    if (GUILayout.Toggle(entity.Id == _state.SelectedEntityId, entity.Id, EditorStyles.miniButton))
                        _state.SelectedEntityId = entity.Id;
                EditorGUILayout.EndHorizontal();
            }

            var selected = _state.SelectedEntity;
            if (selected != null)
                DrawEntity(selected);
        }

        // ------------------------------------------------------------ Cell

        private void DrawCellVisual(GridPosition cell)
        {
            var theme = Level.VisualTheme;
            if (theme == null)
                return;

            // Floor layer exists in every cell (also under walls); wall cells also have a wall layer.
            foreach (var layer in CellLayers.All)
            {
                if (!CellLayers.Exists(Level.Geometry.GetCell(cell), layer))
                    continue;

                var set = theme.GetSet(CellLayers.Kind(layer));
                var current = VisualResolver.ResolveCell(Level, cell, layer, out var source);
                EditorGUILayout.LabelField(layer + " visual", current.IsEmpty ? "no visual" : $"{current.VariantId} r{current.Rotation} [{source}]");

                VisualOverrideField(layer + " override", set, current, source == VisualSource.Override,
                    choice => Apply("Set Visual Override", () => LevelEditing.SetCellOverride(Level, cell, layer, choice)),
                    () => Apply("Clear Visual Override", () => LevelEditing.ClearCellOverride(Level, cell, layer)));
            }
        }

        // ------------------------------------------------------------ Object

        private void DrawEntity(LevelEntityData entity)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(entity.GetType().Name.Replace("Data", string.Empty), EditorStyles.boldLabel);

            var newId = EditorGUILayout.DelayedTextField("Id", entity.Id);
            if (newId != entity.Id)
            {
                if (LevelEditing.IsFreeId(Level, newId))
                    Apply("Rename " + entity.Id, () => LevelEditing.Rename(Level, entity, newId));
                else
                    _notify($"Id '{newId}' is empty or already used.");
            }

            switch (entity)
            {
                case DoorData door: DrawDoor(door); break;
                case KeyData key: DrawKey(key); break;
                case ZombieSpawnData zombie: DrawZombie(zombie); break;
                case WeaponPickupData weapon: DrawWeapon(weapon); break;
                case MapFragmentData fragment: DrawFragment(fragment); break;
            }

            if (VisualKinds.TryGetForEntity(entity, out var kind, out _) && Level.VisualTheme != null)
            {
                var current = VisualResolver.ResolveObject(Level, entity, out var source);
                EditorGUILayout.LabelField("Visual", current.IsEmpty ? "no visual" : $"{current.VariantId} r{current.Rotation} [{source}]");
                VisualOverrideField("Override", Level.VisualTheme.GetSet(kind), current, source == VisualSource.Override,
                    choice => Apply("Set Visual Override", () => LevelEditing.SetObjectOverride(Level, entity, choice)),
                    () => Apply("Clear Visual Override", () => LevelEditing.ClearObjectOverride(Level, entity)));
            }

            EditorGUILayout.Space(2f);
            if (GUILayout.Button("Delete " + entity.Id))
            {
                Apply("Delete " + entity.Id, () => LevelEditing.Remove(Level, entity));
                _state.SelectedEntityId = null;
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDoor(DoorData door)
        {
            var keys = Level.Keys.ToList();
            var options = new[] { "(no key)" }.Concat(keys.Select(k =>
            {
                var owner = Level.Doors.FirstOrDefault(d => d != door && d.KeyId == k.Id);
                return owner == null ? k.Id : $"{k.Id}  (now opens {owner.Id})";
            })).ToArray();

            var index = door.RequiresKey ? keys.FindIndex(k => k.Id == door.KeyId) + 1 : 0;
            var selected = EditorGUILayout.Popup("Key", index, options);
            if (selected != index)
                Apply("Link Key", () => LevelEditing.LinkKey(Level, door, selected == 0 ? null : keys[selected - 1]));

            if (door.RequiresKey && keys.All(k => k.Id != door.KeyId))
                EditorGUILayout.HelpBox($"Key '{door.KeyId}' does not exist.", MessageType.Error);

            if (GUILayout.Button("Place a new key for this door"))
            {
                _tools.Tool = EditTool.Key;
                _tools.PendingKeyDoorId = door.Id;
                _notify("Click a floor cell to place the key.");
            }

            var open = EditorGUILayout.Toggle("Initially Open", door.IsInitiallyOpen);
            if (open != door.IsInitiallyOpen)
                Apply("Toggle Door Open", () => LevelEditing.SetDoorInitiallyOpen(door, open));
        }

        private void DrawKey(KeyData key)
        {
            var door = Level.Doors.FirstOrDefault(d => d.KeyId == key.Id);
            EditorGUILayout.LabelField("Opens", door != null ? door.Id : "no door (link it from a door)");
        }

        private void DrawZombie(ZombieSpawnData zombie)
        {
            EditorGUI.BeginChangeCheck();
            var definition = (ZombieDefinition)EditorGUILayout.ObjectField("Definition", zombie.Definition, typeof(ZombieDefinition), false);
            var facing = (Direction)EditorGUILayout.EnumPopup("Facing", zombie.Facing);
            if (EditorGUI.EndChangeCheck())
                Apply("Change Zombie", () => LevelEditing.SetZombie(Level, zombie, definition, facing));

            var patrol = Level.Patrols.FirstOrDefault(p => zombie.HasPatrol && p.Id == zombie.PatrolId);
            EditorGUILayout.LabelField("Patrol", patrol == null ? "none (idles at spawn)" : $"{patrol.Id}: {patrol.Points.Count} point(s)");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Edit Patrol"))
            {
                _tools.Tool = EditTool.Patrol;
                _notify("Click cells to add patrol points. Right-click removes the last one.");
            }

            using (new EditorGUI.DisabledScope(patrol == null))
            {
                if (GUILayout.Button("Clear Patrol"))
                    Apply("Clear Patrol", () => LevelEditing.ClearPatrol(Level, zombie));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawWeapon(WeaponPickupData weapon)
        {
            var definition = (WeaponDefinition)EditorGUILayout.ObjectField("Definition", weapon.Definition, typeof(WeaponDefinition), false);
            if (definition != weapon.Definition)
                Apply("Change Weapon", () => LevelEditing.SetWeaponDefinition(Level, weapon, definition));
        }

        private void DrawFragment(MapFragmentData fragment)
        {
            var r = fragment.Region;
            EditorGUI.BeginChangeCheck();
            var rect = EditorGUILayout.RectIntField("Region", new RectInt(r.X, r.Y, r.Width, r.Height));
            if (EditorGUI.EndChangeCheck())
                Apply("Change Region", () => LevelEditing.SetFragmentRegion(fragment, new GridRect(rect.x, rect.y, rect.width, rect.height)));

            if (GUILayout.Button("Draw Region"))
            {
                _tools.Tool = EditTool.FragmentRegion;
                _notify("Drag a rectangle on the grid.");
            }
        }

        // ------------------------------------------------------------ Helpers

        /// <summary>"(automatic)" = no override; picking a variant creates a manual override that has priority.</summary>
        private static void VisualOverrideField(string label, VisualSet set, VisualChoice current, bool hasOverride,
            Action<VisualChoice> setOverride, Action clearOverride)
        {
            if (set == null)
                return;

            var ids = set.Variants.Select(v => v.Id).ToList();
            var options = new[] { "(automatic)" }.Concat(ids.Select(id => id ?? "<no id>")).ToArray();
            var index = hasOverride ? ids.IndexOf(current.VariantId) + 1 : 0;

            var selected = EditorGUILayout.Popup(label, Mathf.Max(0, index), options);
            if (selected != Mathf.Max(0, index))
            {
                if (selected == 0)
                    clearOverride();
                else
                    setOverride(new VisualChoice(ids[selected - 1], current.Rotation));
                return;
            }

            if (!hasOverride)
                return;

            var rotation = EditorGUILayout.IntPopup("Rotation", current.Rotation, RotationNames, RotationValues);
            if (rotation != current.Rotation)
                setOverride(new VisualChoice(current.VariantId, rotation));
        }

        private void Apply(string undoName, Action change)
        {
            LevelEditorCommands.Modify(Level, undoName, change);
            _state.Revalidate();
        }
    }
}
