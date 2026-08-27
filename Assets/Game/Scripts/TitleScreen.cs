using UnityEngine;

namespace TinyRpg
{
    /// Pantalla de inicio: fondo con la escena original sombreada y el menu
    /// principal (comenzar partida / configuracion / salir).
    public class TitleScreen : MonoBehaviour
    {
        public static TitleScreen Instance { get; private set; }

        public GameObject panel;
        public SettingsScreen settingsScreen;
        public ClassSelectScreen classSelect;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            GameSettings.ApplyAll();
            Time.timeScale = 0f;
            if (panel != null) panel.SetActive(true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void StartGame()
        {
            if (panel != null) panel.SetActive(false);
            classSelect?.Show();
        }

        public void OpenSettings()
        {
            settingsScreen?.Open();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
