using UnityEngine;

namespace OnePairOfHands
{
    /// <summary>Teach the children: a simple prompt; tap to answer it.</summary>
    public class TeachTaskAction : TaskAction
    {
        [SerializeField, Min(0f)] private float perAnswer = 0.06f;
        [SerializeField, Min(0f), Tooltip("Pause before the next question appears.")]
        private float nextPromptDelay = 0.8f;

        [SerializeField, TextArea]
        private string[] prompts =
        {
            "\"What is 3 + 4?\"",
            "\"How do you spell 'sun'?\"",
            "\"Which is bigger, 9 or 6?\"",
            "\"Read this word: 'home'.\"",
            "\"What do plants need to grow?\"",
            "\"Count the stones: one, two, three...\"",
        };

        private int promptIndex;
        private float waitUntil;

        private bool Waiting => Time.time < waitUntil;

        public override string Hint => Waiting
            ? "Good. Next question..."
            : $"A child asks: {prompts[promptIndex]}\nTap to answer.";

        public override void OnArrive()
        {
            waitUntil = 0f;
        }

        public override void OnTap()
        {
            if (Waiting || prompts.Length == 0) return;
            State.Add(perAnswer);
            promptIndex = (promptIndex + 1) % prompts.Length;
            waitUntil = Time.time + nextPromptDelay;
        }
    }
}
