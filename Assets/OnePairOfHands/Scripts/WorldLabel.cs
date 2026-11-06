using TMPro;
using UnityEngine;

namespace OnePairOfHands
{
    /// <summary>
    /// Keeps a floating world-space label facing the camera. If the label would sit behind the HUD clock
    /// at the top of the screen (at any zoom), it is nudged down on screen so it stays just below it.
    /// Runs after the follow camera so it uses this frame's camera position.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class WorldLabel : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Tooltip("Gap kept between the label and the bottom of the HUD clock, in screen pixels.")]
        private float clockMarginPixels = 8f;

        private TMP_Text text;
        private RectTransform clock;
        private Vector3 basePosition;
        private bool hasBase;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
            basePosition = transform.position;
            hasBase = true;
        }

        private void LateUpdate()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;
            if (!hasBase) { basePosition = transform.position; hasBase = true; }

            transform.position = basePosition;
            transform.rotation = viewCamera.transform.rotation;
            ClampBelowClock();
        }

        private void ClampBelowClock()
        {
            if (clock == null)
            {
                var go = GameObject.Find("OnePairOfHands/HUD/ClockPanel");
                if (go == null) return;
                clock = (RectTransform)go.transform;
            }
            if (!clock.gameObject.activeInHierarchy) return;
            if (!TryGetScreenRect(out Rect label)) return;

            Rect c = ClockScreenRect();
            float limit = c.yMin - clockMarginPixels;
            bool horizontalOverlap = label.xMax > c.xMin && label.xMin < c.xMax;
            bool partlyOnScreen = label.yMin < Screen.height;
            if (!horizontalOverlap || !partlyOnScreen || label.yMax <= limit) return;

            // Move down along the camera's up axis by the overlap, converted from pixels to world units at this depth.
            float depth = viewCamera.WorldToScreenPoint(basePosition).z;
            float worldPerPixel = viewCamera.orthographic
                ? 2f * viewCamera.orthographicSize / Screen.height
                : 2f * depth * Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            transform.position = basePosition - viewCamera.transform.up * ((label.yMax - limit) * worldPerPixel);
        }

        /// <summary>The HUD clock's rectangle in screen pixels (Screen Space - Overlay canvas).</summary>
        public Rect ClockScreenRect()
        {
            if (clock == null) return Rect.zero;
            var corners = new Vector3[4];
            clock.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        /// <summary>This label's rendered text rectangle in screen pixels (false if behind the camera).</summary>
        public bool TryGetScreenRect(out Rect rect)
        {
            rect = default;
            if (text == null || viewCamera == null) return false;
            Bounds b = text.textBounds;
            if (b.size == Vector3.zero) return false;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, i < 2 ? b.min.y : b.max.y, 0f);
                Vector3 sp = viewCamera.WorldToScreenPoint(transform.TransformPoint(local));
                if (sp.z <= 0f) return false;
                xMin = Mathf.Min(xMin, sp.x); xMax = Mathf.Max(xMax, sp.x);
                yMin = Mathf.Min(yMin, sp.y); yMax = Mathf.Max(yMax, sp.y);
            }
            rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        /// <summary>True if this label is (partly) on screen and overlaps the HUD clock.</summary>
        public bool OverlapsClock()
        {
            if (clock == null || !TryGetScreenRect(out Rect r)) return false;
            if (r.yMin >= Screen.height || r.xMax < 0f || r.xMin > Screen.width) return false;
            return r.Overlaps(ClockScreenRect());
        }
    }
}
