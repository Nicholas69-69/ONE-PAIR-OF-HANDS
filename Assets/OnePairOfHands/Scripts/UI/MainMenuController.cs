using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OnePairOfHands
{
    /// <summary>Main menu: Start, How to Play, Settings, Quit (Quit hidden on WebGL).</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameScene = "Level Design";
        [SerializeField] private Button startButton;
        [SerializeField] private Button howToButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject menuButtons;
        [SerializeField] private GameObject howToPanel;
        [SerializeField] private Button howToCloseButton;
        [SerializeField] private SettingsPanel settingsPanel;

        private void Awake()
        {
            Time.timeScale = 1f;
            AtaaController.InputBlocked = false;

            startButton.onClick.AddListener(() => SceneManager.LoadScene(gameScene));
            howToButton.onClick.AddListener(() => ShowSub(howToPanel));
            settingsButton.onClick.AddListener(() => ShowSub(settingsPanel.gameObject));
            howToCloseButton.onClick.AddListener(CloseSubs);
            settingsPanel.Closed += CloseSubs;

            bool canQuit = Application.platform != RuntimePlatform.WebGLPlayer;
#if UNITY_WEBGL
            canQuit = false;
#endif
            quitButton.gameObject.SetActive(canQuit);
            quitButton.onClick.AddListener(Quit);

            howToPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(false);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) CloseSubs();
        }

        private void ShowSub(GameObject panel)
        {
            howToPanel.SetActive(panel == howToPanel);
            settingsPanel.gameObject.SetActive(panel == settingsPanel.gameObject);
            if (menuButtons != null) menuButtons.SetActive(false);
        }

        private void CloseSubs()
        {
            howToPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(false);
            if (menuButtons != null) menuButtons.SetActive(true);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
