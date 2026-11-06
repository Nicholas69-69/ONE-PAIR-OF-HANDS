using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>Slow pan over the still render of the camp behind the main menu.</summary>
    [RequireComponent(typeof(RawImage))]
    public class MenuBackgroundPan : MonoBehaviour
    {
        [SerializeField] private float period = 60f;
        [SerializeField, Range(0.5f, 1f)] private float zoom = 0.82f;
        private RawImage image;

        private void Awake() => image = GetComponent<RawImage>();

        private void Update()
        {
            if (image == null || image.texture == null) return;
            float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float texAspect = (float)image.texture.width / image.texture.height;
            // Cover the screen, then zoom in a little so there is room to drift.
            float w = zoom, h = zoom;
            if (screenAspect < texAspect) w = zoom * screenAspect / texAspect;
            else h = zoom * texAspect / screenAspect;
            float t = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period) * 0.5f + 0.5f;
            float x = Mathf.Lerp(0f, 1f - w, t);
            float y = Mathf.Lerp(0.15f * (1f - h), 0.85f * (1f - h), 1f - t);
            image.uvRect = new Rect(x, y, w, h);
        }
    }
}
