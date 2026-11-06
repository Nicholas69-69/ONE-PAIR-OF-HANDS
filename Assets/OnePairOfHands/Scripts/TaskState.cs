using UnityEngine;

namespace OnePairOfHands
{
    public enum TaskType { Water, Cook, Teach, Work }

    /// <summary>
    /// "Carried so far" for one task: a 0–1 value that never completes or locks.
    /// It rises only through its TaskAction while Ataa is here, and drains whenever she is elsewhere.
    /// Lives on the Task_X object, whose trigger collider marks the place.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TaskState : MonoBehaviour
    {
        public const float HeldThreshold = 0.5f;

        [SerializeField] private TaskType type;
        [SerializeField] private int number = 1;
        [SerializeField] private string displayName = "Task";

        [SerializeField, Range(0f, 1f), Tooltip("Value at the start of the day.")]
        private float startValue = 0f;

        [SerializeField, Min(0f), Tooltip("How much is lost per real second while Ataa is elsewhere (or walking).")]
        private float drainPerSecond = 0.003f;

        [SerializeField, Min(0.5f), Tooltip("Clicking within this distance of the task sends Ataa here.")]
        private float clickRadius = 6f;

        public TaskType Type => type;
        public int Number => number;
        public string DisplayName => displayName;
        public string NumberedName => $"{number} {displayName}";
        public float ClickRadius => clickRadius;
        public float Value { get; private set; }
        public bool Visited { get; private set; }
        public bool IsHeld => Value >= HeldThreshold;
        public TaskAction Action { get; private set; }

        /// <summary>"not been" if never visited, otherwise "held" (≥0.5) or "slipping".</summary>
        public string StatusLabel => !Visited ? "not been" : IsHeld ? "held" : "slipping";

        public Vector3 StandPoint => transform.position;

        private void Awake()
        {
            Value = startValue;
            Action = GetComponent<TaskAction>();
            GetComponent<Collider>().isTrigger = true;
        }

        public void Add(float amount)
        {
            Value = Mathf.Clamp01(Value + amount);
        }

        public void Drain(float deltaTime)
        {
            Value = Mathf.Max(0f, Value - drainPerSecond * deltaTime);
        }

        public void MarkVisited() => Visited = true;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<AtaaController>() != null && TaskManager.Instance != null)
                TaskManager.Instance.NotifyEnteredZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<AtaaController>() != null && TaskManager.Instance != null)
                TaskManager.Instance.NotifyExitedZone(this);
        }
    }
}
