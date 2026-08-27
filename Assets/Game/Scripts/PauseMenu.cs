using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyRpg
{
    /// Menu de pausa in-game: se abre con el engranaje de la esquina superior
    /// izquierda o con ESC. Contiene Configuracion y Salir del juego; ESC o el
    /// engranaje lo cierran de nuevo.
    public class PauseMenu : MonoBehaviour
    {
        public GameObject panel;
        public SettingsScreen settingsScreen;

        public bool IsOpen => panel != null && panel.activeSelf;

        void Start()
        {
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            // ESC dentro de Configuracion: solo cierra la configuracion.
            if (settingsScreen != null && settingsScreen.panel != null
                && settingsScreen.panel.activeSelf)
            {
                settingsScreen.Close();
                return;
            }

            // En el titulo o en la seleccion de clase, ESC no pausa.
            if (TitleScreen.Instance != null && TitleScreen.Instance.panel != null
                && TitleScreen.Instance.panel.activeSelf) return;
            if (ClassSelectScreen.Instance != null && !ClassSelectScreen.Instance.HasChosen) return;

            Toggle();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (ClassSelectScreen.Instance == null || !ClassSelectScreen.Instance.HasChosen)
                return; // solo in-game
            if (panel != null) panel.SetActive(true);
            Time.timeScale = 0f;
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            Time.timeScale = 1f;
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
