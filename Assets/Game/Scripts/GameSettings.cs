using UnityEngine;

namespace TinyRpg
{
    public enum GameLanguage { Spanish = 0, English = 1 }

    /// Ajustes del juego persistidos en PlayerPrefs: idioma, resolucion,
    /// modo de pantalla, temblor de camara y volumenes.
    public static class GameSettings
    {
        const string KeyLanguage = "tinyrpg.language";
        const string KeyResW = "tinyrpg.res.w";
        const string KeyResH = "tinyrpg.res.h";
        const string KeyFullscreen = "tinyrpg.fullscreen";
        const string KeyShake = "tinyrpg.shake";
        const string KeyTouch = "tinyrpg.touch";
        const string KeyVolGeneral = "tinyrpg.vol.general";
        const string KeyVolEffects = "tinyrpg.vol.effects";
        const string KeyVolMusic = "tinyrpg.vol.music";

        static bool loaded;
        static GameLanguage language;
        static int resWidth;  // 0 = recomendada (nativa)
        static int resHeight;
        static bool fullscreen;
        static bool screenShake;
        static bool forceTouch;
        static float volGeneral, volEffects, volMusic;

        public static GameLanguage Language
        {
            get { EnsureLoaded(); return language; }
            set
            {
                EnsureLoaded();
                if (language == value) return;
                language = value;
                PlayerPrefs.SetInt(KeyLanguage, (int)value);
                PlayerPrefs.Save();
                Loc.NotifyLanguageChanged();
            }
        }

        public static bool ScreenShake
        {
            get { EnsureLoaded(); return screenShake; }
            set
            {
                EnsureLoaded();
                screenShake = value;
                // La camara tiene su propio interruptor: asi el sistema de
                // combate (exportable como paquete) no depende de los ajustes
                // del juego, sino al reves.
                SmoothCameraFollow.ShakeEnabled = value;
                PlayerPrefs.SetInt(KeyShake, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// Fuerza los controles tactiles en escritorio (para probarlos en el
        /// editor). En movil se activan solos, sin depender de este ajuste.
        public static bool ForceTouchControls
        {
            get { EnsureLoaded(); return forceTouch; }
            set { EnsureLoaded(); forceTouch = value; PlayerPrefs.SetInt(KeyTouch, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Fullscreen
        {
            get { EnsureLoaded(); return fullscreen; }
            set { EnsureLoaded(); fullscreen = value; PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0); PlayerPrefs.Save(); ApplyResolution(); }
        }

        public static float VolumeGeneral
        {
            get { EnsureLoaded(); return volGeneral; }
            set { EnsureLoaded(); volGeneral = Mathf.Clamp01(value); AudioListener.volume = volGeneral; PlayerPrefs.SetFloat(KeyVolGeneral, volGeneral); PlayerPrefs.Save(); }
        }

        public static float VolumeEffects
        {
            get { EnsureLoaded(); return volEffects; }
            set { EnsureLoaded(); volEffects = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KeyVolEffects, volEffects); PlayerPrefs.Save(); }
        }

        public static float VolumeMusic
        {
            get { EnsureLoaded(); return volMusic; }
            set { EnsureLoaded(); volMusic = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KeyVolMusic, volMusic); PlayerPrefs.Save(); }
        }

        /// true si esta activa la resolucion recomendada (nativa).
        public static bool IsRecommendedResolution
        {
            get { EnsureLoaded(); return resWidth <= 0 || resHeight <= 0; }
        }

        /// Fija una resolucion concreta; (0,0) vuelve a la recomendada.
        public static void SetResolution(int width, int height)
        {
            EnsureLoaded();
            resWidth = width;
            resHeight = height;
            PlayerPrefs.SetInt(KeyResW, width);
            PlayerPrefs.SetInt(KeyResH, height);
            PlayerPrefs.Save();
            ApplyResolution();
        }

        public static Resolution CurrentResolution
        {
            get
            {
                EnsureLoaded();
                if (!IsRecommendedResolution)
                {
                    foreach (var r in Screen.resolutions)
                        if (r.width == resWidth && r.height == resHeight) return r;
                }
                return Screen.currentResolution; // recomendada (o guardada ya no disponible)
            }
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            language = (GameLanguage)PlayerPrefs.GetInt(KeyLanguage, 0);
            resWidth = PlayerPrefs.GetInt(KeyResW, 0);
            resHeight = PlayerPrefs.GetInt(KeyResH, 0);
            fullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
            screenShake = PlayerPrefs.GetInt(KeyShake, 1) == 1;
            SmoothCameraFollow.ShakeEnabled = screenShake; // sincroniza la camara
            forceTouch = PlayerPrefs.GetInt(KeyTouch, 0) == 1;
            volGeneral = PlayerPrefs.GetFloat(KeyVolGeneral, 1f);
            volEffects = PlayerPrefs.GetFloat(KeyVolEffects, 1f);
            volMusic = PlayerPrefs.GetFloat(KeyVolMusic, 1f);
        }

        /// Aplica volumen y resolucion guardados (al arrancar la escena).
        public static void ApplyAll()
        {
            EnsureLoaded();
            AudioListener.volume = volGeneral;
            ApplyResolution();
        }

        static void ApplyResolution()
        {
#if !UNITY_EDITOR
            var res = CurrentResolution;
            Screen.SetResolution(res.width, res.height,
                fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        }
    }
}
