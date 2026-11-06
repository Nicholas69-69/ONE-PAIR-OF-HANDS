using UnityEngine;

namespace OnePairOfHands
{
    /// <summary>Cook food: tap to stir the pot.</summary>
    public class CookTaskAction : TaskAction
    {
        [SerializeField, Min(0f)] private float perStir = 0.05f;

        private int stirs;

        public override string Hint => stirs == 0 ? "Tap to stir the pot." : $"Tap to stir the pot. ({stirs} stirs)";

        public override void OnTap()
        {
            stirs++;
            State.Add(perStir);
        }
    }
}
