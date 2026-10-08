using System;
using System.Collections.Generic;
using Maze.Core.Definitions;
using Maze.Core.Level;
using Maze.Core.Visual;
using Maze.Presentation.Visual;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Maze.Editor.Dev
{
    /// <summary>
    /// Maze → Dev → Build Weapons: the weapons of the game from the models in Art/Weapons (Synty: pivot at the grip,
    /// pointing along +Z).
    /// <list type="number">
    /// <item><see cref="WeaponDefinition"/> per model in Data/Weapons (<see cref="Weapons"/>). A new definition gets
    /// draft values; an existing one keeps them (tune in the asset).</item>
    /// <item>Held prefab = the model + <see cref="WeaponModel"/> with a Muzzle (guns) or Tip (melee) mark at the far
    /// end of the mesh; <see cref="WeaponVisualCatalog"/> (Data/Weapons/WeaponVisuals, "Weapons/Visual") maps
    /// definitions to them.</item>
    /// <item>Pickup prefab (Art/Weapons/Pickups) = the model lying on its side, so its silhouette is seen from above,
    /// diagonal in the cell, on the floor; the placeholder theme's Weapon set gets one variant per definition, and
    /// saved levels whose weapon visuals no longer fit are reassigned.</item>
    /// <item><see cref="CharacterWeaponRig"/> on the player prefab: palm centres and the melee socket, from the hand
    /// and finger bones (independent of any pose).</item>
    /// <item>Gun's <see cref="WeaponModel.GripLeft"/> (Grip_L): where the left palm is on the gun in the shooting pose
    /// (<see cref="GripPoseClip"/>), posed as in game — the left hand IK pulls the hand there in every other pose.</item>
    /// </list>
    /// </summary>
    internal static class WeaponsBuilder
    {
        private const string ModelsPath = "Assets/_Project/Art/Weapons";
        private const string PickupsPath = ModelsPath + "/Pickups";
        private const string DataPath = "Assets/_Project/Data/Weapons";
        private const string CatalogPath = DataPath + "/WeaponVisuals.asset";
        private const string WeaponSetPath = "Assets/_Project/Data/Themes/PlaceholderWeaponSet.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Art/Player/SM_Chr_Hunter_Male_01.prefab";

        /// <summary>Same as the characters, so a weapon on the floor is as big as in the hands.</summary>
        private const float PickupScale = 0.75f;

        private const float PickupYaw = 45f; // diagonal: the longest guns fit the cell

        /// <summary>Pose where the clip holds a gun properly with both hands: the left grip mark is taken from it.</summary>
        private const string GripPoseClip = "Player_rifle_weapon_shoot";
        private const float GripPoseTime = 0.5f; // normalized

        private sealed class Weapon
        {
            public string Asset, Id, Model;
            public WeaponSlot Slot;
            public float Damage, AttackSpeed = 1f, MeleeRange = 1.2f, MeleeArc = 100f, FireInterval = 0.3f, ReloadTime = 1.5f,
                ProjectileSpeed = 20f, SoundRadius = 4f;
            public int Magazine = 10;
            public FireMode Mode;
        }

        /// <summary>Draft values: zombies have 30 HP; the player's anims fit any cooldown.</summary>
        private static readonly Weapon[] Weapons =
        {
            Melee("Bat", "bat", "Melee/SM_Wep_Bat_Wood_01", damage: 12f, attackSpeed: 1.2f, range: 1.3f, arc: 100f, sound: 4f),
            Melee("Crowbar", "crowbar", "Melee/SM_Wep_Crowbar_01", damage: 10f, attackSpeed: 1.4f, range: 1.2f, arc: 90f, sound: 3f),
            Melee("FireAxe", "fire_axe", "Melee/SM_Wep_FireAxe_01", damage: 25f, attackSpeed: 0.7f, range: 1.4f, arc: 110f, sound: 5f),
            Melee("Katana", "katana", "Melee/SM_Wep_Katana_01", damage: 15f, attackSpeed: 1.5f, range: 1.4f, arc: 120f, sound: 3f),
            Gun("SubMachineGun", "smg", "Rifle/Auto/SM_Wep_SubMGun_02", FireMode.Automatic, damage: 6f, interval: 0.08f, magazine: 30, reload: 1.6f, speed: 22f, sound: 3f),
            Gun("AssaultRifle01", "assault_rifle_01", "Rifle/Auto/SM_Wep_AssaultRifle_01", FireMode.Automatic, damage: 9f, interval: 0.12f, magazine: 30, reload: 2f, speed: 25f, sound: 8f),
            Gun("AssaultRifle03", "assault_rifle_03", "Rifle/Auto/SM_Wep_AssaultRifle_03", FireMode.Automatic, damage: 10f, interval: 0.14f, magazine: 25, reload: 2f, speed: 25f, sound: 8f),
            Gun("HuntingRifle", "hunting_rifle", "Rifle/SM_Wep_HuntingRifle_Clean_01", FireMode.Single, damage: 25f, interval: 0.9f, magazine: 5, reload: 2.2f, speed: 30f, sound: 9f),
            Gun("SniperRifle", "sniper_rifle", "Rifle/SM_Wep_SniperRifle_01", FireMode.Single, damage: 40f, interval: 1.4f, magazine: 5, reload: 2.6f, speed: 35f, sound: 10f),
            Gun("RevolverRifle", "revolver_rifle", "Rifle/SM_Wep_Hybrid_03", FireMode.Single, damage: 18f, interval: 0.5f, magazine: 6, reload: 2f, speed: 28f, sound: 7f),
        };

        /// <summary>Placeholder definitions of earlier versions, renamed in place (GUID kept, levels stay linked).</summary>
        private static readonly (string From, string To)[] Renames = { ("Knife", "Katana"), ("Pistol", "HuntingRifle") };

        [MenuItem("Maze/Dev/Build Weapons")]
        public static void Build()
        {
            try
            {
                RenameOldDefinitions();
                var definitions = new Dictionary<Weapon, WeaponDefinition>();
                var catalog = LoadOrCreate<WeaponVisualCatalog>(CatalogPath);
                var icons = new Dictionary<WeaponDefinition, Sprite>();
                foreach (var old in catalog.MutableWeapons)
                    if (old?.Definition != null && old.Icon != null) icons[old.Definition] = old.Icon;
                catalog.MutableWeapons.Clear();
                var set = AssetDatabase.LoadAssetAtPath<VisualSet>(WeaponSetPath);
                if (set == null)
                    throw new InvalidOperationException($"'{WeaponSetPath}' not found: run Maze → Dev → Create Placeholder Theme first.");
                set.MutableVariants.Clear();
                set.DefaultVariantId = null;

                var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                var visualsGroup = settings.FindGroup(LevelDesigner.LevelSync.VisualsGroup) ?? settings.DefaultGroup;
                EnsureFolder(PickupsPath);

                foreach (var weapon in Weapons)
                {
                    var definition = DefinitionFor(weapon);
                    definitions[weapon] = definition;

                    var heldPath = $"{ModelsPath}/{weapon.Model}.prefab";
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(heldPath) == null)
                        throw new InvalidOperationException($"Weapon model '{heldPath}' not found.");
                    MarkModel(heldPath, weapon.Slot);
                    icons.TryGetValue(definition, out var icon);
                    catalog.MutableWeapons.Add(new WeaponVisualDefinition(definition, Reference(heldPath), icon));

                    var pickupPath = BuildPickup(heldPath);
                    var pickupGuid = AssetDatabase.AssetPathToGUID(pickupPath);
                    if (settings.FindAssetEntry(pickupGuid) == null)
                        settings.CreateOrMoveEntry(pickupGuid, visualsGroup);
                    set.MutableVariants.Add(new VisualVariant("weapon_" + weapon.Id, definition: definition,
                        prefab: new AssetReferenceGameObject(pickupGuid)));
                }

                EditorUtility.SetDirty(catalog);
                EditorUtility.SetDirty(set);
                EditorUtility.SetDirty(settings);
                RigPlayer();
                MarkGrips();
                var reassigned = ReassignLevels(set);
                var problem = LevelDesigner.LevelSync.SyncShared(settings);
                if (problem != null) Debug.LogWarning("[Maze] " + problem);
                AssetDatabase.SaveAssets();
                UiIconsBuilder.BuildWeaponIcons(catalog); // Models may have changed.
                Debug.Log($"[Maze] Weapons built: {Weapons.Length} definition(s), held and pickup prefabs, player rig; " +
                          $"{reassigned} weapon visual(s) in levels reassigned.");
            }
            catch (Exception e)
            {
                Debug.LogError("[Maze] Building weapons failed: " + e.Message);
                throw;
            }
        }

        private static Weapon Melee(string asset, string id, string model, float damage, float attackSpeed, float range,
            float arc, float sound) =>
            new Weapon
            {
                Asset = asset, Id = id, Model = model, Slot = WeaponSlot.Melee, Damage = damage, AttackSpeed = attackSpeed,
                MeleeRange = range, MeleeArc = arc, SoundRadius = sound,
            };

        private static Weapon Gun(string asset, string id, string model, FireMode mode, float damage, float interval, int magazine,
            float reload, float speed, float sound) =>
            new Weapon
            {
                Asset = asset, Id = id, Model = model, Slot = WeaponSlot.Ranged, Mode = mode, Damage = damage,
                FireInterval = interval, Magazine = magazine, ReloadTime = reload, ProjectileSpeed = speed, SoundRadius = sound,
            };

        private static void RenameOldDefinitions()
        {
            foreach (var (from, to) in Renames)
            {
                var fromPath = $"{DataPath}/{from}.asset";
                if (AssetDatabase.LoadAssetAtPath<WeaponDefinition>(fromPath) == null ||
                    AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{DataPath}/{to}.asset") != null)
                    continue;
                var error = AssetDatabase.RenameAsset(fromPath, to);
                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException($"Renaming '{fromPath}' failed: {error}");
            }
        }

        /// <summary>The definition asset; a new one (or a renamed one with another id) gets the draft values.</summary>
        private static WeaponDefinition DefinitionFor(Weapon weapon)
        {
            var definition = LoadOrCreate<WeaponDefinition>($"{DataPath}/{weapon.Asset}.asset");
            if (definition.Id == weapon.Id && definition.Slot == weapon.Slot)
                return definition;

            definition.Configure(weapon.Id, weapon.Slot, weapon.Magazine);
            definition.ConfigureCombat(weapon.Damage, weapon.AttackSpeed, weapon.FireInterval, weapon.ReloadTime, weapon.Mode,
                weapon.ProjectileSpeed, weapon.MeleeRange, weapon.MeleeArc, weapon.SoundRadius);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        /// <summary>Adds <see cref="WeaponModel"/> with a mark at the far (+Z) end of the mesh: Muzzle for guns, Tip for melee.</summary>
        private static void MarkModel(string path, WeaponSlot slot)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var markName = slot == WeaponSlot.Ranged ? "Muzzle" : "Tip";
                var obsoleteName = slot == WeaponSlot.Ranged ? "Tip" : "Muzzle";
                var obsolete = root.transform.Find(obsoleteName);
                if (obsolete != null) UnityEngine.Object.DestroyImmediate(obsolete.gameObject);

                var mark = root.transform.Find(markName);
                if (mark == null)
                {
                    mark = new GameObject(markName).transform;
                    mark.SetParent(root.transform, false);
                }

                mark.localPosition = FarEnd(root);
                mark.localRotation = Quaternion.identity;

                var model = root.GetComponent<WeaponModel>() ?? root.AddComponent<WeaponModel>();
                var serialized = new SerializedObject(model);
                serialized.FindProperty("_muzzle").objectReferenceValue = slot == WeaponSlot.Ranged ? mark : null;
                serialized.FindProperty("_tip").objectReferenceValue = slot == WeaponSlot.Melee ? mark : null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Centre of the mesh vertices within 3 cm of the farthest +Z point, in the root's space.</summary>
        private static Vector3 FarEnd(GameObject root)
        {
            var points = new List<Vector3>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                foreach (var vertex in mesh.vertices)
                    points.Add(root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
            }

            if (points.Count == 0)
            {
                // Not readable: the bounds' far face is close enough.
                var bounds = new Bounds();
                var first = true;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if (first) bounds = renderer.bounds;
                    else bounds.Encapsulate(renderer.bounds);
                    first = false;
                }

                return root.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.center.y, bounds.max.z));
            }

            var maxZ = float.MinValue;
            foreach (var point in points) maxZ = Mathf.Max(maxZ, point.z);
            var sum = Vector3.zero;
            var count = 0;
            foreach (var point in points)
                if (point.z >= maxZ - 0.03f)
                {
                    sum += point;
                    count++;
                }

            var end = sum / count;
            end.z = maxZ;
            return end;
        }

        /// <summary>
        /// Art/Weapons/Pickups/&lt;model&gt;_Pickup: the held model lying on its side (seen in profile from above),
        /// diagonal, centred in the cell and resting on the floor. Rebuilt in place (GUID kept).
        /// </summary>
        private static string BuildPickup(string heldPath)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(heldPath);
            var path = $"{PickupsPath}/{name}_Pickup.prefab";
            var root = new GameObject(name + "_Pickup");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(heldPath), root.transform);
                model.transform.localScale = Vector3.one * PickupScale;
                model.transform.localRotation = Quaternion.Euler(0f, PickupYaw, 0f) * Quaternion.Euler(0f, 0f, 90f);
                model.transform.localPosition = Vector3.zero;

                var bounds = new Bounds();
                var first = true;
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    if (first) bounds = renderer.bounds;
                    else bounds.Encapsulate(renderer.bounds);
                    first = false;
                }

                model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y + 0.005f, -bounds.center.z);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return path;
        }

        /// <summary>
        /// Palm centres under both hands and the melee socket under the right one, from finger bone positions in hand
        /// space: the grip runs along the knuckles toward the index finger (blade above the thumb), edge toward the
        /// fingers. The Synty hand has a thumb, an index and one merged finger.
        /// </summary>
        private static void RigPlayer()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var animator = root.GetComponent<Animator>();
                var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
                var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                if (right == null || left == null)
                    throw new InvalidOperationException("The player avatar has no hand bones.");

                var palmRight = Child(right, "Palm_R");
                palmRight.localPosition = PalmCentre(animator, right, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightIndexProximal);
                palmRight.localRotation = Quaternion.identity;
                var palmLeft = Child(left, "Palm_L");
                palmLeft.localPosition = PalmCentre(animator, left, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftIndexProximal);
                palmLeft.localRotation = Quaternion.identity;

                var finger = right.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position);
                var index = right.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).position);
                var forward = (index - finger).normalized;
                var up = Vector3.ProjectOnPlane(Vector3.Lerp(finger, index, 0.5f), forward).normalized;
                var socket = Child(right, "WeaponSocket_Melee");
                socket.localPosition = palmRight.localPosition;
                socket.localRotation = Quaternion.LookRotation(forward, up);

                var rig = root.GetComponent<CharacterWeaponRig>() ?? root.AddComponent<CharacterWeaponRig>();
                var serialized = new SerializedObject(rig);
                serialized.FindProperty("_meleeSocket").objectReferenceValue = socket;
                serialized.FindProperty("_palmRight").objectReferenceValue = palmRight;
                serialized.FindProperty("_palmLeft").objectReferenceValue = palmLeft;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Grip_L of every gun: the left palm relative to the gun, in the shooting pose, posed as in game.</summary>
        private static void MarkGrips()
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
            try
            {
                var rig = player.GetComponent<CharacterWeaponRig>();
                var clip = CharacterAnimationUtility.LoadClip(GripPoseClip);
                clip.SampleAnimation(player, clip.length * GripPoseTime);

                foreach (var weapon in Weapons)
                {
                    if (weapon.Slot != WeaponSlot.Ranged) continue;
                    var path = $"{ModelsPath}/{weapon.Model}.prefab";

                    var gun = (GameObject)UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), rig.PalmRight, false);
                    gun.transform.localPosition = Vector3.zero;
                    gun.transform.localRotation = Quaternion.identity;
                    gun.transform.rotation = rig.TwoHandedRotation();
                    var position = gun.transform.InverseTransformPoint(rig.PalmLeft.position);
                    var rotation = Quaternion.Inverse(gun.transform.rotation) * rig.PalmLeft.rotation;
                    UnityEngine.Object.DestroyImmediate(gun);

                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var mark = Child(root.transform, "Grip_L");
                        mark.localPosition = position;
                        mark.localRotation = rotation;
                        var serialized = new SerializedObject(root.GetComponent<WeaponModel>());
                        serialized.FindProperty("_gripLeft").objectReferenceValue = mark;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static Vector3 PalmCentre(Animator animator, Transform hand, HumanBodyBones finger, HumanBodyBones index)
        {
            var knuckles = Vector3.Lerp(animator.GetBoneTransform(finger).position, animator.GetBoneTransform(index).position, 0.5f);
            return Vector3.Lerp(Vector3.zero, hand.InverseTransformPoint(knuckles), 0.6f);
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        /// <summary>Weapons of saved levels whose visual no longer fits their definition get a new one.</summary>
        private static int ReassignLevels(VisualSet set)
        {
            var reassigned = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:LevelData"))
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
                if (level == null || level.VisualTheme == null || level.VisualTheme.GetSet(VisualKind.Weapon) != set)
                    continue;

                var changed = false;
                foreach (var weapon in level.Weapons)
                {
                    var variant = set.FindVariant(level.VisualData.GetObjectAssignment(weapon.Id).VariantId);
                    if (variant != null && variant.Matches(VisualCategory.General, weapon.Definition))
                        continue;
                    VisualAssigner.AssignObject(level, weapon);
                    changed = true;
                    reassigned++;
                }

                if (changed) EditorUtility.SetDirty(level);
            }

            return reassigned;
        }

        private static AssetReferenceGameObject Reference(string path) => new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(path));

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = folder.Substring(0, folder.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(folder.LastIndexOf('/') + 1));
        }
    }
}
