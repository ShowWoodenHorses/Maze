using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maze.Composition;
using Maze.Presentation.UI;
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

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Hard; // ТЗ §103: no heavy realtime shadows.
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var viewRoot = new GameObject("Level View").AddComponent<LevelViewRoot>();

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
            SetReference(root, "_result", BuildResult(parent));
            SetReference(root, "_error", BuildError(parent));
            return root;
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
            var empty = Label(column, "EmptyLabel", "No levels yet: run Build / Sync in Maze → Level Designer.", 24,
                FontStyle.Italic, 60f);

            SetReference(screen, "_levelList", list.GetComponent<RectTransform>());
            SetReference(screen, "_levelButtonTemplate", template);
            SetReference(screen, "_emptyLabel", empty);
            return screen;
        }

        private static LoadingScreen BuildLoading(Transform parent)
        {
            var screen = Screen<LoadingScreen>(parent, "LoadingScreen", Background);
            var column = Column(screen.transform, 600f, 0f);
            Label(column, "Label", "Loading…", 40, FontStyle.Normal, 80f);
            return screen;
        }

        private static HudScreen BuildHud(Transform parent)
        {
            var screen = Screen<HudScreen>(parent, "HudScreen", Color.clear);
            screen.GetComponent<Image>().raycastTarget = false;

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
            SetReference(screen, "_touchControls", BuildTouchControls(screen.transform));
            return screen;
        }

        /// <summary>
        /// ТЗ §64 Android: stick bottom-left, attack bottom-right, plus melee, ranged, map and door buttons.
        /// They emulate gamepad controls, which InputService already binds.
        /// </summary>
        private static GameObject BuildTouchControls(Transform parent)
        {
            var root = new GameObject("TouchControls", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var stickArea = Rect(root.transform, "StickArea", new Vector2(0f, 0f), new Vector2(240f, 240f), new Vector2(320f, 320f));
            var area = stickArea.gameObject.AddComponent<Image>();
            area.sprite = knob;
            area.color = new Color(1f, 1f, 1f, 0.12f);
            var handle = Rect(stickArea, "Stick", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = knob;
            handleImage.color = new Color(1f, 1f, 1f, 0.45f);
            var stick = handle.gameObject.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/leftStick";
            stick.movementRange = 110f;

            TouchButton(root.transform, "Attack", "<Gamepad>/buttonSouth", new Vector2(-230f, 230f), 220f, knob);
            TouchButton(root.transform, "Melee", "<Gamepad>/leftShoulder", new Vector2(-470f, 150f), 120f, knob);
            TouchButton(root.transform, "Ranged", "<Gamepad>/rightShoulder", new Vector2(-470f, 310f), 120f, knob);
            TouchButton(root.transform, "Door", "<Gamepad>/buttonWest", new Vector2(-230f, 470f), 130f, knob);
            TouchButton(root.transform, "Map", "<Gamepad>/select", new Vector2(-150f, 650f), 110f, knob);
            return root;
        }

        private static void TouchButton(Transform parent, string label, string controlPath, Vector2 fromBottomRight, float size, Sprite sprite)
        {
            var rect = Rect(parent, label + "Button", new Vector2(1f, 0f), fromBottomRight, new Vector2(size, size));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = new Color(1f, 1f, 1f, 0.3f);
            rect.gameObject.AddComponent<OnScreenButton>().controlPath = controlPath;

            var text = Label(rect, "Text", label, 26, FontStyle.Bold, size);
            Stretch(text.rectTransform);
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
            SetReference(screen, "_exitButton", exit);
            SetReference(screen, "_debugGroup", debugGroup);
            SetReference(screen, "_debugCompleteButton", complete);
            SetReference(screen, "_debugFailButton", fail);
            return screen;
        }

        private static ResultScreen BuildResult(Transform parent)
        {
            var screen = Screen<ResultScreen>(parent, "ResultScreen", Dim);
            var column = Column(screen.transform, 480f, 16f, Panel);
            var title = Label(column, "Title", "Level complete", 48, FontStyle.Bold, 90f);
            var retry = Button(column, "RetryButton", "Play again");
            var menu = Button(column, "MenuButton", "Main menu");

            SetReference(screen, "_title", title);
            SetReference(screen, "_retryButton", retry);
            SetReference(screen, "_menuButton", menu);
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

        private static void RegisterInBuildSettings()
        {
            var ours = new[] { BootstrapPath, GamePath };
            var scenes = new List<EditorBuildSettingsScene>(ours.Select(path => new EditorBuildSettingsScene(path, true)));
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => !ours.Contains(s.path)));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
