using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>Plays the UI click for every button and toggle under this object.</summary>
    public class UIClickSounds : MonoBehaviour
    {
        private void Awake()
        {
            foreach (var b in GetComponentsInChildren<Button>(true))
                b.onClick.AddListener(() => AudioManager.Instance.Click());
            foreach (var t in GetComponentsInChildren<Toggle>(true))
                t.onValueChanged.AddListener(_ => AudioManager.Instance.Click());
        }
    }
}
