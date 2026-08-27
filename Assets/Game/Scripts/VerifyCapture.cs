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
                ClassSelectScreen.Instance.Choose(4); // verificar el Mago
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
            var playerNow = GameManager.Player;

            // 1) Forzar un ataque en el punto de aparicion (zona despejada, sin
            //    arboles que tapen al sprite) para verificar la animacion
            //    (frame de impacto ~0.15 s tras iniciarla).
            var pc = playerNow != null ? playerNow.GetComponent<CharacterCombat>() : null;
            pc?.OnPrimaryDown(Vector2.right);
            yield return new WaitForSecondsRealtime(0.16f);
            Capture("03_gameplay_a");

            // 2) Colocar al jugador junto al enemigo para que haga aggro y
            //    capturar combate real.
            var enemy = FindFirstObjectByType<EnemyAI>();
            if (enemy != null && playerNow != null)
            {
                playerNow.transform.position =
                    (Vector2)enemy.transform.position + new Vector2(3.2f, 0.5f);
                Camera.main?.GetComponent<SmoothCameraFollow>()?.SnapToTarget();
            }

            // Diagnostico: ¿el animador del enemigo avanza o esta congelado?
            if (enemy != null)
            {
                var ea = enemy.GetComponentInChildren<Animator>();
                var esr = enemy.GetComponentInChildren<SpriteRenderer>();
                var d = new System.Text.StringBuilder();
                d.AppendLine("enemigo: " + enemy.name + " tier " + enemy.tier);
                if (ea == null) d.AppendLine("animator: NULL");
                else
                {
                    var st = ea.GetCurrentAnimatorStateInfo(0);
                    d.AppendLine("controller: " + (ea.runtimeAnimatorController != null
                        ? ea.runtimeAnimatorController.name : "NULL")
                        + " enabled: " + ea.isActiveAndEnabled + " speed: " + ea.speed
                        + " culling: " + ea.cullingMode);
                    d.AppendLine("estado len: " + st.length + " loop: " + st.loop
                        + " t: " + st.normalizedTime.ToString("F3"));
                    var clips = ea.GetCurrentAnimatorClipInfo(0);
                    d.AppendLine("clip: " + (clips.Length > 0 && clips[0].clip != null
                        ? clips[0].clip.name + " looping:" + clips[0].clip.isLooping
                        + " len:" + clips[0].clip.length : "SIN CLIP"));
                    d.AppendLine("sprite A: " + (esr != null && esr.sprite != null ? esr.sprite.name : "NULL"));
                }
                yield return new WaitForSecondsRealtime(0.7f);
                if (ea != null)
                {
                    d.AppendLine("t tras 0.7s: " + ea.GetCurrentAnimatorStateInfo(0).normalizedTime.ToString("F3"));
                    d.AppendLine("sprite B: " + (esr != null && esr.sprite != null ? esr.sprite.name : "NULL"));
                }
                File.WriteAllText("Library/tinyrpg_debug_enemy.txt", d.ToString());
            }

            yield return new WaitForSecondsRealtime(1.8f);
            Capture("04_gameplay_b");
            yield return new WaitForSecondsRealtime(2f);
            Capture("05_gameplay_c");
            yield return new WaitForSecondsRealtime(0.8f);

            // --- Verificacion de aliados ---
            // Campamento tras el nivel 6: debe aparecer el recluta gratis.
            GameFlow.Instance?.DebugLoadRest(6);
            yield return new WaitForSecondsRealtime(0.8f);

            // Diagnostico de la fogata (llama invisible en capturas previas).
            var campfire = FindFirstObjectByType<Campfire>();
            var diag = new System.Text.StringBuilder();
            if (campfire == null) diag.AppendLine("campfire: NO EXISTE");
            else
            {
                var fireChild = campfire.transform.Find("Fire");
                diag.AppendLine("campfire pos: " + campfire.transform.position);
                if (fireChild == null) diag.AppendLine("fire child: NO EXISTE");
                else
                {
                    var fsr = fireChild.GetComponent<SpriteRenderer>();
                    var fan = fireChild.GetComponent<Animator>();
                    diag.AppendLine("fire pos: " + fireChild.position
                        + " activo: " + fireChild.gameObject.activeInHierarchy);
                    diag.AppendLine("fire sprite: " + (fsr != null && fsr.sprite != null ? fsr.sprite.name : "NULL")
                        + " order: " + (fsr != null ? fsr.sortingOrder.ToString() : "-")
                        + " enabled: " + (fsr != null && fsr.enabled));
                    diag.AppendLine("fire controller: " + (fan != null && fan.runtimeAnimatorController != null
                        ? fan.runtimeAnimatorController.name : "NULL"));
                }
            }
            var lib2 = MapLibrary.Instance;
            diag.AppendLine("lib.fireSprite: " + (lib2 != null && lib2.fireSprite != null ? lib2.fireSprite.name : "NULL"));
            diag.AppendLine("lib.fireController: " + (lib2 != null && lib2.fireController != null ? lib2.fireController.name : "NULL"));
            File.WriteAllText("Library/tinyrpg_debug.txt", diag.ToString());
            var recruiter = FindFirstObjectByType<AllyRecruiter>();
            if (recruiter != null && GameManager.Player != null)
            {
                // Acercarse para ver la burbuja y reclutar via SendMessage.
                GameManager.Player.transform.position =
                    (Vector2)recruiter.transform.position + new Vector2(-1.6f, 0f);
                Camera.main?.GetComponent<SmoothCameraFollow>()?.SnapToTarget();
                yield return new WaitForSecondsRealtime(0.5f);
                Capture("06_rest_recruiter");
                // La captura se hace al FINAL del frame: esperar antes de mutar
                // el estado o la imagen saldria con el estado nuevo.
                yield return new WaitForSecondsRealtime(0.3f);
                recruiter.SendMessage("TryRecruit", GameManager.Player);
                yield return new WaitForSecondsRealtime(0.8f);
                Capture("07_ally_recruited");
                yield return new WaitForSecondsRealtime(0.3f);
            }

            // Nivel 7 (IA media) con el aliado en combate. Curar al jugador
            // antes: llega tocado del combate escenificado y moriria.
            GameFlow.Instance?.DebugLoadLevel(7);
            yield return new WaitForSecondsRealtime(0.8f);
            var playerStats = GameManager.Player != null
                ? GameManager.Player.GetComponent<CharacterStats>() : null;
            playerStats?.Heal(playerStats.maxHealth);
            var foe = FindFirstObjectByType<EnemyAI>();
            if (foe != null && GameManager.Player != null)
            {
                GameManager.Player.transform.position =
                    (Vector2)foe.transform.position + new Vector2(3.0f, 0.4f);
                Camera.main?.GetComponent<SmoothCameraFollow>()?.SnapToTarget();
            }
            yield return new WaitForSecondsRealtime(3f);
            Capture("08_ally_combat_a");
            yield return new WaitForSecondsRealtime(2.5f);
            Capture("09_ally_combat_b");
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
