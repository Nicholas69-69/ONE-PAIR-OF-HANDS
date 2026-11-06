using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>
    /// In-game UI flow around the existing gameplay: opening screen (clock waits for Begin),
    /// first-play tooltips, pause menu (the only place time stops), leaving screen (the clock keeps
    /// running), and the end screen at 16:00.
    /// </summary>
    public class GameFlowController : MonoBehaviour
    {
        public const string StoryUrl = "https://therefugeearchive.substack.com/podcast";

        /// <summary>Test hook: if set, the story link calls this instead of opening a browser.</summary>
        public static Action<string> OpenUrlOverride;

        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private GameClock clock;
        [SerializeField] private TaskManager taskManager;

        [Header("Opening")]
        [SerializeField] private GameObject openingPanel;
        [SerializeField] private Button beginButton;

        [Header("Pause")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseHowToButton;
        [SerializeField] private Button pauseSettingsButton;
        [SerializeField] private Button pauseMainMenuButton;

        [Header("Shared panels")]
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private GameObject howToPanel;
        [SerializeField] private Button howToCloseButton;
        [SerializeField] private ControlBar controlBar;

        [Header("Leaving")]
        [SerializeField] private GameObject leavePanel;
        [SerializeField] private TMP_Text leaveClockText;
        [SerializeField] private Button goBackButton;
        [SerializeField] private Button leaveConfirmButton;

        [Header("End of day")]
        [SerializeField] private GameObject endPanel;
        [SerializeField] private TMP_Text[] endRowNames = new TMP_Text[4];
        [SerializeField] private TMP_Text[] endRowStates = new TMP_Text[4];
        [SerializeField] private Button storyButton;
        [SerializeField] private Button endMainMenuButton;
        [SerializeField] private Button playAgainButton;

        [Header("First-play tooltips")]
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private TMP_Text tooltipText;
        [SerializeField] private TMP_Text tooltipCounter;
        [SerializeField] private Button tooltipNextButton;
        [SerializeField] private Button tooltipSkipButton;
        [SerializeField, TextArea] private string[] tooltips =
        {
            "Click or tap the ground to walk.",
            "Walk to a numbered place, then click or tap it to work there. At the well, hold to fill the jug.",
            "The Meanwhile strip (top left) shows what is slipping while you are somewhere else.",
            "The map in the corner shows where each task is. Tap a number to walk there.",
        };
        [SerializeField, Tooltip("Index of the tooltip that points at the minimap.")]
        private int minimapTooltipIndex = 3;
        [SerializeField] private RectTransform minimapPanel;
        [SerializeField] private GameObject tooltipPointer;

        public bool Begun { get; private set; }
        public bool Paused { get; private set; }
        public bool Leaving { get; private set; }
        public bool Ended { get; private set; }
        public bool TooltipShowing => tooltipPanel != null && tooltipPanel.activeSelf;

        private int tooltipIndex;
        private Vector2 tipAnchorMin, tipAnchorMax, tipPivot, tipPos;
        private float tipWidth;
        private bool subFromPause;

        private void Awake()
        {
            Time.timeScale = 1f;
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (taskManager == null) taskManager = FindFirstObjectByType<TaskManager>();

            beginButton.onClick.AddListener(Begin);
            pauseButton.onClick.AddListener(Pause);
            resumeButton.onClick.AddListener(Resume);
            pauseHowToButton.onClick.AddListener(() => OpenSub(howToPanel));
            pauseSettingsButton.onClick.AddListener(() => OpenSub(settingsPanel.gameObject));
            pauseMainMenuButton.onClick.AddListener(GoToMainMenu);
            howToCloseButton.onClick.AddListener(CloseSub);
            settingsPanel.Closed += CloseSub;
            controlBar.LeavePressed += OpenLeave;
            goBackButton.onClick.AddListener(GoBack);
            leaveConfirmButton.onClick.AddListener(GoToMainMenu);
            storyButton.onClick.AddListener(OpenStory);
            endMainMenuButton.onClick.AddListener(GoToMainMenu);
            playAgainButton.onClick.AddListener(PlayAgain);
            tooltipNextButton.onClick.AddListener(NextTooltip);
            tooltipSkipButton.onClick.AddListener(CloseTooltips);

            var tipRt = (RectTransform)tooltipPanel.transform;
            tipAnchorMin = tipRt.anchorMin;
            tipAnchorMax = tipRt.anchorMax;
            tipPivot = tipRt.pivot;
            tipPos = tipRt.anchoredPosition;

            openingPanel.SetActive(true);
            pausePanel.SetActive(false);
            leavePanel.SetActive(false);
            endPanel.SetActive(false);
            tooltipPanel.SetActive(false);
            howToPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (clock != null) clock.OnDayEnd += ShowEnd;
        }

        private void OnDisable()
        {
            if (clock != null) clock.OnDayEnd -= ShowEnd;
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            AtaaController.InputBlocked = false;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) HandleEscape();

            if (Leaving && leaveClockText != null && clock != null)
                leaveClockText.text = $"{clock.TimeString} — still running";

            pauseButton.gameObject.SetActive(Begun && !Ended && !Paused && !Leaving);

            AtaaController.InputBlocked = !Begun || Paused || Leaving || Ended || SubOpen;
        }

        private bool SubOpen => howToPanel.activeSelf || settingsPanel.gameObject.activeSelf;

        private void HandleEscape()
        {
            if (SubOpen) { CloseSub(); return; }
            if (!Begun || Ended) return;
            if (Leaving) { GoBack(); return; }
            if (Paused) Resume(); else Pause();
        }

        // ---------------------------------------------------------------- opening

        public void Begin()
        {
            if (Begun) return;
            Begun = true;
            openingPanel.SetActive(false);
            if (clock != null)
            {
                clock.SetDayLengthMinutes(GameSettings.DayLengthMinutes);
                clock.BeginDay();
            }
            AudioManager.Instance.StartAmbient();
            if (!GameSettings.TooltipsSeen) ShowTooltip(0);
        }

        // ---------------------------------------------------------------- tooltips

        private void ShowTooltip(int index)
        {
            tooltipIndex = index;
            tooltipPanel.SetActive(true);
            tooltipText.text = tooltips[index];
            PlaceTooltip(index == minimapTooltipIndex && minimapPanel != null && minimapPanel.gameObject.activeInHierarchy);
            if (tooltipCounter != null) tooltipCounter.text = $"{index + 1} / {tooltips.Length}";
            var label = tooltipNextButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = index == tooltips.Length - 1 ? "Got it" : "Next";
        }

        /// <summary>The minimap tooltip sits right under the map with a small pointer; the others sit above the task panel.</summary>
        private void PlaceTooltip(bool atMinimap)
        {
            var rt = (RectTransform)tooltipPanel.transform;
            if (tipWidth <= 0f) tipWidth = rt.sizeDelta.x;
            // In portrait the Meanwhile strip shares the top of the screen, so keep this tip in the right column.
            bool narrow = atMinimap && Screen.height > Screen.width;
            rt.sizeDelta = new Vector2(narrow ? 500f : tipWidth, rt.sizeDelta.y);
            if (atMinimap)
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(minimapPanel.anchoredPosition.x,
                    minimapPanel.anchoredPosition.y - minimapPanel.rect.height - 22f);
            }
            else
            {
                rt.anchorMin = tipAnchorMin;
                rt.anchorMax = tipAnchorMax;
                rt.pivot = tipPivot;
                rt.anchoredPosition = tipPos;
            }
            if (tooltipPointer != null)
            {
                tooltipPointer.SetActive(atMinimap);
                if (atMinimap)
                    ((RectTransform)tooltipPointer.transform).anchoredPosition = new Vector2(-minimapPanel.rect.width * 0.5f, 0f);
            }
        }

        private void NextTooltip()
        {
            if (tooltipIndex + 1 < tooltips.Length) ShowTooltip(tooltipIndex + 1);
            else CloseTooltips();
        }

        private void CloseTooltips()
        {
            tooltipPanel.SetActive(false);
            GameSettings.TooltipsSeen = true;
        }

        // ---------------------------------------------------------------- pause

        public void Pause()
        {
            if (!Begun || Ended || Leaving) return;
            Paused = true;
            Time.timeScale = 0f; // the only place the clock stops
            pausePanel.SetActive(true);
        }

        public void Resume()
        {
            CloseSubSilently();
            Paused = false;
            Time.timeScale = 1f;
            pausePanel.SetActive(false);
        }

        private void OpenSub(GameObject panel)
        {
            subFromPause = Paused;
            howToPanel.SetActive(panel == howToPanel);
            settingsPanel.gameObject.SetActive(panel == settingsPanel.gameObject);
            pausePanel.SetActive(false);
        }

        private void CloseSub()
        {
            CloseSubSilently();
            if (subFromPause && Paused) pausePanel.SetActive(true);
        }

        private void CloseSubSilently()
        {
            if (howToPanel.activeSelf) howToPanel.SetActive(false);
            if (settingsPanel.gameObject.activeSelf) settingsPanel.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- leaving

        public void OpenLeave()
        {
            // Before the day begins, or after it has ended, nothing is running: just leave.
            if (Ended || !Begun) { GoToMainMenu(); return; }
            if (Paused) Resume(); // the clock is still running behind the leaving screen
            CloseSubSilently();
            Leaving = true;
            if (leaveClockText != null && clock != null) leaveClockText.text = $"{clock.TimeString} — still running";
            leavePanel.SetActive(true);
        }

        public void GoBack()
        {
            Leaving = false;
            leavePanel.SetActive(false);
        }

        // ---------------------------------------------------------------- end

        private void ShowEnd()
        {
            Ended = true;
            Paused = false;
            Leaving = false;
            Time.timeScale = 1f;
            CloseSubSilently();
            pausePanel.SetActive(false);
            leavePanel.SetActive(false);
            tooltipPanel.SetActive(false);

            if (taskManager != null)
            {
                // TaskManager records on the same OnDayEnd event; read live values to be order-independent.
                var tasks = taskManager.Tasks;
                for (int i = 0; i < endRowNames.Length && i < tasks.Count; i++)
                {
                    if (endRowNames[i] != null) endRowNames[i].text = tasks[i].DisplayName;
                    if (endRowStates[i] != null) endRowStates[i].text = tasks[i].IsHeld ? "held" : "slipped";
                }
            }
            endPanel.SetActive(true);
        }

        private void OpenStory()
        {
            if (OpenUrlOverride != null) OpenUrlOverride(StoryUrl);
            else Application.OpenURL(StoryUrl);
        }

        public void PlayAgain()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void GoToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuScene);
        }
    }
}
