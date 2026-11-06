using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>
    /// Persistent bar along the bottom of every in-game screen: text size (S/M/L), audio on/off, Leave.
    /// Synced with the Settings panel through <see cref="GameSettings"/>.
    /// </summary>
    public class ControlBar : MonoBehaviour
    {
        [SerializeField] private Button[] textSizeButtons = new Button[3];
        [SerializeField] private Button audioButton;
        [SerializeField] private TMP_Text audioLabel;
        [SerializeField] private Button leaveButton;

        public event Action LeavePressed;

        private void Awake()
        {
            for (int i = 0; i < textSizeButtons.Length; i++)
            {
                int idx = i;
                if (textSizeButtons[i] != null) textSizeButtons[i].onClick.AddListener(() => GameSettings.TextSize = idx);
            }
            if (audioButton != null) audioButton.onClick.AddListener(() => GameSettings.AudioOn = !GameSettings.AudioOn);
            if (leaveButton != null) leaveButton.onClick.AddListener(() => LeavePressed?.Invoke());
        }

        private void OnEnable()
        {
            GameSettings.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= Refresh;
        }

        private void Refresh()
        {
            for (int i = 0; i < textSizeButtons.Length; i++) UIPalette.SetSelected(textSizeButtons[i], i == GameSettings.TextSize);
            if (audioLabel != null) audioLabel.text = GameSettings.AudioOn ? "Audio: on" : "Audio: off";
            UIPalette.SetSelected(audioButton, GameSettings.AudioOn);
        }
    }
}
