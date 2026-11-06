using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>
    /// Settings: master volume, text size, day length, fullscreen (desktop only).
    /// Writes straight to <see cref="GameSettings"/> (PlayerPrefs) and refreshes whenever settings
    /// change anywhere (e.g. from the control bar).
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TMP_Text volumeValue;
        [SerializeField] private Button[] textSizeButtons = new Button[3];
        [SerializeField] private Button[] dayLengthButtons = new Button[3];
        [SerializeField] private GameObject fullscreenRow;
        [SerializeField] private Button[] fullscreenButtons = new Button[2]; // 0 = Off, 1 = On
        [SerializeField] private Button[] minimapButtons = new Button[2]; // 0 = Off, 1 = On
        [SerializeField] private Button closeButton;

        public event Action Closed;

        private bool refreshing;
        private bool wired;

        public bool IsOpen => gameObject.activeSelf;

        private void Wire()
        {
            if (wired) return;
            wired = true;
            if (volumeSlider != null)
                volumeSlider.onValueChanged.AddListener(v => { if (!refreshing) GameSettings.MasterVolume = v; });
            for (int i = 0; i < textSizeButtons.Length; i++)
            {
                int idx = i;
                if (textSizeButtons[i] != null) textSizeButtons[i].onClick.AddListener(() => GameSettings.TextSize = idx);
            }
            for (int i = 0; i < dayLengthButtons.Length; i++)
            {
                int idx = i;
                if (dayLengthButtons[i] != null) dayLengthButtons[i].onClick.AddListener(() => GameSettings.DayLength = idx);
            }
            for (int i = 0; i < fullscreenButtons.Length; i++)
            {
                bool on = i == 1;
                if (fullscreenButtons[i] != null) fullscreenButtons[i].onClick.AddListener(() => GameSettings.Fullscreen = on);
            }
            for (int i = 0; i < minimapButtons.Length; i++)
            {
                bool on = i == 1;
                if (minimapButtons[i] != null) minimapButtons[i].onClick.AddListener(() => GameSettings.MinimapOn = on);
            }
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (fullscreenRow != null) fullscreenRow.SetActive(GameSettings.FullscreenSupported);
        }

        private void Awake() => Wire();

        private void OnEnable()
        {
            Wire();
            GameSettings.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= Refresh;
        }

        public void Open() => gameObject.SetActive(true);

        public void Close()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        private void Refresh()
        {
            refreshing = true;
            float v = GameSettings.MasterVolume;
            if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(v);
            if (volumeValue != null) volumeValue.text = $"{Mathf.RoundToInt(v * 100f)}%";
            for (int i = 0; i < textSizeButtons.Length; i++) UIPalette.SetSelected(textSizeButtons[i], i == GameSettings.TextSize);
            for (int i = 0; i < dayLengthButtons.Length; i++) UIPalette.SetSelected(dayLengthButtons[i], i == GameSettings.DayLength);
            for (int i = 0; i < fullscreenButtons.Length; i++) UIPalette.SetSelected(fullscreenButtons[i], (i == 1) == GameSettings.Fullscreen);
            for (int i = 0; i < minimapButtons.Length; i++) UIPalette.SetSelected(minimapButtons[i], (i == 1) == GameSettings.MinimapOn);
            refreshing = false;
        }
    }
}
