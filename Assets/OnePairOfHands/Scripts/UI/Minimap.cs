using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>
    /// Fixed, north-up minimap. An orthographic top-down camera renders the playable area
    /// (ground, water, bridge, buildings, trees — not UI, labels or Ataa) into a 256x256 RenderTexture.
    /// Ataa is an arrow showing her facing; the four tasks are numbered icons whose fill follows the HUD rule
    /// (held = full, slipping = half, not been = outline). Click/tap an icon to walk there.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        [SerializeField] private Camera mapCamera;
        [SerializeField] private RectTransform panel;
        [SerializeField] private RawImage mapImage;
        [SerializeField] private RectTransform iconArea;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private MinimapIcon[] icons = new MinimapIcon[4];
        [SerializeField] private TMP_Text hoverLabel;
        [SerializeField] private Transform facingSource;
        [SerializeField] private AtaaController ataa;
        [SerializeField] private float landscapeSize = 220f;
        [SerializeField] private float portraitSize = 300f;
        [SerializeField] private Vector2 landscapeOffset = new Vector2(-24f, -112f);
        [SerializeField] private Vector2 portraitOffset = new Vector2(-24f, -112f);

        private RenderTexture rt;

        public bool Visible => panel != null && panel.gameObject.activeSelf;
        public Camera MapCamera => mapCamera;
        public RectTransform Arrow => arrow;
        public MinimapIcon[] Icons => icons;
        public AtaaController Ataa => ataa;

        private void Awake()
        {
            rt = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32) { name = "MinimapRT", antiAliasing = 2 };
            rt.Create();
            mapCamera.targetTexture = rt;
            mapCamera.aspect = 1f;
            mapImage.texture = rt;
            if (ataa == null) ataa = FindFirstObjectByType<AtaaController>();
            foreach (var i in icons) if (i != null) i.Init(this);
            if (hoverLabel != null) hoverLabel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            GameSettings.Changed += ApplySetting;
            ApplySetting();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= ApplySetting;
        }

        private void OnDestroy()
        {
            if (rt != null) rt.Release();
        }

        private void ApplySetting()
        {
            bool on = GameSettings.MinimapOn;
            panel.gameObject.SetActive(on);
            mapCamera.enabled = on;
        }

        /// <summary>World position → anchored position inside the icon area (pivot centre).</summary>
        public Vector2 WorldToMap(Vector3 world)
        {
            Vector3 c = mapCamera.transform.position;
            float size = mapCamera.orthographicSize;
            float u = (world.x - c.x) / (2f * size);
            float v = (world.z - c.z) / (2f * size);
            Rect r = iconArea.rect;
            return new Vector2(u * r.width, v * r.height);
        }

        private void LateUpdate()
        {
            if (!Visible) return;

            bool portrait = Screen.height > Screen.width;
            float s = portrait ? portraitSize : landscapeSize;
            if (!Mathf.Approximately(panel.sizeDelta.x, s)) panel.sizeDelta = new Vector2(s, s);
            panel.anchoredPosition = portrait ? portraitOffset : landscapeOffset;

            if (ataa != null)
            {
                arrow.anchoredPosition = WorldToMap(ataa.transform.position);
                Vector3 f = facingSource != null ? facingSource.forward : ataa.transform.forward;
                float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                arrow.localRotation = Quaternion.Euler(0f, 0f, -yaw); // north-up: screen up = +Z
            }

            var tm = TaskManager.Instance;
            foreach (var i in icons)
                if (i != null) i.Refresh(tm != null && tm.ActiveTask == i.Task);
        }

        public void ShowHover(string text)
        {
            if (hoverLabel == null) return;
            hoverLabel.text = text;
            hoverLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>Walk Ataa to a task, exactly as clicking it in the world does.</summary>
        public bool WalkTo(TaskState task)
        {
            if (task == null || ataa == null || AtaaController.InputBlocked) return false;
            if (TaskManager.Instance != null && TaskManager.Instance.DayOver) return false;
            ataa.MoveTo(task.StandPoint);
            return true;
        }
    }
}
