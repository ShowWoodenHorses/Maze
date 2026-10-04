using Maze.Core.Grid;
using Maze.Core.Level;
using Maze.Core.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maze.Editor.LevelDesigner
{
    /// <summary>
    /// Editor-only 3D preview of a level in the open scene, built exactly from the saved visual design
    /// (the same resolution runtime will use). The preview is never saved with the scene.
    /// Runtime will build geometry differently (mesh combine, chunks); this is only for looking at the design.
    /// </summary>
    internal static class LevelPreviewBuilder
    {
        private const string RootName = "[Maze Level Preview]";

        /// <summary>Returns the number of cells/objects that could not be shown (no visual or missing prefab).</summary>
        public static int Build(LevelData level)
        {
            Clear();
            var root = new GameObject(RootName);
            var geometryRoot = new GameObject("Geometry").transform;
            var objectsRoot = new GameObject("Objects").transform;
            geometryRoot.SetParent(root.transform, false);
            objectsRoot.SetParent(root.transform, false);

            var missing = 0;
            var geometry = level.Geometry;
            var theme = level.VisualTheme;

            // Every cell has a floor; wall cells get the wall on top of it.
            for (var i = 0; i < geometry.CellCount; i++)
            foreach (var layer in CellLayers.All)
            {
                var cell = geometry.ToPosition(i);
                if (!CellLayers.Exists(geometry.GetCell(cell), layer))
                    continue;

                var set = theme != null ? theme.GetSet(CellLayers.Kind(layer)) : null;
                if (!Spawn(set, VisualResolver.ResolveCell(level, cell, layer), cell, geometryRoot))
                    missing++;
            }

            foreach (var entity in level.AllEntities())
            {
                if (!VisualKinds.TryGetForEntity(entity, out var kind, out _))
                    continue;

                var choice = VisualResolver.ResolveObject(level, entity);
                // Zombie orientation is gameplay state (Facing), not part of the visual assignment.
                if (entity is ZombieSpawnData zombie)
                    choice = new VisualChoice(choice.VariantId, (int)zombie.Facing);

                var set = theme != null ? theme.GetSet(kind) : null;
                var instance = Spawn(set, choice, entity.Position, objectsRoot, entity.Id);
                if (!instance)
                    missing++;
            }

            // DontSave keeps the scene clean (not marked dirty, never saved). Such objects also survive a scene
            // unload, so the preview is removed before any scene closes (see PreviewSceneGuard).
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                transform.gameObject.hideFlags = HideFlags.DontSave;

            Selection.activeGameObject = root;
            return missing;
        }

        public static void Clear()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                Clear(SceneManager.GetSceneAt(i));
        }

        private static void Clear(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName)
                    Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Removes the preview before its scene closes (scene switch, new scene, Test Runner reload).
        /// Otherwise the DontSave preview would outlive the scene as an orphan: gone from the Hierarchy,
        /// still drawn in the Scene view, and unreachable by Clear Preview.
        /// </summary>
        [InitializeOnLoad]
        private static class PreviewSceneGuard
        {
            static PreviewSceneGuard() => EditorSceneManager.sceneClosing += (scene, removingScene) => Clear(scene);
        }

        private static bool Spawn(VisualSet set, VisualChoice choice, GridPosition cell, Transform parent, string name = null)
        {
            if (set == null || choice.IsEmpty)
                return false;

            var variant = set.FindVariant(choice.VariantId);
            var prefab = variant?.Prefab?.editorAsset as GameObject;
            if (prefab == null)
                return false;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name ?? $"{choice.VariantId} {cell}";
            instance.transform.localPosition = cell.ToWorld();
            instance.transform.localRotation = Quaternion.Euler(0f, 90f * choice.Rotation, 0f);
            return true;
        }
    }
}
