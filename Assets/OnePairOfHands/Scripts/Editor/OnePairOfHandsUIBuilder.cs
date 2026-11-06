using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OnePairOfHands.EditorTools
{
    /// <summary>
    /// Builds the UI flow: MainMenu scene, the in-game overlay (opening, control bar, pause, settings,
    /// how to play, leaving, tooltips, end), HUD restyle, placeholder audio, build settings and players.
    /// Menu: Tools/One Pair of Hands/...
    /// </summary>
    public static partial class OnePairOfHandsUIBuilder
    {
        public const string GameScenePath = "Assets/Skyden_Games/Low Poly Environment/Scenes/Level Design.unity";
        public const string MenuScenePath = "Assets/OnePairOfHands/Scenes/MainMenu.unity";
        private const string UIFolder = "Assets/OnePairOfHands/UI";
        private const string AudioFolder = "Assets/OnePairOfHands/Resources/Audio";
        private const string BackgroundPath = UIFolder + "/MenuBackground.png";
        private const string RoundedPath = UIFolder + "/Rounded.png";

        private static Sprite rounded;
        private static Sprite white;

        // ================================================================== entry points

        [MenuItem("Tools/One Pair of Hands/Build UI Flow (scenes + UI + audio)")]
        public static void BuildAll()
        {
            EditorSceneManager.SaveOpenScenes();
            EnsureFolder("Assets/OnePairOfHands", "Scenes");
            EnsureFolder("Assets/OnePairOfHands", "UI");
            EnsureFolder("Assets/OnePairOfHands", "Resources");
            EnsureFolder("Assets/OnePairOfHands/Resources", "Audio");

            GenerateAudio();
            rounded = RoundedSprite();
            white = WhiteSprite();
            BuildMinimapSprites();

            // ---- Game scene
            var game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            CleanupMissingScripts();
            RenderMenuBackground();
            ConfigureClock();
            RestyleHUD();
            BuildGameUI();
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);

            // ---- Main menu scene
            var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildMainMenu();
            EditorSceneManager.SaveScene(menu, MenuScenePath);

            // ---- Build settings: MainMenu first
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };

            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Debug.Log("[OnePairOfHands] UI flow built: MainMenu + Level Design, build settings updated.");
        }

        [MenuItem("Tools/One Pair of Hands/Build Windows x64")]
        public static void BuildWindows()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/Windows/OnePairOfHands.exe"));
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, GameScenePath },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[OnePairOfHands] Windows build: {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB -> {path}");
        }

        public static bool WebGLSupported => BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        [MenuItem("Tools/One Pair of Hands/Build WebGL")]
        public static void BuildWebGL()
        {
            if (!WebGLSupported)
            {
                Debug.LogWarning("[OnePairOfHands] WebGL build support is not installed; skipping WebGL build.");
                return;
            }
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/WebGL"));
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, GameScenePath },
                locationPathName = path,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[OnePairOfHands] WebGL build: {report.summary.result}, {report.summary.totalErrors} errors -> {path}");
        }

        // ================================================================== scene prep

        private static void CleanupMissingScripts()
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name != "Main Camera" && go.name != "GameObject") continue;
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                if (removed > 0) Debug.Log($"[OnePairOfHands] Removed {removed} missing script(s) from '{go.name}'.");
            }
        }

        private static void ConfigureClock()
        {
            var clock = Object.FindFirstObjectByType<GameClock>();
            if (clock == null) return;
            var so = new SerializedObject(clock);
            so.FindProperty("waitForBegin").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            var tasks = GameObject.Find("OnePairOfHands/Tasks");
            if (tasks != null && tasks.GetComponent<TextScaler>() == null) tasks.AddComponent<TextScaler>();
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.productName = "One Pair of Hands";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
        }

        // ================================================================== HUD restyle

        private static void RestyleHUD()
        {
            var hudGo = GameObject.Find("OnePairOfHands/HUD");
            if (hudGo == null) { Debug.LogWarning("HUD not found"); return; }
            var hud = hudGo.GetComponent<HUDController>();

            var oldEnd = hudGo.transform.Find("EndPanel");
            if (oldEnd != null) Object.DestroyImmediate(oldEnd.gameObject);

            var so = new SerializedObject(hud);
            so.FindProperty("endPanel").objectReferenceValue = null;
            so.FindProperty("endListText").objectReferenceValue = null;
            so.FindProperty("fillColour").colorValue = UIPalette.Sand;
            so.FindProperty("heldText").colorValue = UIPalette.SandLight;
            so.FindProperty("slippingText").colorValue = new Color(0.70f, 0.84f, 0.83f, 1f);
            so.FindProperty("notBeenText").colorValue = new Color(0.62f, 0.68f, 0.67f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var n in new[] { "ClockPanel", "TaskPanel", "MeanwhilePanel" })
            {
                var t = hudGo.transform.Find(n);
                if (t == null) continue;
                var img = t.GetComponent<Image>();
                img.sprite = rounded;
                img.type = Image.Type.Sliced;
                img.color = UIPalette.DeepTeal;
            }

            // Make room for the control bar along the bottom.
            var taskPanel = (RectTransform)hudGo.transform.Find("TaskPanel");
            if (taskPanel != null) taskPanel.anchoredPosition = new Vector2(0f, 124f);

            foreach (var tmp in hudGo.GetComponentsInChildren<TMP_Text>(true))
                if (tmp.color.r > 0.7f && tmp.color.g > 0.7f && tmp.color.b > 0.7f && tmp.color.b < 0.85f) tmp.color = UIPalette.SoftText;

            BuildMinimap(hudGo);
            if (hudGo.GetComponent<TextScaler>() == null) hudGo.AddComponent<TextScaler>();

            var adj = hudGo.GetComponent<PortraitAdjuster>();
            if (adj != null) Object.DestroyImmediate(adj);
            adj = hudGo.AddComponent<PortraitAdjuster>();
            var mw = (RectTransform)hudGo.transform.Find("MeanwhilePanel");
            var carry = (RectTransform)hudGo.transform.Find("CarryText");
            if (mw != null)
            {
                mw.sizeDelta = new Vector2(500f, mw.sizeDelta.y); // room for "3 Teach the children" at text size L
                adj.Add(mw, new Vector2(24f, -24f), new Vector2(24f, -120f));
            }
            if (carry != null) adj.Add(carry, new Vector2(0f, -112f), new Vector2(0f, -520f));
            EditorUtility.SetDirty(adj);
        }

        // ================================================================== in-game UI

        private static void BuildGameUI()
        {
            var root = GameObject.Find("OnePairOfHands");
            var old = root.transform.Find("GameUI");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var canvas = MakeCanvas("GameUI", root.transform, 20);
            var flow = canvas.gameObject.AddComponent<GameFlowController>();
            var t = canvas.transform;

            // Pause icon (top right)
            var pauseBtn = MakeButton(t, "PauseButton", "Pause", 28, 150, 72);
            Anchor((RectTransform)pauseBtn.transform, new Vector2(1f, 1f), new Vector2(-24f, -24f));

            // First-play tooltip
            var tip = Panel(t, "TooltipPanel", 780, new Vector2(0.5f, 0f), new Vector2(0f, 380f), 28, 16);
            // Small diamond that points up at the minimap for the fourth tooltip.
            var tipPointer = new GameObject("Pointer", typeof(RectTransform));
            tipPointer.layer = 5;
            var tprt = (RectTransform)tipPointer.transform;
            tprt.SetParent(tip, false);
            tprt.anchorMin = tprt.anchorMax = new Vector2(1f, 1f);
            tprt.pivot = new Vector2(0.5f, 0.5f);
            tprt.sizeDelta = new Vector2(30f, 30f);
            tprt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tipPointer.AddComponent<LayoutElement>().ignoreLayout = true;
            var tpi = tipPointer.AddComponent<Image>();
            tpi.sprite = white;
            tpi.color = UIPalette.DeepTealSolid;
            tpi.raycastTarget = false;
            tipPointer.SetActive(false);
            var tipCounter = Label(tip, "Counter", "1 / 4", 22, UIPalette.SoftText, TextAlignmentOptions.Left);
            var tipText = Label(tip, "Text", "Click or tap the ground to walk.", 32, UIPalette.SandLight, TextAlignmentOptions.Left);
            var tipRow = Row(tip, "Buttons", 16, TextAnchor.MiddleRight);
            var tipSkip = MakeButton(tipRow, "TooltipSkipButton", "Skip", 26, 170, 64);
            var tipNext = MakeButton(tipRow, "TooltipNextButton", "Next", 26, 170, 64);

            // How to play + settings (shared)
            var howTo = BuildHowToPanel(t, out var howToClose);
            var settings = BuildSettingsPanel(t);

            // Pause menu
            var pauseDim = Dim(t, "PausePanel", 0.75f);
            var pausePanel = Panel(pauseDim, "Box", 560, new Vector2(0.5f, 0.5f), Vector2.zero, 40, 18);
            Label(pausePanel, "Title", "Paused", 52, UIPalette.SandLight, TextAlignmentOptions.Center, FontStyles.Bold);
            var resume = MakeButton(pausePanel, "ResumeButton", "Resume", 32, 0, 84);
            var pHow = MakeButton(pausePanel, "PauseHowToButton", "How to Play", 32, 0, 84);
            var pSet = MakeButton(pausePanel, "PauseSettingsButton", "Settings", 32, 0, 84);
            var pMenu = MakeButton(pausePanel, "PauseMainMenuButton", "Main Menu", 32, 0, 84);

            // Leaving screen
            var leaveDim = Dim(t, "LeavePanel", 0.86f);
            var leaveBox = Panel(leaveDim, "Box", 900, new Vector2(0.5f, 0.5f), Vector2.zero, 48, 28);
            var leaveClock = Label(leaveBox, "ClockText", "08:00 — still running", 52, UIPalette.SandLight, TextAlignmentOptions.Center, FontStyles.Bold);
            Label(leaveBox, "Line", "You can stop here. Ataa could not.", 36, UIPalette.SandLight, TextAlignmentOptions.Center);
            var leaveRow = Row(leaveBox, "Buttons", 32, TextAnchor.MiddleCenter);
            var goBack = MakeButton(leaveRow, "GoBackButton", "Go back", 34, 300, 88);
            var leaveBtn = MakeButton(leaveRow, "LeaveConfirmButton", "Leave", 34, 300, 88);

            // End screen
            var endDim = Dim(t, "EndPanel", 0.9f);
            var endBox = Panel(endDim, "Box", 980, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), 44, 14);
            Label(endBox, "Heading", "The day", 60, UIPalette.SandLight, TextAlignmentOptions.Center, FontStyles.Bold);
            var names = new TMP_Text[4];
            var states = new TMP_Text[4];
            string[] taskNames = { "Collect water", "Cook food", "Teach the children", "Work online" };
            for (int i = 0; i < 4; i++)
            {
                var row = Row(endBox, $"Row_{i}", 20, TextAnchor.MiddleCenter);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
                names[i] = Label(row, "Name", taskNames[i], 36, UIPalette.SandLight, TextAlignmentOptions.Right);
                states[i] = Label(row, "State", "held", 36, UIPalette.Sand, TextAlignmentOptions.Left, FontStyles.Italic);
            }
            Spacer(endBox, 10);
            Label(endBox, "Positionality", "We are students outside Ataa's experience. This day is our adaptation of her recorded account, not a record of it.", 28, UIPalette.SoftText, TextAlignmentOptions.Center);
            Label(endBox, "Closing", "This is a small part of Ataa's reality, not the whole story.", 32, UIPalette.SandLight, TextAlignmentOptions.Center, FontStyles.Italic);
            var story = MakeButton(endBox, "StoryButton", "Hear Ataa's story", 30, 0, 80);
            Spacer(endBox, 10);
            var endRow = Row(endBox, "Buttons", 28, TextAnchor.MiddleCenter);
            var endMenu = MakeButton(endRow, "EndMainMenuButton", "Main Menu", 30, 280, 80);
            var again = MakeButton(endRow, "PlayAgainButton", "Play again", 30, 280, 80);

            // Opening screen (on top of everything but the control bar)
            var openDim = Dim(t, "OpeningPanel", 0.95f);
            var openBox = Panel(openDim, "Box", 960, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), 52, 24);
            Label(openBox, "Title", "Before you begin", 54, UIPalette.SandLight, TextAlignmentOptions.Center, FontStyles.Bold);
            Label(openBox, "Ethics", "This game is based on Ataa's recorded testimony from The Refugee Archive: her own account of daily life in Za'atari camp.", 32, UIPalette.SandLight, TextAlignmentOptions.Center);
            Label(openBox, "Positionality", "We are students outside Ataa's experience. This is our adaptation of her recorded account, not a record of it.", 30, UIPalette.SoftText, TextAlignmentOptions.Center, FontStyles.Italic);
            var begin = MakeButton(openBox, "BeginButton", "Begin", 36, 0, 92);

            // Control bar (always on top, bottom of the screen)
            var bar = BuildControlBar(t);

            // Order: HUD-level things first, overlays later, bar last.
            pauseBtn.transform.SetSiblingIndex(0);
            bar.transform.SetAsLastSibling();

            canvas.gameObject.AddComponent<TextScaler>();
            canvas.gameObject.AddComponent<UIClickSounds>();

            var so = new SerializedObject(flow);
            so.FindProperty("clock").objectReferenceValue = Object.FindFirstObjectByType<GameClock>();
            so.FindProperty("taskManager").objectReferenceValue = Object.FindFirstObjectByType<TaskManager>();
            so.FindProperty("openingPanel").objectReferenceValue = openDim.gameObject;
            so.FindProperty("beginButton").objectReferenceValue = begin;
            so.FindProperty("pauseButton").objectReferenceValue = pauseBtn;
            so.FindProperty("pausePanel").objectReferenceValue = pauseDim.gameObject;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("pauseHowToButton").objectReferenceValue = pHow;
            so.FindProperty("pauseSettingsButton").objectReferenceValue = pSet;
            so.FindProperty("pauseMainMenuButton").objectReferenceValue = pMenu;
            so.FindProperty("settingsPanel").objectReferenceValue = settings;
            so.FindProperty("howToPanel").objectReferenceValue = howTo;
            so.FindProperty("howToCloseButton").objectReferenceValue = howToClose;
            so.FindProperty("controlBar").objectReferenceValue = bar;
            so.FindProperty("leavePanel").objectReferenceValue = leaveDim.gameObject;
            so.FindProperty("leaveClockText").objectReferenceValue = leaveClock;
            so.FindProperty("goBackButton").objectReferenceValue = goBack;
            so.FindProperty("leaveConfirmButton").objectReferenceValue = leaveBtn;
            so.FindProperty("endPanel").objectReferenceValue = endDim.gameObject;
            SetArray(so, "endRowNames", names);
            SetArray(so, "endRowStates", states);
            so.FindProperty("storyButton").objectReferenceValue = story;
            so.FindProperty("endMainMenuButton").objectReferenceValue = endMenu;
            so.FindProperty("playAgainButton").objectReferenceValue = again;
            so.FindProperty("tooltipPanel").objectReferenceValue = tip.gameObject;
            so.FindProperty("tooltipText").objectReferenceValue = tipText;
            so.FindProperty("tooltipCounter").objectReferenceValue = tipCounter;
            so.FindProperty("tooltipNextButton").objectReferenceValue = tipNext;
            so.FindProperty("tooltipSkipButton").objectReferenceValue = tipSkip;
            so.FindProperty("tooltipPointer").objectReferenceValue = tipPointer;
            var mapPanel = GameObject.Find("OnePairOfHands/HUD/Minimap");
            so.FindProperty("minimapPanel").objectReferenceValue = mapPanel != null ? mapPanel.transform : null;
            string[] tipTexts =
            {
                "Click or tap the ground to walk.",
                "Walk to a numbered place, then click or tap it to work there. At the well, hold to fill the jug.",
                "The Meanwhile strip (top left) shows what is slipping while you are somewhere else.",
                "The map in the corner shows where each task is. Tap a number to walk there.",
            };
            var tips = so.FindProperty("tooltips");
            tips.arraySize = tipTexts.Length;
            for (int i = 0; i < tipTexts.Length; i++) tips.GetArrayElementAtIndex(i).stringValue = tipTexts[i];
            so.FindProperty("minimapTooltipIndex").intValue = 3;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureEventSystem();
        }

        private static ControlBar BuildControlBar(Transform parent)
        {
            var go = new GameObject("ControlBar", typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 104f);
            var bg = go.AddComponent<Image>();
            bg.sprite = white;
            bg.color = UIPalette.DeepTealSolid;
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(24, 24, 14, 14);
            h.spacing = 12;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            var lbl = Label(rt, "TextLabel", "Text", 26, UIPalette.SoftText, TextAlignmentOptions.Right);
            Fixed(lbl, 80);
            var s = MakeButton(rt, "Bar_TextS", "S", 28, 76, 72);
            var m = MakeButton(rt, "Bar_TextM", "M", 28, 76, 72);
            var l = MakeButton(rt, "Bar_TextL", "L", 28, 76, 72);
            Flex(rt, "Gap1");
            var audio = MakeButton(rt, "AudioToggle", "Audio: on", 28, 230, 72);
            Flex(rt, "Gap2");
            var leave = MakeButton(rt, "LeaveButton", "Leave", 28, 180, 72);

            var bar = go.AddComponent<ControlBar>();
            var so = new SerializedObject(bar);
            SetArray(so, "textSizeButtons", new Object[] { s, m, l });
            so.FindProperty("audioButton").objectReferenceValue = audio;
            so.FindProperty("audioLabel").objectReferenceValue = audio.GetComponentInChildren<TMP_Text>();
            so.FindProperty("leaveButton").objectReferenceValue = leave;
            so.ApplyModifiedPropertiesWithoutUndo();
            return bar;
        }

        // ================================================================== shared panels

        private static GameObject BuildHowToPanel(Transform parent, out Button close)
        {
            var dim = Dim(parent, "HowToPlayPanel", 0.88f);
            var box = new GameObject("Box", typeof(RectTransform));
            box.layer = 5;
            var brt = (RectTransform)box.transform;
            brt.SetParent(dim, false);
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(1000f, 900f);
            var img = box.AddComponent<Image>();
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.color = UIPalette.DeepTeal;
            var v = box.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(44, 44, 36, 36);
            v.spacing = 18;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            Label(brt, "Title", "How to Play", 52, UIPalette.SandLight, TextAlignmentOptions.Center, FontStyles.Bold);

            // Scrollable body (keeps working at large text sizes and on portrait screens)
            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.layer = 5;
            var srt = (RectTransform)scrollGo.transform;
            srt.SetParent(brt, false);
            scrollGo.AddComponent<LayoutElement>().flexibleHeight = 1;
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.layer = 5;
            var vrt = (RectTransform)viewport.transform;
            vrt.SetParent(srt, false);
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = vrt.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();
            var vimg = viewport.AddComponent<Image>();
            vimg.color = new Color(0, 0, 0, 0.001f);
            var content = new GameObject("Content", typeof(RectTransform));
            content.layer = 5;
            var crt = (RectTransform)content.transform;
            crt.SetParent(vrt, false);
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.sizeDelta = Vector2.zero;
            var cv = content.AddComponent<VerticalLayoutGroup>();
            cv.spacing = 14;
            cv.padding = new RectOffset(0, 16, 0, 0);
            cv.childControlWidth = cv.childControlHeight = true;
            cv.childForceExpandWidth = true;
            cv.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = vrt;
            scroll.content = crt;

            Label(crt, "MechanicHead", "One pair of hands", 34, UIPalette.Sand, TextAlignmentOptions.Left, FontStyles.Bold);
            Label(crt, "Mechanic", "You can only be in one place at a time. What you don't tend slowly slips.", 30, UIPalette.SandLight, TextAlignmentOptions.Left);
            Label(crt, "ControlsHead", "Controls", 34, UIPalette.Sand, TextAlignmentOptions.Left, FontStyles.Bold);
            Label(crt, "Controls",
                "• Click or tap the ground to walk.\n" +
                "• Click or tap a task to work there.\n" +
                "• Hold to fill water at the well.\n" +
                "• Scroll or pinch to zoom.\n" +
                "• Esc to pause.", 30, UIPalette.SandLight, TextAlignmentOptions.Left);
            Label(crt, "Map", "The map in the corner shows where each task is. Tap a number to walk there.", 30, UIPalette.SandLight, TextAlignmentOptions.Left);
            Label(crt, "TasksHead", "The four tasks", 34, UIPalette.Sand, TextAlignmentOptions.Left, FontStyles.Bold);
            Label(crt, "Tasks",
                "1  Collect water: cross the bridge to the well, hold to fill the jug, then carry it home. It only counts once it's home.\n" +
                "2  Cook food: at the fire by the caravans, tap to stir the pot.\n" +
                "3  Teach the children: at the tent, tap to answer the children's questions.\n" +
                "4  Work online: at the caravan screen, tap to send work. The connection sometimes drops.", 30, UIPalette.SandLight, TextAlignmentOptions.Left);
            Label(crt, "GoalHead", "The day", 34, UIPalette.Sand, TextAlignmentOptions.Left, FontStyles.Bold);
            Label(crt, "Goal", "Get through the day, 08:00 to 16:00. There is no winning, only what you could hold.", 30, UIPalette.SandLight, TextAlignmentOptions.Left);

            close = MakeButton(brt, "HowToCloseButton", "Close", 30, 0, 80);
            dim.gameObject.SetActive(false);
            return dim.gameObject;
        }

        private static SettingsPanel BuildSettingsPanel(Transform parent)
        {
            var dim = Dim(parent, "SettingsPanel", 0.88f);
            var box = Panel(dim, "Box", 960, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), 36, 14);
            Label(box, "Title", "Settings", 52, UIPalette.SandLight, TextAlignmentOptions.Center, FontStyles.Bold);

            // Master volume
            var volRow = Row(box, "VolumeRow", 20, TextAnchor.MiddleLeft);
            var volLbl = Label(volRow, "Label", "Master volume", 30, UIPalette.SandLight, TextAlignmentOptions.Left);
            Fixed(volLbl, 280);
            var sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources { standard = rounded, background = rounded, knob = rounded });
            sliderGo.name = "VolumeSlider";
            sliderGo.layer = 5;
            sliderGo.transform.SetParent(volRow, false);
            var sle = sliderGo.AddComponent<LayoutElement>();
            sle.flexibleWidth = 1;
            sle.preferredHeight = 44;
            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 1;
            sliderGo.transform.Find("Background").GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
            sliderGo.transform.Find("Fill Area/Fill").GetComponent<Image>().color = UIPalette.Sand;
            var knob = sliderGo.transform.Find("Handle Slide Area/Handle");
            knob.GetComponent<Image>().color = UIPalette.Ochre;
            ((RectTransform)knob).sizeDelta = new Vector2(44f, 0f);
            foreach (var r in sliderGo.GetComponentsInChildren<RectTransform>()) r.gameObject.layer = 5;
            var volVal = Label(volRow, "Value", "80%", 30, UIPalette.Sand, TextAlignmentOptions.Right);
            Fixed(volVal, 100);

            // Text size
            var tsRow = Row(box, "TextSizeRow", 14, TextAnchor.MiddleLeft);
            Fixed(Label(tsRow, "Label", "Text size", 30, UIPalette.SandLight, TextAlignmentOptions.Left), 280);
            var ts = new Button[3];
            for (int i = 0; i < 3; i++) ts[i] = MakeButton(tsRow, $"TextSize_{GameSettings.TextSizeLabels[i]}", GameSettings.TextSizeLabels[i], 30, 110, 72);

            // Day length
            Label(box, "DayLabel", "Day length", 30, UIPalette.SandLight, TextAlignmentOptions.Left);
            var dlRow = Row(box, "DayLengthRow", 14, TextAnchor.MiddleCenter);
            dlRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            var dl = new Button[3];
            string[] dlNames = { "DayLength_Short", "DayLength_Normal", "DayLength_Long" };
            for (int i = 0; i < 3; i++) dl[i] = MakeButton(dlRow, dlNames[i], GameSettings.DayLengthLabels[i], 28, 0, 72);
            Label(box, "DayNote", "Day length applies when a day begins.", 24, UIPalette.SoftText, TextAlignmentOptions.Left, FontStyles.Italic);

            // Fullscreen (desktop .exe only)
            var fsRow = Row(box, "FullscreenRow", 14, TextAnchor.MiddleLeft);
            Fixed(Label(fsRow, "Label", "Fullscreen", 30, UIPalette.SandLight, TextAlignmentOptions.Left), 280);
            var fsOff = MakeButton(fsRow, "Fullscreen_Off", "Off", 30, 140, 72);
            var fsOn = MakeButton(fsRow, "Fullscreen_On", "On", 30, 140, 72);

            // Minimap on/off
            var mmRow = Row(box, "MinimapRow", 14, TextAnchor.MiddleLeft);
            Fixed(Label(mmRow, "Label", "Minimap", 30, UIPalette.SandLight, TextAlignmentOptions.Left), 280);
            var mmOff = MakeButton(mmRow, "Minimap_Off", "Off", 30, 140, 72);
            var mmOn = MakeButton(mmRow, "Minimap_On", "On", 30, 140, 72);

            Spacer(box, 4);
            var close = MakeButton(box, "SettingsCloseButton", "Close", 30, 0, 80);

            var panel = dim.gameObject.AddComponent<SettingsPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("volumeSlider").objectReferenceValue = slider;
            so.FindProperty("volumeValue").objectReferenceValue = volVal;
            SetArray(so, "textSizeButtons", ts);
            SetArray(so, "dayLengthButtons", dl);
            so.FindProperty("fullscreenRow").objectReferenceValue = fsRow.gameObject;
            SetArray(so, "fullscreenButtons", new Object[] { fsOff, fsOn });
            SetArray(so, "minimapButtons", new Object[] { mmOff, mmOn });
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
            dim.gameObject.SetActive(false);
            return panel;
        }

        // ================================================================== main menu

        private static void BuildMainMenu()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UIPalette.DeepTealSolid;
            camGo.AddComponent<AudioListener>();

            var canvas = MakeCanvas("MenuCanvas", null, 0);
            var t = canvas.transform;

            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.layer = 5;
            var bgrt = (RectTransform)bgGo.transform;
            bgrt.SetParent(t, false);
            bgrt.anchorMin = Vector2.zero;
            bgrt.anchorMax = Vector2.one;
            bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;
            var raw = bgGo.AddComponent<RawImage>();
            raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            raw.raycastTarget = false;
            bgGo.AddComponent<MenuBackgroundPan>();

            var shade = Dim(t, "Shade", 0.35f);
            shade.GetComponent<Image>().raycastTarget = false;

            // Title block
            var titleBox = Panel(t, "TitleBox", 1000, new Vector2(0.5f, 1f), new Vector2(0f, -120f), 40, 10);
            Label(titleBox, "Title", "One Pair of Hands", 92, UIPalette.SandLight, TextAlignmentOptions.Center, FontStyles.Bold);
            Label(titleBox, "Subtitle", "A day in Za'atari camp, from Ataa's testimony", 34, UIPalette.Sand, TextAlignmentOptions.Center, FontStyles.Italic);

            // Buttons
            var col = new GameObject("MenuButtons", typeof(RectTransform));
            col.layer = 5;
            var colrt = (RectTransform)col.transform;
            colrt.SetParent(t, false);
            colrt.anchorMin = colrt.anchorMax = new Vector2(0.5f, 0.5f);
            colrt.pivot = new Vector2(0.5f, 0.5f);
            colrt.anchoredPosition = new Vector2(0f, -140f);
            colrt.sizeDelta = new Vector2(540f, 0f);
            var v = col.AddComponent<VerticalLayoutGroup>();
            v.spacing = 18;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            col.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var start = MakeButton(colrt, "StartButton", "Start", 36, 0, 92);
            var how = MakeButton(colrt, "HowToPlayButton", "How to Play", 34, 0, 86);
            var set = MakeButton(colrt, "SettingsButton", "Settings", 34, 0, 86);
            var quit = MakeButton(colrt, "QuitButton", "Quit", 34, 0, 86);

            var howTo = BuildHowToPanel(t, out var howClose);
            var settings = BuildSettingsPanel(t);

            canvas.gameObject.AddComponent<TextScaler>();
            canvas.gameObject.AddComponent<UIClickSounds>();
            var mm = canvas.gameObject.AddComponent<MainMenuController>();
            var so = new SerializedObject(mm);
            so.FindProperty("startButton").objectReferenceValue = start;
            so.FindProperty("howToButton").objectReferenceValue = how;
            so.FindProperty("settingsButton").objectReferenceValue = set;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.FindProperty("menuButtons").objectReferenceValue = col;
            so.FindProperty("howToPanel").objectReferenceValue = howTo;
            so.FindProperty("howToCloseButton").objectReferenceValue = howClose;
            so.FindProperty("settingsPanel").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureEventSystem();
        }

        // ================================================================== background render

        private static void RenderMenuBackground()
        {
            var ataa = GameObject.Find("OnePairOfHands/Ataa");
            var tasks = GameObject.Find("OnePairOfHands/Tasks");
            var labels = new List<GameObject>();
            if (tasks != null)
                foreach (var l in tasks.GetComponentsInChildren<WorldLabel>(true)) { labels.Add(l.gameObject); l.gameObject.SetActive(false); }
            bool ataaActive = ataa != null && ataa.activeSelf;
            if (ataa != null) ataa.SetActive(false);

            var camGo = new GameObject("BG_RenderCam");
            var cam = camGo.AddComponent<Camera>();
            if (Camera.main != null) cam.CopyFrom(Camera.main);
            cam.transform.position = new Vector3(-132f, 30f, 96f);
            cam.transform.LookAt(new Vector3(-78f, -4f, 140f));
            cam.fieldOfView = 50f;
            cam.farClipPlane = 600f;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            File.WriteAllBytes(BackgroundPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);

            foreach (var l in labels) l.SetActive(true);
            if (ataa != null) ataa.SetActive(ataaActive);

            AssetDatabase.ImportAsset(BackgroundPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(BackgroundPath);
            imp.textureType = TextureImporterType.Default;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }

        // ================================================================== audio (placeholder clips)

        private static void GenerateAudio()
        {
            const int rate = 22050;

            // Ambient: soft wind (smoothed noise) with a slow, periodic swell; seamless 24 s loop.
            int len = rate * 24, fade = rate * 3;
            var rng = new System.Random(7);
            var raw = new float[len + fade];
            float lp1 = 0f, lp2 = 0f, lpHiss = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += (n - lp1) * 0.02f;
                lp2 += (lp1 - lp2) * 0.05f;
                lpHiss += (n - lpHiss) * 0.25f;
                raw[i] = lp2 * 6f + lpHiss * 0.04f;
            }
            var amb = new float[len];
            for (int i = 0; i < len; i++)
            {
                float s = raw[i];
                if (i < fade)
                {
                    float k = (float)i / fade;
                    s = raw[i] * k + raw[len + i] * (1f - k);
                }
                double t = (double)i / rate;
                float swell = 0.65f + 0.25f * (float)System.Math.Sin(2 * System.Math.PI * t / 24.0) + 0.10f * (float)System.Math.Sin(2 * System.Math.PI * t / 8.0 + 1.3);
                amb[i] = s * swell;
            }
            Normalise(amb, 0.45f);
            WriteWav($"{AudioFolder}/ambient_camp.wav", amb, rate);

            // UI click: short soft wooden tick.
            int clen = (int)(rate * 0.06f);
            var click = new float[clen];
            for (int i = 0; i < clen; i++)
            {
                double t = (double)i / rate;
                float env = (float)System.Math.Exp(-t * 70.0);
                float f = 900f - 400f * (float)(t / 0.06);
                click[i] = env * (float)System.Math.Sin(2 * System.Math.PI * f * t) * 0.8f;
            }
            Normalise(click, 0.6f);
            WriteWav($"{AudioFolder}/ui_click.wav", click, rate);
            AssetDatabase.Refresh();
        }

        private static void Normalise(float[] d, float peak)
        {
            float m = 0.0001f;
            foreach (var x in d) m = Mathf.Max(m, Mathf.Abs(x));
            for (int i = 0; i < d.Length; i++) d[i] = d[i] / m * peak;
        }

        private static void WriteWav(string assetPath, float[] samples, int rate)
        {
            using var fs = new FileStream(assetPath, FileMode.Create);
            using var w = new BinaryWriter(fs);
            int dataBytes = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataBytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataBytes);
            foreach (var s in samples) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }

        // ================================================================== sprites

        private static Sprite RoundedSprite()
        {
            const int size = 64;
            const float r = 18f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                    float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            File.WriteAllBytes(RoundedPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(RoundedPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(RoundedPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = new Vector4(22, 22, 22, 22);
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
        }

        private static Sprite WhiteSprite()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(UIFolder + "/White.png");
            return s != null ? s : rounded;
        }

        // ================================================================== UI factory

        private static Canvas MakeCanvas(string name, Transform parent, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        private static RectTransform Dim(Transform parent, string name, float alpha)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = white;
            var c = UIPalette.Dim;
            c.a = alpha;
            img.color = c;
            img.raycastTarget = true; // blocks clicks to the world behind it
            return rt;
        }

        /// <summary>Rounded deep-teal panel that grows to fit its (vertically laid out) content.</summary>
        private static RectTransform Panel(Transform parent, string name, float width, Vector2 anchor, Vector2 pos, int padding, int spacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 0f);
            var img = go.AddComponent<Image>();
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.color = UIPalette.DeepTeal;
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        private static RectTransform Row(Transform parent, string name, int spacing, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return rt;
        }

        private static void Spacer(Transform parent, float h)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
        }

        private static void Flex(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().flexibleWidth = 1;
        }

        private static TMP_Text Label(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            return t;
        }

        /// <summary>Rounded sand button with deep-teal text. width 0 = stretch to the layout.</summary>
        private static Button MakeButton(Transform parent, string name, string text, float size, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(width > 0 ? width : 300f, height);
            var img = go.AddComponent<Image>();
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.color = UIPalette.Sand;
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 0.97f, 0.9f);
            cb.pressedColor = new Color(0.85f, 0.8f, 0.72f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            b.colors = cb;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            if (width > 0)
            {
                le.preferredWidth = width;
                le.minWidth = width;
            }
            else le.flexibleWidth = 1;

            var lt = new GameObject("Label", typeof(RectTransform));
            lt.layer = 5;
            var lrt = (RectTransform)lt.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10f, 4f);
            lrt.offsetMax = new Vector2(-10f, -4f);
            var t = lt.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = UIPalette.Ink;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return b;
        }

        private static void Fixed(TMP_Text t, float width)
        {
            var le = t.GetComponent<LayoutElement>();
            le.flexibleWidth = 0;
            le.preferredWidth = width;
            le.minWidth = width;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            var le = rt.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
        }

        private static void SetArray(SerializedObject so, string field, IList<Object> items)
        {
            var p = so.FindProperty(field);
            p.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static void SetArray<T>(SerializedObject so, string field, T[] items) where T : Object
        {
            var list = new List<Object>();
            foreach (var i in items) list.Add(i);
            SetArray(so, field, list);
        }

        private static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
            var old = es.GetComponent<StandaloneInputModule>();
            if (old != null) Object.DestroyImmediate(old);
            if (es.GetComponent<InputSystemUIInputModule>() == null) es.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
