using UnityEngine;

namespace OnePairOfHands
{
    /// <summary>
    /// Base for the placeholder action at each task. Only receives input while Ataa is
    /// standing at this task (the one active task — one pair of hands).
    /// </summary>
    [RequireComponent(typeof(TaskState))]
    public abstract class TaskAction : MonoBehaviour
    {
        protected TaskState State { get; private set; }

        /// <summary>Text shown under the fill bar telling the player what to do.</summary>
        public abstract string Hint { get; }

        protected virtual void Awake()
        {
            State = GetComponent<TaskState>();
        }

        /// <summary>Called on the frame the action button/tap is pressed.</summary>
        public virtual void OnTap() { }

        /// <summary>Called every frame the action button/tap is held (including the first).</summary>
        public virtual void OnHold(float deltaTime) { }

        public virtual void OnArrive() { }
        public virtual void OnLeave() { }

        /// <summary>Called every frame while this is the active task.</summary>
        public virtual void ActiveTick(float deltaTime) { }
    }
}
