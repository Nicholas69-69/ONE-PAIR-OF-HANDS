using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>
    /// A numbered task icon on the minimap. Status is shown only by fill (held = full, slipping = half,
    /// not been = outline) — plain colours, no ticks or crosses. A subtle ring marks the task Ataa is at.
    /// Click/tap to walk there; hover shows the task name.
    /// </summary>
    public class MinimapIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private TaskState task;
        [SerializeField] private Image fill;
        [SerializeField] private Image currentRing;

        private Minimap map;

        public TaskState Task => task;
        public float FillAmount => fill != null ? fill.fillAmount : 0f;
        public bool RingVisible => currentRing != null && currentRing.enabled;

        public void Init(Minimap owner)
        {
            map = owner;
            if (currentRing != null) currentRing.enabled = false;
        }

        public void Refresh(bool isCurrent)
        {
            if (task == null) return;
            ((RectTransform)transform).anchoredPosition = map.WorldToMap(task.transform.position);
            if (fill != null) fill.fillAmount = !task.Visited ? 0f : task.IsHeld ? 1f : 0.5f;
            if (currentRing != null)
            {
                currentRing.enabled = isCurrent;
                if (isCurrent)
                {
                    var c = currentRing.color;
                    c.a = 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);
                    currentRing.color = c;
                }
            }
        }

        public void OnPointerEnter(PointerEventData e) => map?.ShowHover(task != null ? task.NumberedName : "");
        public void OnPointerExit(PointerEventData e) => map?.ShowHover(null);
        public void OnPointerClick(PointerEventData e) => Activate();

        /// <summary>Walk Ataa to this task (same NavMesh move as clicking it in the world).</summary>
        public bool Activate()
        {
            if (map == null) return false;
            AudioManager.Instance.Click();
            return map.WalkTo(task);
        }
    }
}
