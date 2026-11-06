using UnityEngine;

namespace OnePairOfHands
{
    /// <summary>
    /// Trigger around Ataa's home cluster. Water she is carrying is delivered when she arrives here.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HomeZone : MonoBehaviour
    {
        [SerializeField] private WaterTaskAction water;

        public bool AtaaIsHome { get; private set; }
        public float LastDeliveredAmount { get; private set; }
        public float LastDeliveryTime { get; private set; } = -999f;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (water == null) water = FindFirstObjectByType<WaterTaskAction>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<AtaaController>() == null) return;
            AtaaIsHome = true;
            TryDeliver();
        }

        private void OnTriggerStay(Collider other)
        {
            // Covers the case where she fills the jug and the day starts with her already home.
            if (other.GetComponentInParent<AtaaController>() != null) TryDeliver();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<AtaaController>() != null) AtaaIsHome = false;
        }

        private void TryDeliver()
        {
            if (water == null || !water.IsCarrying) return;
            if (TaskManager.Instance != null && TaskManager.Instance.DayOver) return;
            LastDeliveredAmount = water.DeliverHome();
            LastDeliveryTime = Time.time;
        }
    }
}
