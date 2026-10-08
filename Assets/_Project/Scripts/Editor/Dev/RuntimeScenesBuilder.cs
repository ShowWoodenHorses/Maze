using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maze.Application.Save;
using Maze.Composition;
using Maze.Presentation.Map;
using Maze.Presentation.UI;
using Maze.Presentation.UI.Touch;
using Maze.Presentation.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Maze.Editor.Dev
{
    /// <summary>
    /// Creates the runtime scenes (ТЗ §6–7) with placeholder uGUI screens and puts them into Build Settings:
    /// Bootstrap (first; ProjectLifetimeScope, application UI) and Game (additive; LevelLifetimeScope, level view root,
    /// top-down camera, light).
    /// Re-running overwrites both scenes.
    /// </summary>
    internal static class RuntimeScenesBuilder
    {
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string BootstrapPath = ScenesFolder + "/Bootstrap.unity";
        public const string GamePath = ScenesFolder + "/Game.unity";

        private static readonly Color Background = new Color(0.08f, 0.09f, 0.11f, 1f);
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color Panel = new Color(0.16f, 0.17f, 0.2f, 0.95f);
        private static readonly Color ButtonColor = new Color(0.27f, 0.29f, 0.34f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.92f, 1f);

        private static Font _font;

        [MenuItem("Maze/Dev/Build Runtime Scenes")]
        public static void Build()
        {
            var existing = File.Exists(BootstrapPath) || File.Exists(GamePath);
            if (existing && !EditorUtility.DisplayDialog("Build Runtime Scenes",
                    "Bootstrap and Game scenes already exist and will be overwritten.", "Overwrite", "Cancel"))
                return;

            var problem = Rebuild();
            if (problem != null)
                EditorUtility.DisplayDialog("Build Runtime Scenes", problem, "OK");
        }

        /// <summary>Builds both scenes without asking (tools, automation). Returns why it could not, or null.</summary>
        public static string Rebuild()
        {
            // Scenes are created additively and closed after saving, so the open scenes stay as they are.
            // Unity cannot add a new scene next to an untitled one.
            var openPaths = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).path).ToList();
            if (openPaths.Any(string.IsNullOrEmpty))
                return "Save or close the untitled scene first.";
            if (openPaths.Contains(BootstrapPath) || openPaths.Contains(GamePath))
                return "Close the Bootstrap and Game scenes first.";

            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            BuildGameScene();
            BuildBootstrapScene();
            RegisterInBuildSettings();

            Debug.Log($"[Maze] Runtime scenes built: {BootstrapPath} (first in Build Settings), {GamePath}.");
            return null;
        }

        private static void BuildGameScene()
        {
            var scene = NewScene();

            // Top-down camera: frames the level when it is built, follows the player later.
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.depth = 0;
            cameraObject.transform.SetPositionAndRotation(new Vector3(10f, 22f, 4f), Quaternion.Euler(60f, 0f, 0f));
            var topDown = cameraObject.AddComponent<TopDownCamera>();
            SetReference(topDown, "_camera", camera);

            // Weak "moon" without shadows; LevelLighting sets color and intensity from the theme.
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.intensity = 0.35f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.25f, 0.34f);

            var viewRoot = new GameObject("Level View").AddComponent<LevelViewRoot>();
            SetReference(viewRoot, "_moon", light);

            var scopeObject = new GameObject("LevelLifetimeScope");
            var scope = scopeObject.AddComponent<LevelLifetimeScope>();
            var serializedScope = new SerializedObject(scope);
            serializedScope.FindProperty("autoRun").boolValue = false;
            serializedScope.ApplyModifiedPropertiesWithoutUndo();
            SetReference(scope, "_viewRoot", viewRoot);
            SetReference(scope, "_camera", topDown);

            SaveAndClose(scene, GamePath);
        }

        private static void BuildBootstrapScene()
        {
            var scene = NewScene();

            // Renders the menus while no level is loaded; the Game scene camera draws on top of it.
            var cameraObject = new GameObject("UI Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.cullingMask = 0;
            camera.depth = -10;
            cameraObject.AddComponent<AudioListener>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var ui = BuildUI();
            SetReference(ui, "_eventSystem", eventSystem.GetComponent<EventSystem>());
            var touch = BuildTouchControls();
            SetReference(ui, "_touchControls", touch);
            SetReference(ui.Hud, "_touchControls", touch);

            var scopeObject = new GameObject("ProjectLifetimeScope");
            var scope = scopeObject.AddComponent<ProjectLifetimeScope>();
            SetReference(scope, "_ui", ui);

            SaveAndClose(scene, BootstrapPath);
        }

        private static Scene NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene); // New GameObjects go to the active scene.
            return scene;
        }

        private static void SaveAndClose(Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static UIRoot BuildUI()
        {
            var canvasObject = new GameObject("UI", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            var root = canvasObject.AddComponent<UIRoot>();
            var parent = canvasObject.transform;

            SetReference(root, "_mainMenu", BuildMainMenu(parent));
            SetReference(root, "_loading", BuildLoading(parent));
            SetReference(root, "_hud", BuildHud(parent));
            SetReference(root, "_pause", BuildPause(parent));
            SetReference(root, "_confirmExit", BuildConfirmExit(parent));
            SetReference(root, "_map", BuildMap(parent));
            SetReference(root, "_result", BuildResult(parent));
            SetReference(root, "_settings", BuildSettings(parent));
            SetReference(root, "_error", BuildError(parent));
            SetReference(root, "_fpsCounter", BuildFpsCounter(parent)); // last: over every screen
            AddUiSounds(root);
            return root;
        }

        /// <summary>
        /// Every button and toggle gets a <see cref="UiSound"/>: a click; Back for back / close / "No"; silent where the
        /// screen it opens sounds by itself (pause, resume, map).
        /// </summary>
        private static void AddUiSounds(UIRoot root)
        {
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (!(selectable is Button) && !(selectable is Toggle)) continue;
                var screen = selectable.GetComponentInParent<UIScreen>(true);
                var name = selectable.gameObject.name;
                var kind = UiSoundKind.Click;
                if (screen is HudScreen && (name == "PauseButton" || name == "MapButton")) kind = UiSoundKind.None;
                else if (screen is PauseScreen && name == "ResumeButton") kind = UiSoundKind.None;
                else if (screen is MapScreen && name == "CloseButton") kind = UiSoundKind.None;
                else if (name == "BackButton" || name == "NoButton") kind = UiSoundKind.Back;

                var sound = selectable.gameObject.AddComponent<UiSound>();
                sound.Kind = kind;
            }
        }

        private static MainMenuScreen BuildMainMenu(Transform parent)
        {
            var screen = Screen<MainMenuScreen>(parent, "MainMenuScreen", Background);
            var column = Column(screen.transform, 520f, 24f);
            Label(column, "Title", "MAZE", 72, FontStyle.Bold, 110f);
            Label(column, "Subtitle", "Select a level", 28, FontStyle.Normal, 50f);

            var list = new GameObject("LevelList", typeof(RectTransform));
            list.transform.SetParent(column, false);
            var listLayout = list.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 12f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandHeight = false;
            list.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var template = Button(list.transform, "LevelButtonTemplate", "Level");
            template.gameObject.SetActive(false);
            var empty = Label(column, "EmptyLabel", "No levels yet: run Build / Sync in Maze > Level Designer.", 24,
                FontStyle.Italic, 60f);
            var summary = Label(column, "Summary", "", 24, FontStyle.Normal, 40f);
            var settings = Button(column, "SettingsButton", "Settings");
            var reset = Button(column, "DebugResetProgressButton", "Debug: reset progress");

            SetReference(screen, "_levelList", list.GetComponent<RectTransform>());
            SetReference(screen, "_levelButtonTemplate", template);
            SetReference(screen, "_emptyLabel", empty);
            SetReference(screen, "_summary", summary);
            SetReference(screen, "_settingsButton", settings);
            SetReference(screen, "_debugResetProgressButton", reset);
            return screen;
        }

        private static LoadingScreen BuildLoading(Transform parent)
        {
            var screen = Screen<LoadingScreen>(parent, "LoadingScreen", Background);
            var column = Column(screen.transform, 600f, 0f);
            Label(column, "Label", "Loading...", 40, FontStyle.Normal, 80f);
            return screen;
        }

        private static HudScreen BuildHud(Transform parent)
        {
            var screen = Screen<HudScreen>(parent, "HudScreen", Color.clear);
            screen.GetComponent<Image>().raycastTarget = false;
            screen.gameObject.AddComponent<SafeAreaFitter>(); // Notches and rounded corners.

            // First child: under the texts. Its texture is made by the screen, it is switched on only while pulsing.
            var damageObject = new GameObject("DamageFlash", typeof(RectTransform));
            damageObject.transform.SetParent(screen.transform, false);
            Stretch(damageObject.GetComponent<RectTransform>());
            var damageFlash = damageObject.AddComponent<RawImage>();
            damageFlash.raycastTarget = false;
            damageFlash.enabled = false;

            var levelName = Label(screen.transform, "LevelName", "Level", 30, FontStyle.Bold, 60f);
            levelName.alignment = TextAnchor.MiddleLeft;
            var nameRect = levelName.rectTransform;
            nameRect.anchorMin = nameRect.anchorMax = nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(32f, -24f);
            nameRect.sizeDelta = new Vector2(800f, 60f);

            var pause = Button(screen.transform, "PauseButton", "II");
            var pauseRect = pause.GetComponent<RectTransform>();
            pauseRect.anchorMin = pauseRect.anchorMax = pauseRect.pivot = new Vector2(1f, 1f);
            pauseRect.anchoredPosition = new Vector2(-32f, -24f);
            pauseRect.sizeDelta = new Vector2(90f, 90f);

            var map = Button(screen.transform, "MapButton", "Map");
            var mapRect = map.GetComponent<RectTransform>();
            mapRect.anchorMin = mapRect.anchorMax = mapRect.pivot = new Vector2(1f, 1f);
            mapRect.anchoredPosition = new Vector2(-138f, -24f);
            mapRect.sizeDelta = new Vector2(120f, 90f);

            var status = Label(screen.transform, "Status", "", 24, FontStyle.Normal, 120f);
            status.alignment = TextAnchor.UpperLeft;
            var statusRect = status.rectTransform;
            statusRect.anchorMin = statusRect.anchorMax = statusRect.pivot = new Vector2(0f, 1f);
            statusRect.anchoredPosition = new Vector2(32f, -90f);
            statusRect.sizeDelta = new Vector2(800f, 120f);

            var message = Label(screen.transform, "Message", "", 30, FontStyle.Bold, 60f);
            var messageRect = message.rectTransform;
            messageRect.anchorMin = messageRect.anchorMax = messageRect.pivot = new Vector2(0.5f, 0.5f);
            messageRect.anchoredPosition = new Vector2(0f, -160f);
            messageRect.sizeDelta = new Vector2(900f, 60f);

            SetReference(screen, "_levelName", levelName);
            SetReference(screen, "_status", status);
            SetReference(screen, "_message", message);
            SetReference(screen, "_pauseButton", pause);
            SetReference(screen, "_mapButton", map);
            SetReference(screen, "_damageFlash", damageFlash);
            return screen;
        }

        /// <summary>
        /// ТЗ §64 Android: a separate overlay canvas under the application UI (screens with a dimmed background cover
        /// it), shown by the HUD. Units are 0.1 mm (see <see cref="TouchControls"/>), inside the safe area.
        /// Stick zone on the left part of the screen (the stick floats there or rests near its corner); attack
        /// bottom-right under the thumb, use above it, melee and ranged to its left; map and pause are HUD buttons at
        /// the top. Everything is mirrored for the left-handed layout. They emulate gamepad controls bound by InputService.
        /// The player may move and resize the stick and buttons (TouchLayoutHandle on each, disabled) in the layout
        /// editor: a dim background under them and a toolbar at the top, both hidden until editing.
        /// </summary>
        private static TouchControls BuildTouchControls()
        {
            var go = new GameObject("TouchControls", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1;
            go.AddComponent<GraphicRaycaster>();
            var controls = go.AddComponent<TouchControls>();
            var knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // Layout editing: under the controls, over the application UI (the canvas goes on top then).
            var editBackground = new GameObject("EditBackground", typeof(RectTransform));
            editBackground.transform.SetParent(go.transform, false);
            Stretch(editBackground.GetComponent<RectTransform>());
            editBackground.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
            editBackground.SetActive(false);

            var content = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(go.transform, false);
            Stretch(content);
            content.gameObject.AddComponent<SafeAreaFitter>();
            var group = content.gameObject.AddComponent<CanvasGroup>(); // Opacity: not the editor toolbar.

            var zone = new GameObject("StickZone", typeof(RectTransform)).GetComponent<RectTransform>();
            zone.SetParent(content, false);
            zone.anchorMin = Vector2.zero;
            zone.anchorMax = new Vector2(0.45f, 0.72f);
            zone.pivot = Vector2.zero;
            zone.offsetMin = zone.offsetMax = Vector2.zero;
            zone.gameObject.AddComponent<Image>().color = Color.clear; // Catches touches, draws nothing visible.

            var ring = Rect(zone, "Ring", Vector2.zero, new Vector2(230f, 230f), new Vector2(240f, 240f));
            var ringImage = ring.gameObject.AddComponent<Image>();
            ringImage.sprite = knobSprite;
            ringImage.color = new Color(1f, 1f, 1f, 0.2f); // Its touches go up to the zone (the stick) or a layout handle.
            var knob = Rect(ring, "Knob", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
            var knobImage = knob.gameObject.AddComponent<Image>();
            knobImage.sprite = knobSprite;
            knobImage.color = new Color(1f, 1f, 1f, 0.75f);
            knobImage.raycastTarget = false;

            var stick = zone.gameObject.AddComponent<TouchStick>();
            SetReference(stick, "_ring", ring);
            SetReference(stick, "_knob", knob);
            var serializedStick = new SerializedObject(stick);
            serializedStick.FindProperty("_restFromCorner").vector2Value = new Vector2(230f, 230f);
            serializedStick.FindProperty("_radius").floatValue = 100f;
            serializedStick.ApplyModifiedPropertiesWithoutUndo();

            // In TouchElement order.
            var mirrored = new List<RectTransform>
            {
                zone,
                TouchButton(content, "Attack", "<Gamepad>/buttonSouth", new Vector2(-200f, 200f), 170f, 32, knobSprite),
                TouchButton(content, "Use", "<Gamepad>/buttonWest", new Vector2(-200f, 410f), 110f, 26, knobSprite),
                TouchButton(content, "Melee", "<Gamepad>/leftShoulder", new Vector2(-400f, 140f), 100f, 24, knobSprite),
                TouchButton(content, "Ranged", "<Gamepad>/rightShoulder", new Vector2(-400f, 300f), 100f, 24, knobSprite),
            };

            var editor = BuildTouchLayoutEditor(go.transform, controls, editBackground);
            AddLayoutHandle(ring.gameObject, TouchElement.Stick, editor);
            for (var i = 1; i < mirrored.Count; i++)
                AddLayoutHandle(mirrored[i].gameObject, (TouchElement)i, editor);

            SetReference(controls, "_canvas", canvas);
            SetReference(controls, "_group", group);
            SetReference(controls, "_stick", stick);
            SetReference(controls, "_area", content);
            SetReference(controls, "_editor", editor);
            SetReferences(controls, "_mirrored", mirrored.Cast<Object>().ToList());
            go.SetActive(false); // The HUD shows it on touch devices.
            return controls;
        }

        private static void AddLayoutHandle(GameObject target, TouchElement element, TouchLayoutEditor editor)
        {
            var handle = target.AddComponent<TouchLayoutHandle>();
            var serialized = new SerializedObject(handle);
            serialized.FindProperty("_element").enumValueIndex = (int)element;
            serialized.FindProperty("_editor").objectReferenceValue = editor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            handle.enabled = false; // TouchControls enables it while editing.
        }

        /// <summary>
        /// Toolbar of the layout editor at the top of the safe area (canvas units are 0.1 mm): hint, size of the selected
        /// control, Reset layout, Done.
        /// </summary>
        private static TouchLayoutEditor BuildTouchLayoutEditor(Transform parent, TouchControls controls, GameObject background)
        {
            var panel = new GameObject("LayoutEditor", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.AddComponent<SafeAreaFitter>();

            var bar = Column(panel.transform, 640f, 8f, Panel);
            var barRect = (RectTransform)bar;
            barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = new Vector2(0f, -10f);
            var barLayout = bar.GetComponent<VerticalLayoutGroup>();
            barLayout.padding = new RectOffset(16, 16, 12, 12);

            Label(bar, "Hint", "Drag the stick and buttons. Tap one to resize it.", 24, FontStyle.Normal, 32f);

            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(bar, false);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 16f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            row.AddComponent<LayoutElement>().preferredHeight = 80f;

            var sizeColumn = new GameObject("Size", typeof(RectTransform));
            sizeColumn.transform.SetParent(row.transform, false);
            var sizeLayout = sizeColumn.AddComponent<VerticalLayoutGroup>();
            sizeLayout.childControlWidth = true;
            sizeLayout.childControlHeight = true;
            sizeLayout.childForceExpandHeight = false;
            sizeColumn.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var (sizeLabel, size) = SliderRow(sizeColumn.transform, "Size");
            sizeLabel.fontSize = 24;
            var handle = (RectTransform)size.transform.Find("Handle Slide Area/Handle");
            if (handle != null) handle.sizeDelta = new Vector2(36f, 0f);

            var reset = Button(row.transform, "ResetLayoutButton", "Reset layout");
            var done = Button(row.transform, "DoneButton", "Done");
            foreach (var button in new[] { reset, done })
            {
                button.GetComponent<LayoutElement>().preferredWidth = 170f;
                button.GetComponentInChildren<Text>().fontSize = 26;
            }

            var editor = panel.AddComponent<TouchLayoutEditor>();
            SetReference(editor, "_controls", controls);
            SetReference(editor, "_background", background);
            SetReference(editor, "_size", size);
            SetReference(editor, "_sizeLabel", sizeLabel);
            SetReference(editor, "_resetButton", reset);
            SetReference(editor, "_doneButton", done);
            panel.SetActive(false);
            return editor;
        }

        private static RectTransform TouchButton(Transform parent, string label, string controlPath, Vector2 fromBottomRight,
            float size, int fontSize, Sprite sprite)
        {
            var rect = Rect(parent, label + "Button", new Vector2(1f, 0f), fromBottomRight, new Vector2(size, size));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = new Color(1f, 1f, 1f, 0.5f);
            rect.gameObject.AddComponent<OnScreenButton>().controlPath = controlPath;

            var text = Label(rect, "Text", label, fontSize, FontStyle.Bold, size);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(text.rectTransform);
            return rect;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static ConfirmExitScreen BuildConfirmExit(Transform parent)
        {
            var screen = Screen<ConfirmExitScreen>(parent, "ConfirmExitScreen", Dim);
            var column = Column(screen.transform, 520f, 16f, Panel);
            Label(column, "Title", "Finish level?", 44, FontStyle.Bold, 90f);
            var yes = Button(column, "YesButton", "Yes");
            var no = Button(column, "NoButton", "No");

            SetReference(screen, "_yesButton", yes);
            SetReference(screen, "_noButton", no);
            return screen;
        }

        private static PauseScreen BuildPause(Transform parent)
        {
            var screen = Screen<PauseScreen>(parent, "PauseScreen", Dim);
            var column = Column(screen.transform, 480f, 16f, Panel);
            Label(column, "Title", "Paused", 48, FontStyle.Bold, 90f);
            var resume = Button(column, "ResumeButton", "Resume");
            var retry = Button(column, "RetryButton", "Restart level");
            var settings = Button(column, "SettingsButton", "Settings");
            var exit = Button(column, "ExitButton", "Exit to menu");

            var debugGroup = new GameObject("DebugGroup", typeof(RectTransform));
            debugGroup.transform.SetParent(column, false);
            var debugLayout = debugGroup.AddComponent<HorizontalLayoutGroup>();
            debugLayout.spacing = 12f;
            debugLayout.childControlWidth = true;
            debugLayout.childControlHeight = true;
            debugGroup.AddComponent<LayoutElement>().preferredHeight = 60f;
            var complete = Button(debugGroup.transform, "DebugCompleteButton", "Debug: complete");
            var fail = Button(debugGroup.transform, "DebugFailButton", "Debug: fail");

            SetReference(screen, "_resumeButton", resume);
            SetReference(screen, "_retryButton", retry);
            SetReference(screen, "_settingsButton", settings);
            SetReference(screen, "_exitButton", exit);
            SetReference(screen, "_debugGroup", debugGroup);
            SetReference(screen, "_debugCompleteButton", complete);
            SetReference(screen, "_debugFailButton", fail);
            return screen;
        }

        private static Toggle MapToggle(Transform parent, string name, string text, float x)
        {
            var toggle = ToggleRow(parent, name, text);
            var rect = (RectTransform)toggle.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -42f);
            rect.sizeDelta = new Vector2(210f, 54f);
            toggle.GetComponentInChildren<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
            return toggle;
        }

        private static ResultScreen BuildResult(Transform parent)
        {
            var screen = Screen<ResultScreen>(parent, "ResultScreen", Dim);
            var column = Column(screen.transform, 480f, 16f, Panel);
            var title = Label(column, "Title", "Level complete", 48, FontStyle.Bold, 90f);
            var details = Label(column, "Details", "", 28, FontStyle.Normal, 170f);
            details.alignment = TextAnchor.UpperLeft;
            var retry = Button(column, "RetryButton", "Play again");
            var menu = Button(column, "MenuButton", "Main menu");

            SetReference(screen, "_title", title);
            SetReference(screen, "_details", details);
            SetReference(screen, "_retryButton", retry);
            SetReference(screen, "_menuButton", menu);
            return screen;
        }

        /// <summary>ТЗ §60: fullscreen map; the texture (one pixel per cell) is set by the level's MapPresenter.</summary>
        private static MapScreen BuildMap(Transform parent)
        {
            var screen = Screen<MapScreen>(parent, "MapScreen", Background);

            var caption = Label(screen.transform, "Caption", "Map", 30, FontStyle.Bold, 60f);
            var captionRect = caption.rectTransform;
            captionRect.anchorMin = new Vector2(0f, 1f);
            captionRect.anchorMax = new Vector2(1f, 1f);
            captionRect.pivot = new Vector2(0.5f, 1f);
            captionRect.anchoredPosition = new Vector2(0f, -24f);
            captionRect.sizeDelta = new Vector2(-300f, 60f);

            var close = Button(screen.transform, "CloseButton", "Close");
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-32f, -24f);
            closeRect.sizeDelta = new Vector2(180f, 90f);

            // Area below the caption; the image keeps the level's aspect ratio inside it.
            var area = new GameObject("MapArea", typeof(RectTransform));
            area.transform.SetParent(screen.transform, false);
            var areaRect = area.GetComponent<RectTransform>();
            Stretch(areaRect);
            areaRect.offsetMin = new Vector2(32f, 32f);
            areaRect.offsetMax = new Vector2(-32f, -130f);

            var imageObject = new GameObject("MapImage", typeof(RectTransform));
            imageObject.transform.SetParent(area.transform, false);
            var image = imageObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            var fitter = imageObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            // Icon switches at the top left (the caption is centred).
            var showPlayer = MapToggle(screen.transform, "ShowPlayer", "Player", 32f);
            var showFragments = MapToggle(screen.transform, "ShowFragments", "Map pieces", 252f);

            var icons = AssetDatabase.LoadAssetAtPath<MapIconSet>(MapIconsBuilder.SetPath);
            if (icons == null) icons = MapIconsBuilder.Build();

            SetReference(screen, "_icons", icons);
            SetReference(screen, "_showPlayer", showPlayer);
            SetReference(screen, "_showFragments", showFragments);
            SetReference(screen, "_image", image);
            SetReference(screen, "_fitter", fitter);
            SetReference(screen, "_caption", caption);
            SetReference(screen, "_closeButton", close);
            return screen;
        }

        /// <summary>
        /// Small FPS text on the left of the safe area, under the HUD status and above the stick (corners may be cut off
        /// on some devices), over every screen; ignores touches. Hidden until
        /// the setting switches it on.
        /// </summary>
        private static FpsCounter BuildFpsCounter(Transform parent)
        {
            var area = new GameObject("FpsCounter", typeof(RectTransform));
            area.transform.SetParent(parent, false);
            Stretch(area.GetComponent<RectTransform>());
            area.AddComponent<SafeAreaFitter>();

            var label = Label(area.transform, "Text", "FPS -", 22, FontStyle.Bold, 30f);
            label.alignment = TextAnchor.UpperLeft;
            label.color = new Color(1f, 1f, 1f, 0.85f);
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            var rect = label.rectTransform;
            // Left, under the HUD status (ends at -210) and above the stick zone; inset like the HUD texts.
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(32f, -220f);
            rect.sizeDelta = new Vector2(200f, 30f);

            var counter = area.AddComponent<FpsCounter>();
            SetReference(counter, "_text", label);
            area.SetActive(false);
            return counter;
        }

        /// <summary>
        /// Settings: an overlay over the main menu or the pause screen. Tabs Controls / Shooting / Other (SettingsTab
        /// order), one page at a time; the pages area keeps the height of the tallest page.
        /// </summary>
        private static SettingsScreen BuildSettings(Transform parent)
        {
            var screen = Screen<SettingsScreen>(parent, "SettingsScreen", Dim);
            // Opaque: the main menu text must not show through.
            var column = Column(screen.transform, 680f, 6f, new Color(Panel.r, Panel.g, Panel.b, 1f));
            Label(column, "Title", "Settings", 40, FontStyle.Bold, 56f);

            var tabs = Row(column, "Tabs", 64f);
            var tabButtons = new List<Object>
            {
                Button(tabs, "ControlsTab", "Controls"),
                Button(tabs, "ShootingTab", "Shooting"),
                Button(tabs, "OtherTab", "Other"),
            };

            var pages = new GameObject("Pages", typeof(RectTransform));
            pages.transform.SetParent(column, false);
            var pagesLayout = pages.AddComponent<VerticalLayoutGroup>();
            pagesLayout.childControlWidth = true;
            pagesLayout.childControlHeight = true;
            pagesLayout.childForceExpandHeight = false;
            pagesLayout.padding = new RectOffset(0, 0, 10, 0);
            pages.AddComponent<LayoutElement>().minHeight = 562f; // The Controls page with the top padding.

            var controlsPage = Page(pages.transform, "ControlsPage");
            var (deadZoneLabel, deadZone) = SliderRow(controlsPage, "DeadZone");
            var (sensitivityLabel, sensitivity) = SliderRow(controlsPage, "Sensitivity");
            var (sizeLabel, size) = SliderRow(controlsPage, "Size");
            var (opacityLabel, opacity) = SliderRow(controlsPage, "Opacity");
            var floating = ToggleRow(controlsPage, "FloatingStick", "Floating stick (appears under the thumb)");
            var leftHanded = ToggleRow(controlsPage, "LeftHanded", "Left-handed layout");
            var editLayout = Button(controlsPage, "EditLayoutButton", "Edit button layout");

            var shootingPage = Page(pages.transform, "ShootingPage");
            var aimMode = Button(shootingPage, "AimModeButton", "Aim");
            var aimAssist = ToggleRow(shootingPage, "AimAssist", "Auto-aim at zombies");

            var otherPage = Page(pages.transform, "OtherPage");
            var (musicLabel, music) = SliderRow(otherPage, "MusicVolume");
            var (sfxLabel, sfx) = SliderRow(otherPage, "SfxVolume");
            var showFps = ToggleRow(otherPage, "ShowFps", "Show FPS counter");

            var buttons = new GameObject("Buttons", typeof(RectTransform));
            buttons.transform.SetParent(column, false);
            var buttonsLayout = buttons.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 12f;
            buttonsLayout.childControlWidth = true;
            buttonsLayout.childControlHeight = true;
            buttons.AddComponent<LayoutElement>().preferredHeight = 72f;
            var reset = Button(buttons.transform, "ResetButton", "Reset to defaults");
            var back = Button(buttons.transform, "BackButton", "Back");

            SetReferences(screen, "_tabButtons", tabButtons);
            SetReferences(screen, "_pages", new List<Object> { controlsPage.gameObject, shootingPage.gameObject, otherPage.gameObject });
            SetReference(screen, "_editLayoutButton", editLayout);
            SetReference(screen, "_deadZone", deadZone);
            SetReference(screen, "_deadZoneLabel", deadZoneLabel);
            SetReference(screen, "_sensitivity", sensitivity);
            SetReference(screen, "_sensitivityLabel", sensitivityLabel);
            SetReference(screen, "_size", size);
            SetReference(screen, "_sizeLabel", sizeLabel);
            SetReference(screen, "_opacity", opacity);
            SetReference(screen, "_opacityLabel", opacityLabel);
            SetReference(screen, "_floatingStick", floating);
            SetReference(screen, "_leftHanded", leftHanded);
            SetReference(screen, "_aimModeButton", aimMode);
            SetReference(screen, "_aimModeLabel", aimMode.GetComponentInChildren<Text>());
            SetReference(screen, "_aimAssist", aimAssist);
            SetReference(screen, "_musicVolume", music);
            SetReference(screen, "_musicVolumeLabel", musicLabel);
            SetReference(screen, "_sfxVolume", sfx);
            SetReference(screen, "_sfxVolumeLabel", sfxLabel);
            SetReference(screen, "_showFps", showFps);
            SetReference(screen, "_resetButton", reset);
            SetReference(screen, "_backButton", back);
            return screen;
        }

        private static ErrorScreen BuildError(Transform parent)
        {
            var screen = Screen<ErrorScreen>(parent, "ErrorScreen", Background);
            var column = Column(screen.transform, 900f, 16f, Panel);
            Label(column, "Title", "Something went wrong", 44, FontStyle.Bold, 80f);
            var message = Label(column, "Message", "Error", 24, FontStyle.Normal, 220f);
            message.alignment = TextAnchor.UpperLeft;
            var back = Button(column, "BackButton", "Back to menu");

            SetReference(screen, "_message", message);
            SetReference(screen, "_backButton", back);
            return screen;
        }

        // ---- uGUI helpers ----

        private static T Screen<T>(Transform parent, string name, Color background) where T : UIScreen
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            go.AddComponent<Image>().color = background;
            var screen = go.AddComponent<T>();
            go.SetActive(false); // ScreenRouter shows the right one.
            return screen;
        }

        private static Transform Column(Transform parent, float width, float spacing, Color? background = null)
        {
            var go = new GameObject("Column", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);

            if (background.HasValue)
                go.AddComponent<Image>().color = background.Value;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = background.HasValue ? new RectOffset(32, 32, 32, 32) : new RectOffset();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go.transform;
        }

        /// <summary>Equal-width children side by side.</summary>
        private static Transform Row(Transform parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            go.AddComponent<LayoutElement>().preferredHeight = height;
            return go.transform;
        }

        /// <summary>A settings page: rows top to bottom.</summary>
        private static Transform Page(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            return go.transform;
        }

        private static Text Label(Transform parent, string name, string text, int size, FontStyle style, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = TextColor;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            go.AddComponent<LayoutElement>().preferredHeight = height;
            return label;
        }

        private static Button Button(Transform parent, string name, string text)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = ButtonColor;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            go.AddComponent<LayoutElement>().preferredHeight = 72f;

            var label = Label(go.transform, "Text", text, 30, FontStyle.Normal, 72f);
            Stretch(label.rectTransform);
            return button;
        }

        private static DefaultControls.Resources ControlResources() => new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
        };

        /// <summary>A caption (its text is set by the screen) above a slider.</summary>
        private static (Text, Slider) SliderRow(Transform parent, string name)
        {
            var label = Label(parent, name + "Label", name, 26, FontStyle.Normal, 38f);
            label.alignment = TextAnchor.MiddleLeft;
            var go = DefaultControls.CreateSlider(ControlResources());
            go.name = name + "Slider";
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 40f;
            return (label, go.GetComponent<Slider>());
        }

        private static Toggle ToggleRow(Transform parent, string name, string text)
        {
            var go = DefaultControls.CreateToggle(ControlResources());
            go.name = name + "Toggle";
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 54f;
            var toggle = go.GetComponent<Toggle>();

            var box = (RectTransform)go.transform.Find("Background");
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            box.sizeDelta = new Vector2(44f, 44f);
            Stretch((RectTransform)box.Find("Checkmark"));

            var label = go.transform.Find("Label").GetComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = 26;
            label.color = TextColor;
            label.alignment = TextAnchor.MiddleLeft;
            var labelRect = label.rectTransform;
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(60f, 0f);
            return toggle;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
                throw new System.InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReferences(Object target, string field, IReadOnlyList<Object> values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null || !property.isArray)
                throw new System.InvalidOperationException($"{target.GetType().Name} has no serialized array '{field}'.");
            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterInBuildSettings()
        {
            var ours = new[] { BootstrapPath, GamePath };
            var scenes = new List<EditorBuildSettingsScene>(ours.Select(path => new EditorBuildSettingsScene(path, true)));
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => !ours.Contains(s.path)));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
