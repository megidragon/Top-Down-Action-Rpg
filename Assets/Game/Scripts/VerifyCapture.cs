#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace TinyRpg
{
    /// Herramienta interna de verificacion: al entrar en Play, si existe la variable
    /// de entorno TINYRPG_CAPTURE o el archivo-senal Temp/tinyrpg_verify_request.txt
    /// (su contenido es la carpeta de salida), captura pantallazos del juego y
    /// despues sale del modo Play (o cierra el editor en modo batch/env).
    /// No hace nada en una partida normal.
    public static class VerifyCapture
    {
        const string VerifyRequest = "Library/tinyrpg_verify_request.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            string outDir = Environment.GetEnvironmentVariable("TINYRPG_CAPTURE");
            bool exitEditorWhenDone = !string.IsNullOrEmpty(outDir);

            if (string.IsNullOrEmpty(outDir) && File.Exists(VerifyRequest))
            {
                outDir = File.ReadAllText(VerifyRequest).Trim();
                File.Delete(VerifyRequest);
            }
            if (string.IsNullOrEmpty(outDir)) return;

            Directory.CreateDirectory(outDir);
            var go = new GameObject("VerifyCapture");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var runner = go.AddComponent<VerifyCaptureRunner>();
            runner.outDir = outDir;
            runner.exitEditorWhenDone = exitEditorWhenDone;
        }
    }

    public class VerifyCaptureRunner : MonoBehaviour
    {
        public string outDir;
        public bool exitEditorWhenDone;

        IEnumerator Start()
        {
            // Esperas en tiempo real: el juego arranca pausado (timeScale 0)
            // mientras se muestra la seleccion de clase.
            yield return new WaitForSecondsRealtime(0.7f);
            if (TitleScreen.Instance != null)
            {
                Capture("00a_title");
                yield return new WaitForSecondsRealtime(0.4f);
                TitleScreen.Instance.StartGame();
            }
            if (ClassSelectScreen.Instance != null && !ClassSelectScreen.Instance.HasChosen)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                Capture("00_class_select");
                yield return new WaitForSecondsRealtime(0.5f);
                ClassSelectScreen.Instance.Choose(3); // verificar el Monje
            }

            yield return new WaitForSecondsRealtime(0.9f);
            Capture("01_spawn");
            yield return new WaitForSecondsRealtime(0.4f);

            // Vista general del mapa completo.
            var cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<SmoothCameraFollow>() : null;
            float originalSize = cam != null ? cam.orthographicSize : 7f;
            if (cam != null)
            {
                if (follow != null) follow.enabled = false;
                var map = GameFlow.Instance != null ? GameFlow.Instance.CurrentMap : null;
                if (map != null)
                {
                    cam.orthographicSize = map.H / 2f + 2f;
                    cam.transform.position = new Vector3(map.W / 2f, map.H / 2f, -10f);
                }
                else
                {
                    cam.orthographicSize = 20f;
                    cam.transform.position = new Vector3(18f, 12f, -10f);
                }
            }
            yield return new WaitForSecondsRealtime(0.5f);
            Capture("02_overview");
            yield return new WaitForSecondsRealtime(0.3f);
            if (cam != null)
            {
                cam.orthographicSize = originalSize;
                if (follow != null) follow.enabled = true;
            }

            // Entrar al nivel 1 del bosque para capturar combate real: colocar
            // al jugador junto al enemigo para que haga aggro y ataque.
            GameFlow.Instance?.Advance();
            yield return new WaitForSecondsRealtime(0.8f);
            var enemy = FindFirstObjectByType<EnemyAI>();
            var playerNow = GameManager.Player;
            if (enemy != null && playerNow != null)
            {
                playerNow.transform.position =
                    (Vector2)enemy.transform.position + new Vector2(3.2f, 0.5f);
                Camera.main?.GetComponent<SmoothCameraFollow>()?.SnapToTarget();
            }
            yield return new WaitForSecondsRealtime(2.7f);
            Capture("03_gameplay_a");
            yield return new WaitForSecondsRealtime(2.5f);
            Capture("04_gameplay_b");
            yield return new WaitForSecondsRealtime(2f);
            Capture("05_gameplay_c");
            yield return new WaitForSecondsRealtime(0.8f);

            File.WriteAllText("Library/tinyrpg_verify_done.txt", "OK " + DateTime.Now.ToString("HH:mm:ss"));

            if (exitEditorWhenDone)
                UnityEditor.EditorApplication.Exit(0);
            else
                UnityEditor.EditorApplication.isPlaying = false;
        }

        void Capture(string name)
        {
            string path = Path.Combine(outDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path, 2);
        }
    }
}
#endif
