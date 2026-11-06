using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>
    /// Placeholder HUD: clock, current task panel, "Meanwhile" strip, and the end-of-day panel.
    /// Plain colours only — no red/green/amber, no ticks or crosses.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Serializable]
        public class MeanwhileRow
        {
            public GameObject root;
            public TMP_Text nameText;
            public TMP_Text statusText;
            public Image fill;
        }

        [SerializeField] private TaskManager taskManager;
        [SerializeField] private GameClock clock;

        [Header("Top")]
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private TMP_Text carryText;

        [Header("Current task")]
        [SerializeField] private GameObject taskPanel;
        [SerializeField] private TMP_Text taskNameText;
        [SerializeField] private Image taskFill;
        [SerializeField] private TMP_Text hintText;

        [Header("Meanwhile")]
        [SerializeField] private TMP_Text meanwhileHeader;
        [SerializeField] private RectTransform meanwhilePanel;
        [SerializeField] private float meanwhileRowHeight = 78f;
        [SerializeField] private float meanwhileHeaderHeight = 64f;
        [SerializeField] private MeanwhileRow[] meanwhileRows = new MeanwhileRow[4];

        [Header("End of day")]
        [SerializeField] private GameObject endPanel;
        [SerializeField] private TMP_Text endListText;

        [Header("Plain colours")]
        [SerializeField] private Color fillColour = new Color(0.62f, 0.72f, 0.88f, 1f);
        [SerializeField] private Color heldText = new Color(0.96f, 0.94f, 0.89f, 1f);
        [SerializeField] private Color slippingText = new Color(0.72f, 0.70f, 0.82f, 1f);
        [SerializeField] private Color notBeenText = new Color(0.62f, 0.62f, 0.64f, 1f);

        private void Awake()
        {
            if (taskManager == null) taskManager = FindFirstObjectByType<TaskManager>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (endPanel != null) endPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (taskManager != null) taskManager.OnDayRecorded += ShowEndPanel;
        }

        private void OnDisable()
        {
            if (taskManager != null) taskManager.OnDayRecorded -= ShowEndPanel;
        }

        private void Update()
        {
            if (clock != null && clockText != null)
                clockText.text = $"Day {clock.DayNumber} · {clock.TimeString}";

            if (taskManager == null) return;

            TaskState active = taskManager.ActiveTask;
            UpdateTaskPanel(active);
            UpdateCarry(active);
            UpdateMeanwhile(active);
        }

        private void UpdateTaskPanel(TaskState active)
        {
            if (taskPanel == null) return;
            bool show = active != null;
            if (taskPanel.activeSelf != show) taskPanel.SetActive(show);
            if (!show) return;

            if (taskNameText != null) taskNameText.text = active.NumberedName;
            if (taskFill != null)
            {
                taskFill.fillAmount = active.Value;
                taskFill.color = fillColour;
            }
            if (hintText != null) hintText.text = active.Action != null ? active.Action.Hint : string.Empty;
        }

        private void UpdateCarry(TaskState active)
        {
            if (carryText == null) return;
            var water = taskManager.Water;
            bool show = water != null && water.IsCarrying && (active == null || active.Action != water) && !taskManager.DayOver;
            if (carryText.gameObject.activeSelf != show) carryText.gameObject.SetActive(show);
            if (show)
                carryText.text = $"Carrying water home ({Mathf.RoundToInt(water.JugFill * 100f)}% of a jug)";
        }

        private void UpdateMeanwhile(TaskState active)
        {
            var tasks = taskManager.Tasks;
            int row = 0;
            for (int i = 0; i < tasks.Count && row < meanwhileRows.Length; i++)
            {
                var t = tasks[i];
                if (t == null || t == active) continue;
                FillRow(meanwhileRows[row], t);
                row++;
            }
            int shown = row;
            for (; row < meanwhileRows.Length; row++)
                if (meanwhileRows[row]?.root != null) meanwhileRows[row].root.SetActive(false);
            if (meanwhilePanel != null)
                meanwhilePanel.sizeDelta = new Vector2(meanwhilePanel.sizeDelta.x, meanwhileHeaderHeight + shown * meanwhileRowHeight);
        }

        private void FillRow(MeanwhileRow r, TaskState t)
        {
            if (r == null || r.root == null) return;
            if (!r.root.activeSelf) r.root.SetActive(true);

            if (r.nameText != null) r.nameText.text = t.NumberedName;
            if (r.statusText != null)
            {
                r.statusText.text = t.StatusLabel;
                r.statusText.color = !t.Visited ? notBeenText : t.IsHeld ? heldText : slippingText;
            }
            if (r.fill != null)
            {
                // "Fill density": length and opacity both follow the value.
                r.fill.fillAmount = t.Value;
                Color c = fillColour;
                c.a = Mathf.Lerp(0.25f, 1f, t.Value);
                r.fill.color = c;
            }
        }

        private void ShowEndPanel()
        {
            if (endPanel == null) return;
            var sb = new StringBuilder();
            foreach (var e in taskManager.DayRecord)
                sb.AppendLine($"{e.Task.NumberedName}  —  {(e.Held ? "held" : "slipped")}");
            if (endListText != null) endListText.text = sb.ToString();
            if (taskPanel != null) taskPanel.SetActive(false);
            if (carryText != null) carryText.gameObject.SetActive(false);
            endPanel.SetActive(true);
        }
    }
}
