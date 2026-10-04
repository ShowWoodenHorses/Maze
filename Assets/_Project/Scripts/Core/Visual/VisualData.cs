using System;
using System.Collections.Generic;
using Maze.Core.Grid;
using UnityEngine;

namespace Maze.Core.Visual
{
    /// <summary>A concrete visual decision: which variant and how it is rotated.</summary>
    [Serializable]
    public struct VisualChoice : IEquatable<VisualChoice>
    {
        [SerializeField] private string _variantId;
        [SerializeField] private byte _rotation;

        public VisualChoice(string variantId, int rotation = 0)
        {
            _variantId = variantId;
            _rotation = (byte)(((rotation % 4) + 4) % 4);
        }

        public static readonly VisualChoice None = default;

        public string VariantId => _variantId;

        /// <summary>Clockwise quarter turns around Y (seen from above): 0 = authored orientation facing North.</summary>
        public int Rotation => _rotation;

        public bool IsEmpty => string.IsNullOrEmpty(_variantId);

        public bool Equals(VisualChoice other) => _variantId == other._variantId && _rotation == other._rotation;
        public override bool Equals(object obj) => obj is VisualChoice other && Equals(other);
        public override int GetHashCode() => ((_variantId?.Length ?? 0) * 397) ^ _rotation;
        public override string ToString() => IsEmpty ? "<none>" : $"{_variantId} r{_rotation}";
    }

    [Serializable]
    public sealed class CellVisualOverride
    {
        [SerializeField] private GridPosition _position;
        [SerializeField] private CellLayer _layer;
        [SerializeField] private VisualChoice _choice;

        public CellVisualOverride(GridPosition position, CellLayer layer, VisualChoice choice)
        {
            _position = position;
            _layer = layer;
            _choice = choice;
        }

        public GridPosition Position => _position;
        public CellLayer Layer => _layer;
        public VisualChoice Choice { get => _choice; internal set => _choice = value; }
    }

    [Serializable]
    public sealed class ObjectVisualAssignment
    {
        [SerializeField] private string _entityId;
        [SerializeField] private VisualChoice _choice;

        public ObjectVisualAssignment(string entityId, VisualChoice choice)
        {
            _entityId = entityId;
            _choice = choice;
        }

        public string EntityId { get => _entityId; internal set => _entityId = value; }
        public VisualChoice Choice { get => _choice; internal set => _choice = value; }
    }

    /// <summary>
    /// Saved visual design of a level (ТЗ §25). Geometry assignments are stored per cell and layer (row-major,
    /// same indexing as LevelGeometry): every cell has a Floor-layer assignment, wall cells also a Wall-layer one.
    /// Doors and exits themselves are object assignments. Overrides are the designer's manual choices
    /// (per cell and layer) and win over assignments.
    /// </summary>
    [Serializable]
    public sealed class VisualData
    {
        [SerializeField] private VisualChoice[] _floorAssignments = Array.Empty<VisualChoice>();
        [SerializeField] private VisualChoice[] _wallAssignments = Array.Empty<VisualChoice>();
        [SerializeField] private List<CellVisualOverride> _cellOverrides = new List<CellVisualOverride>();
        [SerializeField] private List<ObjectVisualAssignment> _objectAssignments = new List<ObjectVisualAssignment>();
        [SerializeField] private List<ObjectVisualAssignment> _objectOverrides = new List<ObjectVisualAssignment>();

        public IReadOnlyList<CellVisualOverride> CellOverrides => _cellOverrides;
        public IReadOnlyList<ObjectVisualAssignment> ObjectAssignments => _objectAssignments;
        public IReadOnlyList<ObjectVisualAssignment> ObjectOverrides => _objectOverrides;

        /// <summary>False if assignments were made for a different level size (or never made).</summary>
        public bool HasCellAssignments(int cellCount) =>
            _floorAssignments.Length == cellCount && _wallAssignments.Length == cellCount;

