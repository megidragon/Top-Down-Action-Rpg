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

        // OJO: este componente vive en un panel que la escena guarda INACTIVO,
        // asi que Start() se difiere hasta la primera apertura. Toda la
        // inicializacion va en Awake (corre sincrono en el primer SetActive(true))
        // y no se desactiva nada aqui: el panel ya se guarda oculto.
        void Awake()
        {
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
            // Lista deduplicada por ancho x alto, con la "recomendada" como
            // posicion extra al final (siempre recuperable).
            var unique = new System.Collections.Generic.List<(int w, int h)>();
            foreach (var r in Screen.resolutions)
                if (!unique.Contains((r.width, r.height))) unique.Add((r.width, r.height));
            if (unique.Count == 0) return;

            int positions = unique.Count + 1; // ultima posicion = recomendada
            int current = positions - 1;
            if (!GameSettings.IsRecommendedResolution)
            {
                var actual = GameSettings.CurrentResolution;
                int found = unique.FindIndex(u => u.w == actual.width && u.h == actual.height);
                if (found >= 0) current = found;
            }

            int next = (current + direction + positions) % positions;
            if (next == positions - 1) GameSettings.SetResolution(0, 0); // recomendada
            else GameSettings.SetResolution(unique[next].w, unique[next].h);
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
                string suffix = GameSettings.IsRecommendedResolution ? " *" : "";
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
