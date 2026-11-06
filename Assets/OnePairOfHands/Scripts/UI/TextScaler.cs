using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>Scales every TMP text under this object by the Text size setting (S/M/L).</summary>
    public class TextScaler : MonoBehaviour
    {
        private readonly Dictionary<TMP_Text, float> baseSizes = new Dictionary<TMP_Text, float>();

        private void OnEnable()
        {
            GameSettings.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= Apply;
        }

        public void Apply()
        {
            float s = GameSettings.TextScale;
            foreach (var t in GetComponentsInChildren<TMP_Text>(true))
            {
                if (!baseSizes.TryGetValue(t, out float b))
                {
                    b = t.fontSize;
                    baseSizes[t] = b;
                }
                t.fontSize = b * s;
            }
        }

        public float BaseSizeOf(TMP_Text t) => baseSizes.TryGetValue(t, out float b) ? b : t.fontSize;
    }
}
