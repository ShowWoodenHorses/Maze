using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Maze.Editor.Dev
{
    /// <summary>
    /// Maze → Dev → Collect Used Third-Party Assets. Asset packs are downloaded into Assets/_Project/Import, which is
    /// git-ignored (hundreds of MB, mostly unused). This keeps the project self-contained:
    /// <list type="number">
    /// <item>strips physics (colliders, rigidbodies — forbidden in this project; packs add them by default) from every
    /// prefab outside Import;</item>
    /// <item>moves everything the project uses from Import/&lt;Pack&gt;/… to Art/ThirdParty/&lt;Pack&gt;/… —
    /// <see cref="AssetDatabase.MoveAsset"/> keeps GUIDs, so prefabs, levels and Addressables stay linked;</item>
    /// <item>checks that nothing outside Import references Import any more.</item>
    /// </list>
    /// Run again after taking anything new from a pack. What stays in Import is unused and can be deleted.
    /// </summary>
    internal static class ThirdPartyAssetCollector
    {
        private const string ImportRoot = "Assets/_Project/Import";
        private const string TargetRoot = "Assets/_Project/Art/ThirdParty";

        [MenuItem("Maze/Dev/Collect Used Third-Party Assets")]
        public static void Collect()
        {
            var stripped = StripPhysics();
            AssetDatabase.SaveAssets();

            var used = FindUsedImportAssets();
            var moved = 0;
            var failed = new List<string>();
            foreach (var path in used)
            {
                var target = TargetRoot + path.Substring(ImportRoot.Length);
                EnsureFolder(target.Substring(0, target.LastIndexOf('/')));
                var error = AssetDatabase.MoveAsset(path, target);
                if (string.IsNullOrEmpty(error)) moved++;
                else failed.Add($"{path}: {error}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var remaining = FindUsedImportAssets();
            foreach (var error in failed)
                Debug.LogError("[Maze] Move failed: " + error);
            foreach (var path in remaining)
                Debug.LogError("[Maze] Still referenced from Import: " + path);

            Debug.Log($"[Maze] Third-party assets: physics stripped from {stripped} prefab(s), {moved} file(s) moved to " +
                      $"{TargetRoot}, {remaining.Count} still in Import" +
                      (remaining.Count == 0 ? " — Import is unused and can be deleted." : "."));
        }

        /// <summary>Import assets referenced (directly or not) by any asset outside Import.</summary>
        private static List<string> FindUsedImportAssets()
        {
            var roots = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !IsInImport(p) && !AssetDatabase.IsValidFolder(p))
                .ToArray();
            return AssetDatabase.GetDependencies(roots, true)
                .Where(IsInImport)
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .Distinct()
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
        }

        private static int StripPhysics()
        {
            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsInImport(path)) continue;

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || (asset.GetComponentsInChildren<Collider>(true).Length == 0 &&
                                      asset.GetComponentsInChildren<Rigidbody>(true).Length == 0))
                    continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                        UnityEngine.Object.DestroyImmediate(body);
                    foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                        UnityEngine.Object.DestroyImmediate(collider);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    count++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return count;
        }

        private static bool IsInImport(string path) =>
            path == ImportRoot || path.StartsWith(ImportRoot + "/", StringComparison.Ordinal);

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = folder.Substring(0, folder.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(folder.LastIndexOf('/') + 1));
        }
    }
}
