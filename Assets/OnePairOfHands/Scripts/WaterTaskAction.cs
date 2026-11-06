using UnityEngine;

namespace OnePairOfHands
{
    /// <summary>
    /// Collect water: hold to fill the jug at the well. The water only counts
    /// once Ataa has carried it back home (see HomeZone).
    /// </summary>
    public class WaterTaskAction : TaskAction
    {
        [SerializeField, Min(0.01f), Tooltip("Jug fill per second while holding.")]
        private float fillPerSecond = 0.25f;

        [SerializeField, Range(0.1f, 1f), Tooltip("How much one full jug adds to 'carried so far' when delivered home.")]
        private float valuePerFullJug = 0.6f;

        public float JugFill { get; private set; }
        public bool IsCarrying => JugFill > 0.001f;

        public override string Hint
        {
            get
            {
                if (JugFill >= 1f) return "The jug is full. Walk it home over the bridge.";
                if (IsCarrying) return $"Hold to fill the jug ({Mathf.RoundToInt(JugFill * 100f)}%). It only counts once it's home.";
                return "Hold to fill the jug. It only counts once it's home.";
            }
        }

        public override void OnHold(float deltaTime)
        {
            JugFill = Mathf.Min(1f, JugFill + fillPerSecond * deltaTime);
        }

        /// <summary>Pour the carried water out at home. Returns the amount added.</summary>
        public float DeliverHome()
        {
            if (!IsCarrying) return 0f;
            float added = JugFill * valuePerFullJug;
            State.Add(added);
            JugFill = 0f;
            return added;
        }
    }
}
