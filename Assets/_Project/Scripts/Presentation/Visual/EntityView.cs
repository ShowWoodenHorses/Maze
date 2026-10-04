using System;
using System.Collections.Generic;
using Maze.Core.Grid;
using Maze.Core.Level;
using Maze.Core.Visual;
using UnityEngine;

namespace Maze.Presentation.Visual
{
    /// <summary>
    /// Visual of one level object (door, exit, pickup, zombie). Display only: gameplay systems own the state and
    /// tell the view what to show. Hidden views keep existing (ТЗ §48, §57).
    /// </summary>
    public sealed class EntityView
    {
        public EntityView(string entityId, VisualKind kind, GridPosition cell, GameObject gameObject)
        {
            EntityId = entityId;
            Kind = kind;
            Cell = cell;
            GameObject = gameObject;
        }

        public string EntityId { get; }
        public VisualKind Kind { get; }

        /// <summary>Cell used by visibility. Moving entities update it.</summary>
        public GridPosition Cell { get; set; }

        public GameObject GameObject { get; }
        public bool IsVisible => GameObject != null && GameObject.activeSelf;

        public void SetVisible(bool visible)
        {
            if (GameObject != null && GameObject.activeSelf != visible)
                GameObject.SetActive(visible);
        }
    }

    /// <summary>All entity views of the active level, by entity id.</summary>
    public sealed class EntityViewRegistry : IDisposable
    {
        private readonly Dictionary<string, EntityView> _views = new Dictionary<string, EntityView>();
        private readonly List<EntityView> _ordered = new List<EntityView>();

        public int Count => _ordered.Count;
        public IReadOnlyList<EntityView> All => _ordered;

        public void Add(EntityView view)
        {
            if (_views.ContainsKey(view.EntityId))
                throw new ArgumentException($"Entity view '{view.EntityId}' already exists.", nameof(view));

            _views.Add(view.EntityId, view);
            _ordered.Add(view);
        }

        public bool TryGet(string entityId, out EntityView view) => _views.TryGetValue(entityId, out view);

        /// <summary>Removes and destroys the view (e.g. a collected pickup).</summary>
        public bool Remove(string entityId)
        {
            if (!_views.TryGetValue(entityId, out var view))
                return false;

            _views.Remove(entityId);
            _ordered.Remove(view);
            UnityObjects.Destroy(view.GameObject);
            return true;
        }

        public void Dispose()
        {
            foreach (var view in _ordered)
                UnityObjects.Destroy(view.GameObject);
            _ordered.Clear();
            _views.Clear();
        }
    }

    /// <summary>Instantiates the exact saved visual of an object (ТЗ §43). Never chooses a variant itself.</summary>
    public sealed class EntityViewFactory
    {
        private readonly IVisualPrefabs _prefabs;

        public EntityViewFactory(IVisualPrefabs prefabs)
        {
            _prefabs = prefabs;
        }

        /// <summary>Null when the object has no visual or its prefab is missing.</summary>
        public EntityView Create(LevelData level, LevelEntityData entity, Transform parent)
        {
            if (!VisualKinds.TryGetForEntity(entity, out var kind, out _))
                return null;

            return Create(entity.Id, kind, VisualResolver.ResolveObject(level, entity), entity.Position, parent);
        }

        public EntityView Create(string entityId, VisualKind kind, VisualChoice choice, GridPosition cell, Transform parent)
        {
            var prefab = choice.IsEmpty ? null : _prefabs.Get(kind, choice.VariantId);
            if (prefab == null)
                return null;

            var instance = UnityEngine.Object.Instantiate(prefab, parent);
            instance.name = entityId;
            instance.transform.localPosition = cell.ToWorld();
            instance.transform.localRotation = Quaternion.Euler(0f, 90f * choice.Rotation, 0f);
            return new EntityView(entityId, kind, cell, instance);
        }
    }
}
