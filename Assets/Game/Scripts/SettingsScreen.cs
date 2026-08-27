using UnityEngine;
using UnityEngine.UI;

namespace TinyRpg
{
    /// Pantalla de configuracion con pestanas Video / Sonido / General.
    ///  - Video: resolucion (por defecto la recomendada), pantalla completa o
    ///    ventana, y activar/desactivar el temblor de pantalla.
    ///  - Sonido: volumen general, efectos y musica.
    ///  - General: idioma (espanol / ingles).
    public class SettingsScreen : MonoBehaviour
    {
        public GameObject panel;
        public GameObject videoTab;
        public GameObject audioTab;
        public GameObject generalTab;

        public Text resolutionValue;
        public Text windowModeValue;
        public Text shakeValue;
        public Text languageValue;
        public Slider generalSlider;
        public Slider effectsSlider;
        public Slider musicSlider;

        void Start()
        {
            if (panel != null) panel.SetActive(false);

            if (generalSlider != null)
            {
                generalSlider.SetValueWithoutNotify(GameSettings.VolumeGeneral);
                generalSlider.onValueChanged.AddListener(v => GameSettings.VolumeGeneral = v);
            }
            if (effectsSlider != null)
            {
                effectsSlider.SetValueWithoutNotify(GameSettings.VolumeEffects);
                effectsSlider.onValueChanged.AddListener(v => GameSettings.VolumeEffects = v);
            }
            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(GameSettings.VolumeMusic);
                musicSlider.onValueChanged.AddListener(v => GameSettings.VolumeMusic = v);
            }
        }

        public void Open()
        {
            if (panel != null) panel.SetActive(true);
            ShowVideoTab();
            RefreshValues();
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void ShowVideoTab() => ShowTab(videoTab);
        public void ShowAudioTab() => ShowTab(audioTab);
        public void ShowGeneralTab() => ShowTab(generalTab);

        void ShowTab(GameObject tab)
        {
            if (videoTab != null) videoTab.SetActive(tab == videoTab);
            if (audioTab != null) audioTab.SetActive(tab == audioTab);
            if (generalTab != null) generalTab.SetActive(tab == generalTab);
            RefreshValues();
        }

        // ------------------- Video -------------------

        public void CycleResolution(int direction)
        {
            var all = Screen.resolutions;
            if (all.Length == 0) return;
            int index = GameSettings.ResolutionIndex;
            if (index < 0) index = all.Length - 1; // recomendada = la mayor nativa
            index = (index + direction + all.Length) % all.Length;
            GameSettings.ResolutionIndex = index;
            RefreshValues();
        }

        public void ToggleWindowMode()
        {
            GameSettings.Fullscreen = !GameSettings.Fullscreen;
            RefreshValues();
        }

        public void ToggleShake()
        {
            GameSettings.ScreenShake = !GameSettings.ScreenShake;
            RefreshValues();
        }

        // ------------------- General -------------------

        public void ToggleLanguage()
        {
            GameSettings.Language = GameSettings.Language == GameLanguage.Spanish
                ? GameLanguage.English : GameLanguage.Spanish;
            RefreshValues();
        }

        void RefreshValues()
        {
            if (resolutionValue != null)
            {
                var res = GameSettings.CurrentResolution;
                string suffix = GameSettings.ResolutionIndex < 0 ? " *" : "";
                resolutionValue.text = $"{res.width} x {res.height}{suffix}";
            }
            if (windowModeValue != null)
                windowModeValue.text = Loc.T(GameSettings.Fullscreen ? "set.fullscreen" : "set.windowed");
            if (shakeValue != null)
                shakeValue.text = Loc.T(GameSettings.ScreenShake ? "set.on" : "set.off");
            if (languageValue != null)
                languageValue.text = GameSettings.Language == GameLanguage.Spanish ? "Español" : "English";
        }
    }
}
