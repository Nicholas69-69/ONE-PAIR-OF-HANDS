using System;
using System.Collections;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>
    /// Automated walk through the whole UI flow, for checking a built player.
    /// Only runs when launched with  -opohSelfTest  (optionally  -opohShots &lt;folder&gt;).
    /// Presses the real buttons, checks every setting, takes screenshots, restores the player's
    /// settings afterwards and quits. Results go to selftest.txt in the shots folder.
    /// </summary>
    public class FlowSelfTest : MonoBehaviour
    {
        private readonly StringBuilder log = new StringBuilder();
        private string dir;
        private int fails;
        private int passes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-opohSelfTest") < 0) return;
            var go = new GameObject("FlowSelfTest");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<FlowSelfTest>();
            int i = Array.IndexOf(args, "-opohShots");
            t.dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.persistentDataPath, "selftest");
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            // Remember the player's saved settings exactly (including "never set") so the test leaves no trace.
            var saved = new System.Collections.Generic.Dictionary<string, (bool has, int i, float f)>();
            foreach (var k in GameSettings.AllKeys)
                saved[k] = (PlayerPrefs.HasKey(k), PlayerPrefs.GetInt(k, 0), PlayerPrefs.GetFloat(k, 0f));
            bool fsBefore = GameSettings.Fullscreen;
            Check(GameSettings.TextSize == 2 || PlayerPrefs.HasKey("opoh.textSize"), $"Default text size is L (unless the player chose another): {GameSettings.TextSizeLabels[GameSettings.TextSize]}");
            GameSettings.AudioOn = true;
            GameSettings.MinimapOn = true;
            GameSettings.TooltipsSeen = false;

            yield return Run();

            foreach (var kv in saved)
            {
                if (!kv.Value.has) PlayerPrefs.DeleteKey(kv.Key);
                else if (kv.Key == "opoh.masterVolume") PlayerPrefs.SetFloat(kv.Key, kv.Value.f);
                else PlayerPrefs.SetInt(kv.Key, kv.Value.i);
            }
            PlayerPrefs.Save();
            if (GameSettings.Fullscreen != fsBefore || Screen.fullScreen != fsBefore) GameSettings.ApplyDisplay();
            GameSettings.NotifyChanged();

            Log($"RESULT: {passes} passed, {fails} failed -> {(fails == 0 ? "PASS" : "FAIL")}");
            File.WriteAllText(Path.Combine(dir, "selftest.txt"), log.ToString());
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        private IEnumerator Run()
        {
            Log($"Screen {Screen.width}x{Screen.height}, platform {Application.platform}");
            yield return WaitScene("MainMenu");
            yield return Wait(1.5f);
            yield return Shot("01_menu");
            Check(Find("QuitButton") != null, "Quit button visible on desktop");

            // ---------------- Settings
            Click("SettingsButton");
            yield return Wait(0.5f);
            var slider = FindObj<Slider>("VolumeSlider");
            Check(slider != null, "Volume slider present");
            if (slider != null)
            {
                slider.value = 0.3f;
                yield return null;
                Check(Mathf.Abs(AudioListener.volume - 0.3f) < 0.01f, $"Master volume 0.3 -> AudioListener.volume={AudioListener.volume:F2}");
                Check(Mathf.Abs(GameSettings.MasterVolume - 0.3f) < 0.01f, "Master volume saved to PlayerPrefs");
                slider.value = 0.8f;
                yield return null;
            }

            var title = FindText("Title", "Settings");
            var scaler = title != null ? title.GetComponentInParent<TextScaler>() : null;
            foreach (var (btn, scale) in new[] { ("TextSize_L", 1.2f), ("TextSize_S", 0.85f), ("TextSize_M", 1f) })
            {
                Click(btn);
                yield return null;
                if (title != null && scaler != null)
                    Check(Mathf.Abs(title.fontSize - scaler.BaseSizeOf(title) * scale) < 0.05f, $"{btn}: text scaled to x{scale} ({title.fontSize:F1}pt)");
                if (btn == "TextSize_L") { yield return Wait(0.3f); yield return Shot("02_settings_textL"); }
            }

            Click("DayLength_Short");
            yield return null;
            Check(Mathf.Approximately(GameSettings.DayLengthMinutes, 5f), "Day length Short = 5 min saved");

            Click("Fullscreen_On");
            yield return Wait(1.5f);
            Check(Screen.fullScreen, $"Fullscreen on -> Screen.fullScreen={Screen.fullScreen} ({Screen.width}x{Screen.height})");
            yield return Shot("03_settings_fullscreen");
            Click("Fullscreen_Off");
            yield return Wait(1.5f);
            Check(!Screen.fullScreen, $"Fullscreen off -> Screen.fullScreen={Screen.fullScreen} ({Screen.width}x{Screen.height})");
            Click("SettingsCloseButton");
            yield return Wait(0.3f);

            // ---------------- How to play
            Click("HowToPlayButton");
            yield return Wait(0.5f);
            yield return Shot("04_howto");
            Check(FindText("Goal", "There is no winning") != null, "How to Play shows the objective");
            Click("HowToCloseButton");
            yield return Wait(0.3f);

            // ---------------- Opening
            Click("StartButton");
            yield return WaitScene("Level Design");
            yield return Wait(1.5f);
            var clock = FindFirstObjectByType<GameClock>();
            Check(clock != null && !clock.IsRunning, "Clock waits on the opening screen");
            Check(clock != null && clock.TimeString == "08:00", "Clock still 08:00 before Begin");
            yield return Shot("05_opening");

            Click("BeginButton");
            yield return Wait(0.5f);
            Check(clock.IsRunning, "Clock runs after Begin");
            Check(AudioManager.Instance.AmbientPlaying, "Ambient audio started on Begin");
            Check(Mathf.Approximately(clock.DurationSeconds, 300f), $"Day length Short applied (day = {clock.DurationSeconds}s)");

            // ---------------- Tooltips
            var tipText = FindObj<TMP_Text>("Text", "TooltipPanel");
            Check(tipText != null, "First-play tooltip shown");
            yield return Shot("06_tooltip");
            string first = tipText != null ? tipText.text : "";
            Click("TooltipNextButton");
            yield return null;
            Check(tipText != null && tipText.text != first, "Tooltip 2 shown");
            Click("TooltipNextButton");
            yield return null;
            Click("TooltipNextButton"); // 4th: the minimap
            yield return null;
            Check(tipText != null && tipText.text.Contains("map in the corner"), "Tooltip 4 is about the minimap");
            var pointer = FindObj<Image>("Pointer", "TooltipPanel");
            Check(pointer != null, "Tooltip 4 points at the minimap");
            yield return Shot("06b_tooltip_minimap");
            Click("TooltipNextButton");
            yield return null;
            Check(GameSettings.TooltipsSeen && FindObj<TMP_Text>("Text", "TooltipPanel") == null, "Tooltips finished and remembered");

            // ---------------- Control bar
            var clockText = FindText("ClockText", "Day 1");
            var hudScaler = clockText != null ? clockText.GetComponentInParent<TextScaler>() : null;
            Click("Bar_TextL");
            yield return null;
            if (clockText != null && hudScaler != null)
                Check(Mathf.Abs(clockText.fontSize - hudScaler.BaseSizeOf(clockText) * 1.2f) < 0.05f, "Control bar L scales HUD text");
            Check(GameSettings.TextSize == 2, "Control bar text size synced to settings");
            yield return Wait(0.3f);
            yield return Shot("07_hud_textL");
            Click("Bar_TextM");
            Click("AudioToggle");
            yield return null;
            Check(AudioListener.volume == 0f && !GameSettings.AudioOn, "Audio toggle off -> silent");
            Click("AudioToggle");
            yield return null;
            Check(Mathf.Abs(AudioListener.volume - 0.8f) < 0.01f, "Audio toggle on -> master volume restored");

            // ---------------- World labels never sit behind the HUD clock (default, closest and furthest zoom)
            yield return LabelClockChecks();

            // ---------------- Minimap
            var ataa = FindFirstObjectByType<AtaaController>();
            var tm = TaskManager.Instance;
            var map = FindFirstObjectByType<Minimap>();
            var animDriver = FindFirstObjectByType<AtaaAnimatorDriver>();
            var animator = animDriver != null ? animDriver.GetComponent<Animator>() : null;
            Check(map != null && map.Visible && map.MapCamera.enabled, "Minimap visible with its camera on");
            Check(map != null && map.Icons.Length == 4, "Minimap has 4 numbered task icons");
            if (map != null)
            {
                foreach (var icon in map.Icons)
                {
                    // The icon must sit where the map camera actually renders that task.
                    Vector3 vp = map.MapCamera.WorldToViewportPoint(icon.Task.transform.position);
                    var irt = (RectTransform)icon.transform;
                    var area = ((RectTransform)irt.parent).rect;
                    Vector2 iconUv = new Vector2(irt.anchoredPosition.x / area.width + 0.5f, irt.anchoredPosition.y / area.height + 0.5f);
                    Check(Vector2.Distance(iconUv, vp) < 0.01f, $"Icon {icon.Task.Number} lines up with the map render (icon {iconUv:F3} vs camera {(Vector2)vp:F3})");
                }
                Vector2 expect = map.WorldToMap(ataa.transform.position);
                Check(Vector2.Distance(map.Arrow.anchoredPosition, expect) < 1f, "Ataa's arrow is at her position");
                int ruleOk = 0;
                foreach (var icon in map.Icons)
                {
                    float want = !icon.Task.Visited ? 0f : icon.Task.IsHeld ? 1f : 0.5f;
                    if (Mathf.Approximately(icon.FillAmount, want)) ruleOk++;
                }
                Check(ruleOk == 4, "Icon fill follows held/slipping/not been (full/half/outline)");
            }
            Check(animator != null && animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.name == "Ataa_Animator", "Ataa uses Ataa_Animator");
            Check(animator != null && !animator.applyRootMotion, "Root motion off (agent drives movement)");
            Check(animDriver != null && !animDriver.JerrycanVisible, "No jerrycan while not carrying");
            yield return Shot("07b_minimap");

            // ---------------- Walk via the minimap: 4 Work, measuring the walk
            var workIcon = map.Icons[3];
            Check(workIcon.Activate(), "Tapping minimap 4 starts the walk");
            float until = Time.realtimeSinceStartup + 25f;
            float maxSpeedParam = 0f, slipSum = 0f, arrowErr = 0f;
            int slipN = 0;
            Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot), rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 pl = lf.position, pr = rf.position;
            var feet = new[] { lf, rf, animator.GetBoneTransform(HumanBodyBones.LeftToes), animator.GetBoneTransform(HumanBodyBones.RightToes) };
            var feetPrev = new Vector3[4];
            for (int k = 0; k < 4; k++) feetPrev[k] = feet[k].position;
            Vector3 lastVel = Vector3.zero;
            float winT = 0f;
            bool winStraight = true;
            float facingErr = 0f;
            int facingN = 0;
            bool shotWalk = false;
            while (tm.ActiveTask != tm.Tasks[3] && Time.realtimeSinceStartup < until)
            {
                yield return null;
                float sp = animator.GetFloat("Speed");
                maxSpeedParam = Mathf.Max(maxSpeedParam, sp);
                Vector3 v = ataa.Agent.velocity;
                v.y = 0f;
                if (v.magnitude > ataa.Agent.speed * 0.9f && Time.deltaTime > 0f)
                {
                    // The planted foot (slowest of heels/toes) should hardly move over the ground while she
                    // walks straight at full speed; it would slide at ~2 m/s if the clip speed didn't match.
                    // Measured over 40 ms windows so per-frame jitter at high frame rates doesn't dominate.
                    if (winT <= 0f)
                    {
                        for (int k = 0; k < 4; k++) feetPrev[k] = feet[k].position;
                        lastVel = v;
                        winStraight = true;
                    }
                    winT += Time.deltaTime;
                    if (Vector3.Angle(v, lastVel) > 1f) winStraight = false;
                    if (winT >= 0.04f)
                    {
                        float m = float.MaxValue;
                        for (int k = 0; k < 4; k++)
                        {
                            Vector3 dd = feet[k].position - feetPrev[k];
                            dd.y = 0f;
                            m = Mathf.Min(m, dd.magnitude / winT);
                        }
                        if (winStraight)
                        {
                            slipSum += m;
                            slipN++;
                        }
                        winT = 0f;
                    }
                    facingErr += Vector3.Angle(animDriver.transform.forward, v);
                    facingN++;
                    if (!shotWalk && slipN > 30) { shotWalk = true; yield return Shot("08a_walking"); winT = 0f; }
                }
                else winT = 0f;
                arrowErr = Mathf.Max(arrowErr, Vector2.Distance(map.Arrow.anchoredPosition, map.WorldToMap(ataa.transform.position)));
                pl = lf.position;
                pr = rf.position;
            }
            Check(tm.ActiveTask == tm.Tasks[3], "Walked to 4 Work online via the minimap");
            Check(maxSpeedParam > ataa.Agent.speed * 0.85f, $"Animator Speed rises while walking (max {maxSpeedParam:F2}, agent speed {ataa.Agent.speed:F1})");
            float avgSlip = slipN > 0 ? slipSum / slipN : 99f;
            Check(slipN > 10 && avgSlip < 0.5f, $"Planted foot barely slides at full speed (avg {avgSlip:F2} m/s vs agent {ataa.Agent.speed:F1} m/s over {slipN} straight 40 ms windows)");
            Check(facingN > 0 && facingErr / facingN < 20f, $"Faces where she is going (avg {(facingN > 0 ? facingErr / facingN : 0):F1} deg)");
            Check(arrowErr < 1.5f, $"Minimap arrow tracked her the whole walk (max error {arrowErr:F2}px)");
            yield return Wait(0.6f);
            Check(animator.GetFloat("Speed") < 0.1f, $"Animator Speed back to idle on arrival ({animator.GetFloat("Speed"):F2})");
            Check(workIcon.RingVisible, "Current task ring shown on minimap icon 4");
            tm.Tap();
            yield return null;
            yield return null;
            Check(animator.GetBool("Working"), "Animator Working = true while performing the action");
            yield return Wait(0.35f);
            Check(animDriver.WorkWeight > 0.4f, $"Working pose blends in ({animDriver.WorkWeight:F2})");
            yield return Wait(1.2f);
            Check(!animator.GetBool("Working") && animDriver.WorkWeight < 0.05f, "Working pose blends out after the action");

            // Jerrycan follows the carrying flag (set directly here; the real trip is covered in the editor test).
            var water = tm.Water;
            var jug = typeof(WaterTaskAction).GetProperty("JugFill");
            jug.SetValue(water, 1f);
            yield return null;
            Check(animDriver.JerrycanVisible, "Jerrycan shown while carrying");
            jug.SetValue(water, 0f);
            yield return null;
            Check(!animDriver.JerrycanVisible, "Jerrycan hidden when not carrying");

            // ---------------- Play: cook (via minimap 2)
            Check(map.Icons[1].Activate(), "Tapping minimap 2 starts the walk");
            until = Time.realtimeSinceStartup + 20f;
            while (tm.ActiveTask != tm.Tasks[1] && Time.realtimeSinceStartup < until) yield return null;
            Check(tm.ActiveTask == tm.Tasks[1], "Walked to the cooking fire");
            for (int i = 0; i < 12; i++) tm.Tap();
            Check(tm.Tasks[1].IsHeld, $"Cooking raised value to {tm.Tasks[1].Value:F2} (held)");
            yield return Wait(0.5f);
            Check(Mathf.Approximately(map.Icons[1].FillAmount, 1f) && Mathf.Approximately(map.Icons[3].FillAmount, tm.Tasks[3].IsHeld ? 1f : 0.5f), "Minimap fills updated (cook full, work by rule)");
            yield return Shot("08_cook");

            // ---------------- Full well trip, timed: home -> bridge -> well -> home (real seconds, timeScale 1)
            yield return WellTrip(ataa, tm, map, animator);

            // ---------------- Pause (button)
            Click("PauseButton");
            yield return null;
            Check(Time.timeScale == 0f, "Pause sets timeScale 0");
            float p0 = clock.Progress01;
            yield return Wait(1.5f);
            Check(Mathf.Approximately(p0, clock.Progress01), "Clock stopped while paused");
            yield return Shot("09_pause");
            Click("PauseSettingsButton");
            yield return Wait(0.3f);
            Click("Minimap_Off");
            yield return null;
            Check(map != null && !map.Visible && !map.MapCamera.enabled && !GameSettings.MinimapOn, "Settings: Minimap Off hides the map and stops its camera");
            yield return Shot("09b_settings_minimap_off");
            Click("Minimap_On");
            yield return null;
            Check(map != null && map.Visible && map.MapCamera.enabled && GameSettings.MinimapOn, "Settings: Minimap On brings it back");
            Click("SettingsCloseButton");
            yield return null;
            Check(Find("ResumeButton") != null, "Settings from pause returns to pause menu");
            Click("PauseHowToButton");
            yield return Wait(0.3f);
            Click("HowToCloseButton");
            yield return null;
            Click("ResumeButton");
            yield return null;
            Check(Time.timeScale == 1f, "Resume restores time");

            // ---------------- Pause (Esc)
            yield return PressEscape();
            Check(Time.timeScale == 0f, "Esc pauses");
            yield return PressEscape();
            Check(Time.timeScale == 1f, "Esc resumes");

            // ---------------- Leaving screen
            Click("LeaveButton");
            yield return null;
            var leaveText = FindText("ClockText", "still running");
            Check(leaveText != null, "Leaving screen shows 'HH:MM — still running'");
            Check(FindText("Line", "You can stop here. Ataa could not.") != null, "Leaving line shown");
            float l0 = clock.Progress01;
            yield return Wait(2f);
            Check(clock.Progress01 > l0, "Clock keeps running behind the leaving screen");
            var gb = Find("GoBackButton");
            var lv = Find("LeaveConfirmButton");
            if (gb != null && lv != null)
            {
                var a = (RectTransform)gb.transform;
                var b = (RectTransform)lv.transform;
                Check(a.rect.size == b.rect.size && ((Image)gb.targetGraphic).color == ((Image)lv.targetGraphic).color,
                    $"Go back / Leave equal size+style ({a.rect.size} vs {b.rect.size})");
            }
            yield return Shot("10_leave");
            Click("GoBackButton");
            yield return null;
            Check(FindText("Line", "You can stop here") == null, "Go back closes the leaving screen");

            // ---------------- 16:00
            clock.AdvanceSeconds(clock.DurationSeconds);
            yield return Wait(1f);
            var heading = FindText("Heading", "The day");
            Check(heading != null, "End screen shown at 16:00");
            Check(FindText("Closing", "This is a small part of Ataa's reality, not the whole story.") != null, "Closing line exact");
            int rows = 0;
            for (int i = 0; i < 4; i++)
            {
                var st = FindObj<TMP_Text>("State", $"Row_{i}");
                var nm = FindObj<TMP_Text>("Name", $"Row_{i}");
                if (st != null && (st.text == "held" || st.text == "slipped")) rows++;
                if (st != null && nm != null) Log($"   end row: {nm.text} — {st.text}");
            }
            Check(rows == 4, "End screen lists 4 tasks as held/slipped");
            yield return Wait(0.3f);
            yield return Shot("11_end");

            string opened = null;
            GameFlowController.OpenUrlOverride = u => opened = u;
            Click("StoryButton");
            GameFlowController.OpenUrlOverride = null;
            Check(opened == "https://therefugeearchive.substack.com/podcast", $"Story link -> {opened}");

            // ---------------- Play again
            Click("PlayAgainButton");
            yield return Wait(1.5f);
            Check(Find("BeginButton") != null, "Play again reloads the day at the opening screen");
            Click("LeaveButton"); // before Begin: straight to the menu
            yield return WaitScene("MainMenu");
            yield return Wait(1f);
            Check(SceneManager.GetActiveScene().name == "MainMenu", "Back at the main menu");

            // ---------------- And once more through End -> Main Menu
            Click("StartButton");
            yield return WaitScene("Level Design");
            yield return Wait(1f);
            Click("BeginButton");
            yield return null;
            FindFirstObjectByType<GameClock>().AdvanceSeconds(10000f);
            yield return Wait(1f);
            Click("EndMainMenuButton");
            yield return WaitScene("MainMenu");
            yield return Wait(1f);
            Check(SceneManager.GetActiveScene().name == "MainMenu", "End screen Main Menu returns to the menu");
            yield return Shot("12_menu_again");
        }

        // ------------------------------------------------------------- labels vs clock

        private IEnumerator LabelClockChecks()
        {
            var follow = Camera.main.GetComponent<TopDownCameraFollow>();
            var f = typeof(TopDownCameraFollow).GetField("targetDistance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var minF = typeof(TopDownCameraFollow).GetField("minDistance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var maxF = typeof(TopDownCameraFollow).GetField("maxDistance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float start = (float)f.GetValue(follow);
            var labels = FindObjectsByType<WorldLabel>(FindObjectsSortMode.None);
            foreach (var (name, dist) in new[] { ("default", start), ("closest", (float)minF.GetValue(follow)), ("furthest", (float)maxF.GetValue(follow)) })
            {
                f.SetValue(follow, dist);
                yield return Wait(1.2f); // let the smooth zoom settle
                int overlaps = 0, onScreen = 0;
                string which = "";
                foreach (var l in labels)
                {
                    if (l.TryGetScreenRect(out Rect r) && r.yMin < Screen.height && r.yMax > 0f && r.xMax > 0f && r.xMin < Screen.width) onScreen++;
                    if (l.OverlapsClock()) { overlaps++; which += " " + l.GetComponent<TMP_Text>().text; }
                }
                Check(overlaps == 0, $"No world label behind the clock at {name} zoom ({dist:F0} m; {onScreen} labels on screen){which}");
                if (name == "default") yield return Shot("07a_labels_default_zoom");
            }
            f.SetValue(follow, start);
            yield return Wait(1.2f);
        }

        // ------------------------------------------------------------- well trip

        private const float BridgeWestX = -84f, BridgeEastX = -48f;

        private IEnumerator WellTrip(AtaaController ataa, TaskManager tm, Minimap map, Animator animator)
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<TopDownCameraFollow>() : null;
            if (follow != null)
                Log($"   camera distance to Ataa now {Vector3.Distance(Camera.main.transform.position, ataa.transform.position + Vector3.up):F1} m");

            float start = Time.unscaledTime, tBridgeIn = -1f, tBridgeOut = -1f, tArrive = -1f;
            float animSpeedSum = 0f;
            int animSpeedN = 0;
            Check(map.Icons[0].Activate(), "Tapping minimap 1 starts the walk to the well");
            float until = Time.realtimeSinceStartup + 90f;
            while (tm.ActiveTask != tm.Tasks[0] && Time.realtimeSinceStartup < until)
            {
                yield return null;
                float x = ataa.transform.position.x;
                if (tBridgeIn < 0f && x >= BridgeWestX) tBridgeIn = Time.unscaledTime;
                if (tBridgeOut < 0f && x >= BridgeEastX) tBridgeOut = Time.unscaledTime;
                if (ataa.Agent.velocity.magnitude > ataa.Agent.speed * 0.95f)
                {
                    animSpeedSum += animator.GetFloat("AnimSpeed");
                    animSpeedN++;
                }
            }
            tArrive = Time.unscaledTime;
            Check(tm.ActiveTask == tm.Tasks[0] && tBridgeIn > 0f && tBridgeOut > tBridgeIn, "Reached the well over the bridge");
            float avgAnim = animSpeedN > 0 ? animSpeedSum / animSpeedN : 0f;
            Check(avgAnim > 1.4f && avgAnim < 1.7f, $"Walking clip plays at x{avgAnim:F2} at full speed (feet matched to {ataa.Agent.speed:F1} m/s)");

            // Fill the jug (hold), as the player would.
            float fillStart = Time.unscaledTime;
            while (tm.Water.JugFill < 1f && Time.unscaledTime - fillStart < 10f)
            {
                tm.Hold(Time.deltaTime);
                yield return null;
            }
            float fillTime = Time.unscaledTime - fillStart;
            Check(tm.Water.IsCarrying, "Jug filled at the well");
            yield return null;
            Check(FindFirstObjectByType<AtaaAnimatorDriver>().JerrycanVisible, "Jerrycan shown on the walk home");

            float back = Time.unscaledTime, rBridgeIn = -1f, rBridgeOut = -1f, tHome = -1f;
            float waterBefore = tm.Tasks[0].Value;
            ataa.MoveTo(tm.Tasks[1].StandPoint); // home: the cooking fire by the caravans
            var home = FindFirstObjectByType<HomeZone>();
            until = Time.realtimeSinceStartup + 90f;
            while (tm.ActiveTask != tm.Tasks[1] && Time.realtimeSinceStartup < until)
            {
                yield return null;
                float x = ataa.transform.position.x;
                if (rBridgeIn < 0f && x <= BridgeEastX) rBridgeIn = Time.unscaledTime;
                if (rBridgeOut < 0f && x <= BridgeWestX) rBridgeOut = Time.unscaledTime;
                if (tHome < 0f && home != null && home.AtaaIsHome) tHome = Time.unscaledTime;
            }
            float tBack = Time.unscaledTime;
            Check(tm.ActiveTask == tm.Tasks[1] && rBridgeIn > 0f && rBridgeOut > rBridgeIn, "Walked home over the bridge");
            Check(!tm.Water.IsCarrying && tm.Tasks[0].Value > waterBefore, $"Water delivered at home ({waterBefore:F2} -> {tm.Tasks[0].Value:F2})");

            Log($"   WELL TRIP (real seconds, agent {ataa.Agent.speed:F1} m/s):");
            Log($"     home -> bridge (west end)   {tBridgeIn - start,5:F1} s");
            Log($"     across the bridge           {tBridgeOut - tBridgeIn,5:F1} s");
            Log($"     bridge -> well              {tArrive - tBridgeOut,5:F1} s");
            Log($"     filling the jug (hold)      {fillTime,5:F1} s");
            Log($"     well -> bridge (east end)   {rBridgeIn - back,5:F1} s");
            Log($"     across the bridge           {rBridgeOut - rBridgeIn,5:F1} s");
            Log($"     bridge -> home zone         {(tHome > 0 ? tHome - rBridgeOut : -1),5:F1} s   (water counts here)");
            Log($"     home zone -> fire           {(tHome > 0 ? tBack - tHome : -1),5:F1} s");
            Log($"     out {tArrive - start:F1} s, back {tBack - back:F1} s, round trip incl. filling {tBack - start:F1} s");
        }

        // ------------------------------------------------------------- helpers

        private IEnumerator PressEscape()
        {
            var kb = Keyboard.current;
            if (kb == null) { Check(false, "No keyboard device"); yield break; }
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
            yield return null;
        }

        private IEnumerator WaitScene(string name)
        {
            float until = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < until) yield return null;
            Check(SceneManager.GetActiveScene().name == name, $"Scene '{name}' loaded");
        }

        private static WaitForSecondsRealtime Wait(float s) => new WaitForSecondsRealtime(s);

        private static Button Find(string name)
        {
            foreach (var b in FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.name == name && b.isActiveAndEnabled) return b;
            return null;
        }

        private static T FindObj<T>(string name, string parentName = null) where T : Component
        {
            foreach (var c in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (c.name != name || !c.gameObject.activeInHierarchy) continue;
                if (parentName == null) return c;
                for (var p = c.transform.parent; p != null; p = p.parent)
                    if (p.name == parentName) return c;
            }
            return null;
        }

        private static TMP_Text FindText(string name, string contains)
        {
            foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                if (t.name == name && t.gameObject.activeInHierarchy && t.text.Contains(contains)) return t;
            return null;
        }

        private void Click(string name)
        {
            var b = Find(name);
            if (b == null) { Check(false, $"Button '{name}' not found or not active"); return; }
            b.onClick.Invoke();
            Log($"   click {name}");
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            Log($"   screenshot {name}.png");
            yield return null;
            yield return null;
        }

        private void Check(bool ok, string what)
        {
            if (ok) passes++; else fails++;
            Log($"{(ok ? "PASS" : "FAIL")}  {what}");
        }

        private void Log(string s)
        {
            log.AppendLine(s);
            Debug.Log("[SelfTest] " + s);
        }
    }
}
