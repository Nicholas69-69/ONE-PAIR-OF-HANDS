using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>
    /// Scripted demo recording for a built player. Only runs when launched with  -opohDemo -opohFrames &lt;folder&gt;.
    /// Locks the game to 30 fps of game time per rendered frame (Time.captureFramerate), saves every frame
    /// as a JPG, drives the real UI/gameplay, then restores the player's settings and quits.
    /// Writes demo.txt (frame markers + click frames) next to the frames for the audio mix.
    /// </summary>
    public class DemoRecorder : MonoBehaviour
    {
        private const int Fps = 30;
        private const int W = 1920, H = 1080;

        private string dir;
        private bool recording;
        private int frame;
        private readonly StringBuilder log = new StringBuilder();
        private readonly List<int> clickFrames = new List<int>();

        private RectTransform cursor;
        private Image cursorImg, cursorRing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-opohDemo") < 0) return;
            var go = new GameObject("DemoRecorder");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<DemoRecorder>();
            int i = Array.IndexOf(args, "-opohFrames");
            r.dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.persistentDataPath, "demo");
        }

        // ------------------------------------------------------------------ main

        private IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "f_*.jpg")) File.Delete(f);

            var saved = new Dictionary<string, (bool has, int i, float f)>();
            foreach (var k in GameSettings.AllKeys)
                saved[k] = (PlayerPrefs.HasKey(k), PlayerPrefs.GetInt(k, 0), PlayerPrefs.GetFloat(k, 0f));

            // Known starting state: Normal day (switched to Short on camera), text M, volume 80%.
            PlayerPrefs.SetInt("opoh.fullscreen", 0);
            GameSettings.DayLength = 1;
            GameSettings.TextSize = 1;
            GameSettings.MasterVolume = 0.8f;
            GameSettings.AudioOn = true;
            GameSettings.MinimapOn = true;
            GameSettings.TooltipsSeen = false;
            GameSettings.NotifyChanged();
            Screen.SetResolution(W, H, FullScreenMode.Windowed);
            Cursor.visible = false;
            for (int i = 0; i < 60 && (Screen.width != W || Screen.height != H); i++) yield return null;
            Log($"Screen {Screen.width}x{Screen.height}");

            BuildCursor();
            Time.captureFramerate = Fps;
            yield return null;
            StartCoroutine(CaptureLoop());
            recording = true;

            yield return Script();

            recording = false;
            Time.captureFramerate = 0;
            Log($"FRAMES {frame}");
            Log("CLICKS " + string.Join(",", clickFrames));

            foreach (var kv in saved)
            {
                if (!kv.Value.has) PlayerPrefs.DeleteKey(kv.Key);
                else if (kv.Key == "opoh.masterVolume") PlayerPrefs.SetFloat(kv.Key, kv.Value.f);
                else PlayerPrefs.SetInt(kv.Key, kv.Value.i);
            }
            PlayerPrefs.Save();
            File.WriteAllText(Path.Combine(dir, "demo.txt"), log.ToString());
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }

        private IEnumerator CaptureLoop()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (!recording) continue;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(dir, $"f_{frame:D6}.jpg"), tex.EncodeToJPG(93));
                Destroy(tex);
                frame++;
            }
        }

        // ------------------------------------------------------------------ the script

        private IEnumerator Script()
        {
            // 1. Main menu, Settings, How to Play
            yield return WaitScene("MainMenu");
            Mark("menu");
            yield return Hold(3f);

            int s0 = frame;
            yield return ClickButton("SettingsButton");
            Mark("settings");
            yield return Hold(0.8f);
            yield return DragSlider("VolumeSlider", 0.8f, 0.55f, 1.6f);
            yield return Hold(0.6f);
            yield return ClickButton("TextSize_L");
            yield return Hold(1.2f);
            yield return ClickButton("DayLength_Short");
            yield return HoldUntil(s0, 8f);
            yield return ClickButton("SettingsCloseButton");
            yield return Hold(0.5f);

            int h0 = frame;
            yield return ClickButton("HowToPlayButton");
            Mark("howto");
            HideCursor();
            yield return HoldUntil(h0, 8f);
            yield return ClickButton("HowToCloseButton");
            yield return Hold(0.8f);

            // 2. Start -> opening -> Begin
            yield return ClickButton("StartButton");
            yield return WaitScene("Level Design");
            Mark("opening");
            HideCursor();
            yield return Hold(5.4f);
            yield return ClickButton("BeginButton");
            Mark("begin");

            var clock = FindFirstObjectByType<GameClock>();
            var tm = TaskManager.Instance;
            var ataa = FindFirstObjectByType<AtaaController>();
            var map = FindFirstObjectByType<Minimap>();
            var home = FindFirstObjectByType<HomeZone>();
            TaskState water = null, cook = null, teach = null, work = null;
            foreach (var t in tm.Tasks)
            {
                if (t.Action is WaterTaskAction) water = t;
                else if (t.Action is CookTaskAction) cook = t;
                else if (t.Action is TeachTaskAction) teach = t;
                else if (t.Action is WorkTaskAction) work = t;
            }
            Log($"Day length {clock.DurationSeconds}s");

            // 3. Tooltips shown, then skipped
            yield return Hold(2.6f);
            yield return ClickButton("TooltipNextButton");
            yield return Hold(2.6f);
            yield return ClickButton("TooltipNextButton");
            yield return Hold(2.6f);
            yield return ClickButton("TooltipSkipButton");
            Mark("tooltips_skipped");
            yield return Hold(0.6f);

            // 4. Fire (cook) -> work caravan -> tent (teach)
            yield return WalkTo(ataa, tm, cook);
            Mark("cook");
            yield return Work(tm, ataa, cook, 8f, 0.35f);
            yield return WalkTo(ataa, tm, work);
            Mark("work");
            yield return Work(tm, ataa, work, 8f, 0.35f);
            yield return WalkTo(ataa, tm, teach);
            Mark("teach");
            yield return Work(tm, ataa, teach, 9f, 0.9f);

            // 5. Minimap 1 -> well over the bridge, fill, carry home
            int iconIdx = 0;
            for (int i = 0; i < map.Icons.Length; i++) if (map.Icons[i].Task == water) iconIdx = i;
            var icon = map.Icons[iconIdx];
            yield return GlideTo(((RectTransform)icon.transform).position, 0.6f);
            Pulse();
            clickFrames.Add(frame);
            icon.Activate();
            Mark("well_walk");
            yield return Hold(0.4f);
            HideCursor();
            yield return WaitArrive(tm, water, 120f);
            Mark("well_fill");
            yield return Hold(0.6f);
            yield return GlideTo(AtaaScreen(ataa), 0.4f);
            while (tm.Water.JugFill < 1f && !tm.DayOver)
            {
                tm.Hold(Time.deltaTime);
                cursorRing.color = new Color(1f, 1f, 1f, 0.9f);
                FollowAtaa(ataa);
                yield return null;
            }
            cursorRing.color = new Color(1f, 1f, 1f, 0f);
            yield return Hold(1f);
            Mark("carry_home");
            float before = water.Value;
            yield return ClickWorld(cook.StandPoint);
            ataa.MoveTo(cook.StandPoint);
            HideCursor();
            float delivered = -1f;
            int until = frame + 120 * Fps;
            while (tm.ActiveTask != cook && frame < until && !tm.DayOver)
            {
                if (delivered < 0f && home != null && !tm.Water.IsCarrying) { delivered = water.Value; Mark("delivered"); }
                yield return null;
            }
            Log($"Water {before:F2} -> {water.Value:F2}");
            yield return Hold(2.5f);

            // 6. Leave -> 5s -> Go back
            yield return ClickButton("LeaveButton");
            Mark("leave");
            HideCursor();
            yield return Hold(5f);
            yield return ClickButton("GoBackButton");
            yield return Hold(1f);

            // 7. Pause briefly
            yield return ClickButton("PauseButton");
            Mark("pause");
            yield return Hold(3f);
            yield return ClickButton("ResumeButton");
            yield return Hold(0.5f);

            // 8. Keep her busy until 16:00
            var loop = new[] { cook, work, teach, cook, water };
            int n = 0;
            while (!clock.HasEnded)
            {
                var t = loop[n++ % 4]; // the well trip is only shown once
                yield return WalkTo(ataa, tm, t);
                yield return Work(tm, ataa, t, t == teach ? 10f : 9f, t == teach ? 0.9f : 0.35f);
            }
            HideCursor();
            Mark("end");
            yield return Hold(10f);
            Mark("stop");
        }

        // ------------------------------------------------------------------ gameplay helpers

        private IEnumerator WalkTo(AtaaController ataa, TaskManager tm, TaskState t)
        {
            if (tm.DayOver) yield break;
            if (tm.ActiveTask == t) yield break;
            yield return ClickWorld(t.StandPoint);
            ataa.MoveTo(t.StandPoint);
            HideCursor();
            yield return WaitArrive(tm, t, 90f);
            yield return Hold(0.4f);
        }

        private IEnumerator WaitArrive(TaskManager tm, TaskState t, float timeout)
        {
            int until = frame + Mathf.RoundToInt(timeout * Fps);
            while (tm.ActiveTask != t && frame < until && !tm.DayOver) yield return null;
        }

        private IEnumerator Work(TaskManager tm, AtaaController ataa, TaskState t, float seconds, float interval)
        {
            if (tm.DayOver) yield break;
            yield return GlideTo(AtaaScreen(ataa), 0.4f);
            int end = frame + Mathf.RoundToInt(seconds * Fps);
            int step = Mathf.Max(1, Mathf.RoundToInt(interval * Fps));
            int next = frame;
            while (frame < end && !tm.DayOver)
            {
                if (frame >= next && tm.ActiveTask == t)
                {
                    tm.Tap();
                    Pulse();
                    next = frame + step;
                }
                FollowAtaa(ataa);
                yield return null;
            }
        }

        private IEnumerator ClickWorld(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) yield break;
            sp.x = Mathf.Clamp(sp.x, 40f, Screen.width - 40f);
            sp.y = Mathf.Clamp(sp.y, 40f, Screen.height - 40f);
            yield return GlideTo(sp, 0.5f);
            Pulse();
            yield return Hold(0.15f);
        }

        private static Vector3 AtaaScreen(AtaaController ataa)
        {
            var cam = Camera.main;
            return cam != null ? cam.WorldToScreenPoint(ataa.transform.position + Vector3.up * 0.9f) : new Vector3(W / 2f, H / 2f);
        }

        private void FollowAtaa(AtaaController ataa)
        {
            var target = AtaaScreen(ataa);
            cursor.position = Vector3.Lerp(cursor.position, new Vector3(target.x, target.y, 0f), 0.25f);
        }

        // ------------------------------------------------------------------ UI helpers

        private IEnumerator ClickButton(string name)
        {
            var b = FindButton(name);
            if (b == null) { Log($"MISSING button {name}"); yield break; }
            yield return GlideTo(((RectTransform)b.transform).position, 0.5f);
            var es = EventSystem.current;
            if (es != null) ExecuteEvents.Execute(b.gameObject, new PointerEventData(es), ExecuteEvents.pointerEnterHandler);
            yield return Hold(0.15f);
            Pulse();
            clickFrames.Add(frame);
            if (es != null) ExecuteEvents.Execute(b.gameObject, new PointerEventData(es), ExecuteEvents.pointerExitHandler);
            b.onClick.Invoke();
            Log($"click {name} @ {frame}");
            yield return null;
        }

        private IEnumerator DragSlider(string name, float from, float to, float seconds)
        {
            Slider s = null;
            foreach (var c in FindObjectsByType<Slider>(FindObjectsSortMode.None))
                if (c.name == name && c.isActiveAndEnabled) s = c;
            if (s == null) { Log($"MISSING slider {name}"); yield break; }
            s.value = from;
            Vector3 Handle() => s.handleRect != null ? s.handleRect.position : s.transform.position;
            yield return GlideTo(Handle(), 0.5f);
            Pulse();
            clickFrames.Add(frame);
            int n = Mathf.RoundToInt(seconds * Fps);
            for (int i = 1; i <= n; i++)
            {
                s.value = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, i / (float)n));
                cursor.position = Handle();
                cursorRing.color = new Color(1f, 1f, 1f, 0.8f);
                yield return null;
            }
            cursorRing.color = new Color(1f, 1f, 1f, 0f);
        }

        private static Button FindButton(string name)
        {
            foreach (var b in FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.name == name && b.isActiveAndEnabled) return b;
            return null;
        }

        private IEnumerator WaitScene(string name)
        {
            int until = frame + 20 * Fps;
            while (SceneManager.GetActiveScene().name != name && frame < until) yield return null;
            yield return null;
        }

        private IEnumerator Hold(float seconds)
        {
            int n = Mathf.RoundToInt(seconds * Fps);
            for (int i = 0; i < n; i++) yield return null;
        }

        private IEnumerator HoldUntil(int startFrame, float seconds)
        {
            int end = startFrame + Mathf.RoundToInt(seconds * Fps);
            while (frame < end) yield return null;
        }

        private void Mark(string what) => Log($"MARK {what} {frame}");

        private void Log(string s)
        {
            log.AppendLine(s);
            Debug.Log("[Demo] " + s);
        }

        // ------------------------------------------------------------------ on-screen pointer

        private void BuildCursor()
        {
            var go = new GameObject("DemoCursorCanvas");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;

            var dot = MakeCircle(64, 0.36f, 0.5f);
            var ring = MakeCircle(128, 0.42f, 0.5f, true);

            var c = new GameObject("Cursor", typeof(RectTransform));
            c.transform.SetParent(go.transform, false);
            cursor = (RectTransform)c.transform;
            cursor.sizeDelta = new Vector2(34f, 34f);
            cursorImg = c.AddComponent<Image>();
            cursorImg.sprite = dot;
            cursorImg.raycastTarget = false;
            cursorImg.color = new Color(1f, 1f, 1f, 0f);

            var r = new GameObject("Ring", typeof(RectTransform));
            r.transform.SetParent(cursor, false);
            var rrt = (RectTransform)r.transform;
            rrt.sizeDelta = new Vector2(64f, 64f);
            cursorRing = r.AddComponent<Image>();
            cursorRing.sprite = ring;
            cursorRing.raycastTarget = false;
            cursorRing.color = new Color(1f, 1f, 1f, 0f);
            cursor.position = new Vector3(W * 0.5f, H * 0.4f, 0f);
        }

        private static Sprite MakeCircle(int size, float radius, float soft, bool ringOnly = false)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a;
                byte v = 255;
                if (ringOnly)
                {
                    a = Mathf.Clamp01(1f - Mathf.Abs(d - radius) / 0.035f);
                }
                else
                {
                    a = Mathf.Clamp01((radius - d) / 0.03f);
                    float edge = Mathf.Clamp01((d - (radius - 0.09f)) / 0.04f);
                    v = (byte)Mathf.Lerp(250, 40, edge); // light fill, dark rim: visible on any background
                    a *= 0.85f;
                }
                px[y * size + x] = new Color32(v, v, v, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private IEnumerator GlideTo(Vector3 target, float seconds)
        {
            target.z = 0f;
            if (cursorImg.color.a < 0.5f)
            {
                // Appear near the target rather than sliding across the whole screen.
                cursor.position = target + new Vector3(90f, -70f, 0f);
                cursorImg.color = Color.white;
            }
            Vector3 from = cursor.position;
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Fps));
            for (int i = 1; i <= n; i++)
            {
                cursor.position = Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, i / (float)n));
                yield return null;
            }
        }

        private Coroutine pulse;

        private void Pulse()
        {
            if (pulse != null) StopCoroutine(pulse);
            pulse = StartCoroutine(PulseRoutine());
        }

        private IEnumerator PulseRoutine()
        {
            var rt = (RectTransform)cursorRing.transform;
            int n = Mathf.RoundToInt(0.35f * Fps);
            for (int i = 0; i <= n; i++)
            {
                float k = i / (float)n;
                rt.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.3f, k);
                cursorRing.color = new Color(1f, 1f, 1f, 0.9f * (1f - k));
                cursor.localScale = Vector3.one * (k < 0.3f ? 0.85f : 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
            cursor.localScale = Vector3.one;
            pulse = null;
        }

        private void HideCursor() => cursorImg.color = new Color(1f, 1f, 1f, 0f);
    }
}
