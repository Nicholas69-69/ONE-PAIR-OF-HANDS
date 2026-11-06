using UnityEngine;

namespace OnePairOfHands
{
    /// <summary>
    /// Persistent audio: a soft ambient loop and UI clicks. Created automatically on first use and
    /// kept across scenes. Everything goes through AudioListener.volume, which follows the master
    /// volume setting (and the audio on/off toggle).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;

        [SerializeField] private float ambientVolume = 0.55f;
        [SerializeField] private float clickVolume = 0.6f;

        private AudioSource ambientSource;
        private AudioSource uiSource;
        private AudioClip clickClip;

        public static AudioManager Instance
        {
            get
            {
                Ensure();
                return instance;
            }
        }

        public bool AmbientPlaying => ambientSource != null && ambientSource.isPlaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Ensure();
            GameSettings.ApplyDisplay();
        }

        private static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.clip = Resources.Load<AudioClip>("Audio/ambient_camp");
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;
            ambientSource.volume = ambientVolume;
            ambientSource.ignoreListenerPause = false;

            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.ignoreListenerPause = true;
            clickClip = Resources.Load<AudioClip>("Audio/ui_click");

            GameSettings.Changed += ApplyVolume;
            ApplyVolume();
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplyVolume;
        }

        private void ApplyVolume()
        {
            AudioListener.volume = GameSettings.EffectiveVolume;
        }

        /// <summary>Starts the ambient loop (first called on the player's Begin tap).</summary>
        public void StartAmbient()
        {
            if (ambientSource.clip != null && !ambientSource.isPlaying) ambientSource.Play();
        }

        public void Click()
        {
            if (clickClip != null) uiSource.PlayOneShot(clickClip, clickVolume);
        }
    }
}