        public VisualChoice GetCellAssignment(CellLayer layer, int cellIndex)
        {
            var assignments = Layer(layer);
            return cellIndex >= 0 && cellIndex < assignments.Length ? assignments[cellIndex] : VisualChoice.None;
        }

        public bool TryGetCellOverride(GridPosition position, CellLayer layer, out VisualChoice choice)
        {
            foreach (var entry in _cellOverrides)
                if (entry.Position == position && entry.Layer == layer)
                {
                    choice = entry.Choice;
                    return true;
                }

            choice = VisualChoice.None;
            return false;
        }

        public VisualChoice GetObjectAssignment(string entityId) => Find(_objectAssignments, entityId)?.Choice ?? VisualChoice.None;

        public bool TryGetObjectOverride(string entityId, out VisualChoice choice)
        {
            var entry = Find(_objectOverrides, entityId);
            choice = entry?.Choice ?? VisualChoice.None;
            return entry != null;
        }

        internal void ResetCellAssignments(int cellCount)
        {
            _floorAssignments = new VisualChoice[cellCount];
            _wallAssignments = new VisualChoice[cellCount];
        }

        internal void SetCellAssignment(CellLayer layer, int cellIndex, VisualChoice choice) => Layer(layer)[cellIndex] = choice;

        internal void SetCellOverride(GridPosition position, CellLayer layer, VisualChoice choice)
        {
            foreach (var entry in _cellOverrides)
                if (entry.Position == position && entry.Layer == layer)
                {
                    entry.Choice = choice;
                    return;
                }

            _cellOverrides.Add(new CellVisualOverride(position, layer, choice));
        }

        internal bool ClearCellOverride(GridPosition position, CellLayer layer) =>
            _cellOverrides.RemoveAll(o => o.Position == position && o.Layer == layer) > 0;

        private VisualChoice[] Layer(CellLayer layer) => layer == CellLayer.Floor ? _floorAssignments : _wallAssignments;

        internal void SetObjectAssignment(string entityId, VisualChoice choice) => Set(_objectAssignments, entityId, choice);

        internal void SetObjectOverride(string entityId, VisualChoice choice) => Set(_objectOverrides, entityId, choice);

        internal bool ClearObjectOverride(string entityId) => _objectOverrides.RemoveAll(o => o.EntityId == entityId) > 0;

        internal void RemoveObject(string entityId)
        {
            _objectAssignments.RemoveAll(o => o.EntityId == entityId);
            _objectOverrides.RemoveAll(o => o.EntityId == entityId);
        }

        /// <summary>Keeps the visual design of an object when its id changes.</summary>
        internal void RenameObject(string oldId, string newId)
        {
            foreach (var entry in _objectAssignments)
                if (entry.EntityId == oldId)
                    entry.EntityId = newId;

            foreach (var entry in _objectOverrides)
                if (entry.EntityId == oldId)
                    entry.EntityId = newId;
        }

        /// <summary>Drops assignments and overrides of entities that no longer exist.</summary>
        internal void RetainObjects(ICollection<string> existingEntityIds)
        {
            _objectAssignments.RemoveAll(o => !existingEntityIds.Contains(o.EntityId));
            _objectOverrides.RemoveAll(o => !existingEntityIds.Contains(o.EntityId));
        }

        internal void ClearOverrides()
        {
            _cellOverrides.Clear();
            _objectOverrides.Clear();
        }

        internal void Clear()
        {
            _floorAssignments = Array.Empty<VisualChoice>();
            _wallAssignments = Array.Empty<VisualChoice>();
            _objectAssignments.Clear();
            ClearOverrides();
        }

        private static ObjectVisualAssignment Find(List<ObjectVisualAssignment> list, string entityId)
        {
            foreach (var entry in list)
                if (entry.EntityId == entityId)
                    return entry;

            return null;
        }

        private static void Set(List<ObjectVisualAssignment> list, string entityId, VisualChoice choice)
        {
            var entry = Find(list, entityId);
            if (entry != null)
                entry.Choice = choice;
            else
                list.Add(new ObjectVisualAssignment(entityId, choice));
        }
    }
}
