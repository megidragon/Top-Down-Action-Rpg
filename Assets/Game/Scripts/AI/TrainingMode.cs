using UnityEngine;

namespace TinyRpg
{
    /// Interruptor global del entrenamiento por lotes. Con el activo el juego
    /// simula el COMBATE pero no lo dibuja: nada de mallas de efectos, textos
    /// flotantes ni animaciones.
    ///
    /// Es la mitad del ahorro de tiempo. Cada golpe normal crea una malla y un
    /// GameObject para el destello; con ochenta duelistas peleando a 8x eso son
    /// miles de asignaciones por segundo, y el recolector de basura acaba
    /// dominando el reloj.
    public static class TrainingMode
    {
        public static bool Active => users > 0;

        /// Cuantos entrenadores estan dentro. Con varias poblaciones en
        /// paralelo, el primero que TERMINA su lote no debe devolver el reloj
        /// a 1x mientras los demas siguen entrenando.
        static int users;

        /// Estado del reloj antes de entrenar, para restaurarlo al terminar.
        static float previousTimeScale = 1f;
        static float previousMaxDelta = 0.333f;

        public static void Begin(float timeScale)
        {
            users++;
            if (users > 1) return; // ya esta acelerado

            previousTimeScale = Time.timeScale;
            previousMaxDelta = Time.maximumDeltaTime;

            Time.timeScale = Mathf.Max(1f, timeScale);
            // Sin esto Unity recorta cuantos FixedUpdate ejecuta por frame y la
            // escala de tiempo alta deja de rendir: el juego "va a camara
            // lenta" respecto al reloj real justo cuando mas prisa tenemos.
            Time.maximumDeltaTime = Mathf.Max(0.5f, timeScale * 0.05f);
        }

        public static void End()
        {
            if (users == 0) return;
            users--;
            if (users > 0) return; // aun queda gente entrenando

            Time.timeScale = previousTimeScale;
            Time.maximumDeltaTime = previousMaxDelta;
        }
    }
}
