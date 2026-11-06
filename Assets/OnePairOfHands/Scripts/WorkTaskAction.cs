using UnityEngine;

namespace OnePairOfHands
{
    /// <summary>
    /// Work online: tap to send an item. The connection sometimes drops for a few seconds,
    /// and nothing can be sent until it comes back.
    /// </summary>
    public class WorkTaskAction : TaskAction
    {
        [SerializeField, Min(0f)] private float perSend = 0.05f;
        [SerializeField, Min(0f)] private float dropDuration = 3f;
        [SerializeField, Range(0f, 1f), Tooltip("Chance the connection drops after each send.")]
        private float dropChancePerSend = 0.15f;
        [SerializeField, Min(0f), Tooltip("Average seconds between random drops while working.")]
        private float averageSecondsBetweenDrops = 20f;

        [SerializeField, Tooltip("The glowing screen quad (optional).")]
        private Renderer screenRenderer;
        [SerializeField] private Light screenLight;
        [SerializeField] private Color onlineColor = new Color(0.55f, 0.75f, 1f);
        [SerializeField] private Color offlineColor = new Color(0.15f, 0.17f, 0.22f);

        private float offlineUntil;
        private int sent;
        private MaterialPropertyBlock block;

        public bool IsOffline => Time.time < offlineUntil;

        public override string Hint
        {
            get
            {
                if (IsOffline)
                    return $"Connection dropped. Reconnecting... ({Mathf.CeilToInt(offlineUntil - Time.time)}s)";
                return sent == 0 ? "Tap to send an item." : $"Tap to send an item. ({sent} sent)";
            }
        }

        public override void OnTap()
        {
            if (IsOffline) return;
            sent++;
            State.Add(perSend);
            if (Random.value < dropChancePerSend) Drop();
        }

        public override void ActiveTick(float deltaTime)
        {
            if (IsOffline || averageSecondsBetweenDrops <= 0f) return;
            if (Random.value < deltaTime / averageSecondsBetweenDrops) Drop();
        }

        private void Drop() => offlineUntil = Time.time + dropDuration;

        private void Update()
        {
            if (screenRenderer == null) return;
            block ??= new MaterialPropertyBlock();
            Color c = IsOffline ? offlineColor : onlineColor;
            screenRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_EmissionColor", c * (IsOffline ? 0.2f : 2f));
            screenRenderer.SetPropertyBlock(block);
            if (screenLight != null) screenLight.enabled = !IsOffline;
        }
    }
}
