using System;
using System.Collections.Generic;
using UnityEngine;

namespace OnePairOfHands
{
    /// <summary>
    /// Tracks all four tasks, which one is active (one pair of hands), drains the rest,
    /// routes action input, and keeps the end-of-day record.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class TaskManager : MonoBehaviour
    {
        public static TaskManager Instance { get; private set; }

        [SerializeField] private TaskState[] tasks = new TaskState[4];
        [SerializeField] private GameClock clock;
        [SerializeField] private AtaaController ataa;

        public struct DayRecordEntry
        {
            public TaskState Task;
            public bool Held;
        }

        public IReadOnlyList<TaskState> Tasks => tasks;
        public GameClock Clock => clock;

        /// <summary>The task zone Ataa is standing in (null if none).</summary>
        public TaskState CurrentZone { get; private set; }

        /// <summary>The one task being done right now. Walking counts as no task.</summary>
        public TaskState ActiveTask => DayOver || CurrentZone == null || (ataa != null && ataa.IsMoving) ? null : CurrentZone;

        public bool DayOver { get; private set; }

        /// <summary>Time of the last tap/hold that reached a task (visual feedback only).</summary>
        public float LastActionTime { get; private set; } = -999f;

        /// <summary>True while a task action is being performed (a tap within the last moment, or a hold).</summary>
        public bool IsWorking => ActiveTask != null && Time.time - LastActionTime < 0.6f;
        public List<DayRecordEntry> DayRecord { get; } = new List<DayRecordEntry>();

        public event Action<TaskState> OnActiveTaskChanged;
        public event Action OnDayRecorded;

        private TaskState lastActive;

        public WaterTaskAction Water
        {
            get
            {
                foreach (var t in tasks)
                    if (t != null && t.Action is WaterTaskAction w) return w;
                return null;
            }
        }

        private void Awake()
        {
            Instance = this;
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (ataa == null) ataa = FindFirstObjectByType<AtaaController>();
        }

        private void OnEnable()
        {
            if (clock != null) clock.OnDayEnd += HandleDayEnd;
        }

        private void OnDisable()
        {
            if (clock != null) clock.OnDayEnd -= HandleDayEnd;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (DayOver) return;
            if (clock != null && !clock.IsRunning) return; // nothing slips before the day begins

            float dt = Time.deltaTime;
            TaskState active = ActiveTask;

            if (active != lastActive)
            {
                if (lastActive != null && lastActive.Action != null) lastActive.Action.OnLeave();
                if (active != null)
                {
                    active.MarkVisited();
                    if (active.Action != null) active.Action.OnArrive();
                }
                lastActive = active;
                OnActiveTaskChanged?.Invoke(active);
            }

            foreach (var t in tasks)
            {
                if (t == null) continue;
                if (t == active)
                {
                    if (t.Action != null) t.Action.ActiveTick(dt);
                }
                else
                {
                    t.Drain(dt);
                }
            }
        }

        public void NotifyEnteredZone(TaskState task) => CurrentZone = task;

        public void NotifyExitedZone(TaskState task)
        {
            if (CurrentZone == task) CurrentZone = null;
        }

        /// <summary>Action pressed this frame (tap/click/space). Returns true if it went to a task.</summary>
        public bool Tap()
        {
            var a = ActiveTask;
            if (a == null || a.Action == null) return false;
            LastActionTime = Time.time;
            a.Action.OnTap();
            return true;
        }

        /// <summary>Action held this frame.</summary>
        public void Hold(float deltaTime)
        {
            var a = ActiveTask;
            if (a != null && a.Action != null)
            {
                LastActionTime = Time.time;
                a.Action.OnHold(deltaTime);
            }
        }

        /// <summary>Which task (if any) a world-space click point belongs to.</summary>
        public TaskState TaskNear(Vector3 worldPoint)
        {
            TaskState best = null;
            float bestDist = float.MaxValue;
            foreach (var t in tasks)
            {
                if (t == null) continue;
                Vector3 d = worldPoint - t.transform.position;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist <= t.ClickRadius && dist < bestDist)
                {
                    best = t;
                    bestDist = dist;
                }
            }
            return best;
        }

        private void HandleDayEnd()
        {
            if (DayOver) return;
            DayOver = true;
            DayRecord.Clear();
            foreach (var t in tasks)
            {
                if (t == null) continue;
                DayRecord.Add(new DayRecordEntry { Task = t, Held = t.IsHeld });
            }
            if (lastActive != null && lastActive.Action != null) lastActive.Action.OnLeave();
            lastActive = null;
            OnActiveTaskChanged?.Invoke(null);
            OnDayRecorded?.Invoke();
        }
    }
}
