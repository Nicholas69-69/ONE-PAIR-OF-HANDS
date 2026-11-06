using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>Warm, calm palette. No red, green or amber.</summary>
    public static class UIPalette
    {
        public static readonly Color Sand = new Color(0.93f, 0.87f, 0.76f, 1f);
        public static readonly Color SandLight = new Color(0.97f, 0.94f, 0.87f, 1f);
        public static readonly Color Ochre = new Color(0.76f, 0.58f, 0.36f, 1f);
        public static readonly Color DeepTeal = new Color(0.07f, 0.25f, 0.27f, 0.94f);
        public static readonly Color DeepTealSolid = new Color(0.07f, 0.25f, 0.27f, 1f);
        public static readonly Color Ink = new Color(0.06f, 0.18f, 0.20f, 1f);
        public static readonly Color SoftText = new Color(0.80f, 0.86f, 0.84f, 1f);
        public static readonly Color Dim = new Color(0.04f, 0.10f, 0.11f, 0.80f);

        /// <summary>Selected segment = ochre, unselected = sand.</summary>
        public static void SetSelected(Button b, bool selected)
        {
            if (b == null) return;
            var img = b.targetGraphic as Image;
            if (img != null) img.color = selected ? Ochre : Sand;
            var label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
