using System.IO;
using Maze.Presentation.UI.Style;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Maze.Editor.Dev
{
    /// <summary>
    /// Maze → Dev → Build UI Style: the shared <c>Maze/UIShape</c> material, the text fonts (static TextMeshPro SDF
    /// atlases of Rajdhani SemiBold and Bold, printable ASCII only, mobile SDF shader) and the <see cref="UiStyle"/>
    /// asset (palette, line widths). An existing style keeps its tuned values; existing font assets are refilled in
    /// place (references survive). Also makes Rajdhani the TextMeshPro default font and removes the TMP sample font and
    /// emoji from <c>Resources</c> (they would ship in every build).
    /// </summary>
    internal static class UiStyleBuilder
    {
        public const string StylePath = "Assets/_Project/Data/UI/UiStyle.asset";
        public const string MaterialPath = "Assets/_Project/Art/UI/UIShape.mat";
        private const string ShaderName = "Maze/UIShape";
        private const string FontFolder = "Assets/_Project/Art/Fonts/Rajdhani";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private static readonly string[] TmpExtras =
        {
            "Assets/TextMesh Pro/Resources/Fonts & Materials",
            "Assets/TextMesh Pro/Resources/Sprite Assets",
            "Assets/TextMesh Pro/Fonts",
            "Assets/TextMesh Pro/Sprites",
        };

        [MenuItem("Maze/Dev/Build UI Style")]
        public static void BuildMenu()
        {
            var style = Build();
            EditorGUIUtility.PingObject(style);
        }

        public static UiStyle Build()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new FileNotFoundException($"Shader '{ShaderName}' not found.");

            EnsureFolder(Path.GetDirectoryName(MaterialPath));
            EnsureFolder(Path.GetDirectoryName(StylePath));

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "UIShape" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            var style = AssetDatabase.LoadAssetAtPath<UiStyle>(StylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<UiStyle>();
                AssetDatabase.CreateAsset(style, StylePath);
            }

            style.ShapeMaterial = material;
            style.Font = BuildFont("Rajdhani-SemiBold");
            style.BoldFont = BuildFont("Rajdhani-Bold");
            EditorUtility.SetDirty(style);
            ConfigureTextMeshPro(style.Font);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Maze] UI style built: {StylePath}.");
            return style;
        }

        /// <summary>Static SDF atlas (512², sampling 56, padding 6) with the printable ASCII characters.</summary>
        private static TMP_FontAsset BuildFont(string name)
        {
            var sourcePath = $"{FontFolder}/{name}.ttf";
            var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null) throw new FileNotFoundException($"Font '{sourcePath}' not found.");

            var fresh = TMP_FontAsset.CreateFontAsset(source, 56, 6, GlyphRenderMode.SDFAA, 512, 512,
                AtlasPopulationMode.Dynamic, false);
            var characters = new System.Text.StringBuilder();
            for (var c = 32; c < 127; c++) characters.Append((char)c);
            fresh.TryAddCharacters(characters.ToString(), out var missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"[Maze] {name}: missing characters '{missing}'.");

            var path = $"{FontFolder}/{name} SDF.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (asset == null)
            {
                asset = fresh;
                asset.name = name + " SDF";
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                // Refill in place: the GUID (and every reference to it) stays.
                EditorUtility.CopySerialized(fresh, asset);
                asset.name = name + " SDF";
                foreach (var old in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                    if (old is Texture2D || old is Material) Object.DestroyImmediate(old, true);
            }

            var atlas = fresh.atlasTexture;
            atlas.name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);

            var material = new Material(Shader.Find("TextMeshPro/Mobile/Distance Field")) { name = name + " Material" };
            material.CopyPropertiesFromMaterial(fresh.material);
            material.shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
            material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
            AssetDatabase.AddObjectToAsset(material, asset);

            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_AtlasPopulationMode").enumValueIndex = (int)AtlasPopulationMode.Static;
            var atlases = serialized.FindProperty("m_AtlasTextures");
            atlases.arraySize = 1;
            atlases.GetArrayElementAtIndex(0).objectReferenceValue = atlas;
            serialized.FindProperty("m_Material").objectReferenceValue = material;
            // Static atlas: the TTF is not needed at runtime and should not ship.
            var sourceFont = serialized.FindProperty("m_SourceFontFile");
            if (sourceFont != null) sourceFont.objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (fresh != asset) Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }

        private static void ConfigureTextMeshPro(TMP_FontAsset font)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning("[Maze] TMP Settings not found: import TMP Essential Resources.");
                return;
            }

            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            serialized.FindProperty("m_fallbackFontAssets").arraySize = 0;
            serialized.FindProperty("m_defaultSpriteAsset").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);

            foreach (var folder in TmpExtras)
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
