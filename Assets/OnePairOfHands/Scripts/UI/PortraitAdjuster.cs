using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>Moves HUD elements to different spots when the screen is portrait (e.g. 540x960).</summary>
    public class PortraitAdjuster : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            public RectTransform target;
            public Vector2 landscapePosition;
            public Vector2 portraitPosition;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        private int lastW, lastH;

        public void Add(RectTransform t, Vector2 landscape, Vector2 portrait)
        {
            entries.Add(new Entry { target = t, landscapePosition = landscape, portraitPosition = portrait });
        }

        private void Update()
        {
            if (Screen.width == lastW && Screen.height == lastH) return;
            lastW = Screen.width;
            lastH = Screen.height;
            bool portrait = Screen.height > Screen.width;
            foreach (var e in entries)
                if (e.target != null) e.target.anchoredPosition = portrait ? e.portraitPosition : e.landscapePosition;
        }
    }
}
