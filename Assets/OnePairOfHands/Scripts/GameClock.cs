using System;
using UnityEngine;
using UnityEngine.Events;

namespace OnePairOfHands
{
    /// <summary>
    /// Runs the day forward from startHour to endHour. Never counts down.
    /// Fires OnDayEnd once when endHour is reached.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        [SerializeField, Tooltip("Hour the day starts (24h).")]
        private float startHour = 8f;

        [SerializeField, Tooltip("Hour the day ends (24h).")]
        private float endHour = 16f;

        [SerializeField, Min(0.1f), Tooltip("How many real minutes the whole day lasts.")]
        private float realMinutesPerDay = 8f;

        [SerializeField] private int dayNumber = 1;

        [SerializeField, Tooltip("If true the clock stays at the start hour until BeginDay() is called (e.g. by the opening screen).")]
        private bool waitForBegin = false;

        [SerializeField] private UnityEvent onDayEnd = new UnityEvent();

        public event Action OnDayEnd;

        private float elapsedSeconds;

        public int DayNumber => dayNumber;
        public bool IsRunning => !waitForBegin && !HasEnded;
        public float RealMinutesPerDay => realMinutesPerDay;
        public bool HasEnded { get; private set; }
        public float DurationSeconds => realMinutesPerDay * 60f;
        public float Progress01 => Mathf.Clamp01(elapsedSeconds / DurationSeconds);
        public float CurrentHour => Mathf.Lerp(startHour, endHour, Progress01);

        public string TimeString
        {
            get
            {
                float h = CurrentHour;
                int hours = Mathf.FloorToInt(h);
                int minutes = Mathf.FloorToInt((h - hours) * 60f);
                return $"{hours:00}:{minutes:00}";
            }
        }

        private void Update()
        {
            if (HasEnded || waitForBegin) return;

            elapsedSeconds += Time.deltaTime;
            if (elapsedSeconds >= DurationSeconds)
            {
                elapsedSeconds = DurationSeconds;
                HasEnded = true;
                OnDayEnd?.Invoke();
                onDayEnd.Invoke();
            }
        }

        /// <summary>Starts the day if it was waiting for Begin.</summary>
        public void BeginDay() => waitForBegin = false;

        /// <summary>Sets the real-time length of the day. Used by Settings before the day begins.</summary>
        public void SetDayLengthMinutes(float minutes)
        {
            if (minutes > 0.1f) realMinutesPerDay = minutes;
        }

        /// <summary>Debug/testing helper: jump the clock forward (never backwards).</summary>
        public void AdvanceSeconds(float seconds)
        {
            if (seconds > 0f) elapsedSeconds += seconds;
        }
    }
}
