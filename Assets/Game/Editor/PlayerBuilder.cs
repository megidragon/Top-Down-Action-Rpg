using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TinyRpg.EditorTools
{
    /// Compila el ejecutable standalone de Windows en Builds/ElTesoroDelBosque.
    /// Ejecutable desde el menu o via la senal Library/tinyrpg_player_request.txt.
    public static class PlayerBuilder
    {
        public const string OutputDir = "Builds/ElTesoroDelBosque";
        public const string ResultFile = "Library/tinyrpg_player_result.txt";

        [MenuItem("TinyRpg/Compilar juego (Windows)")]
        public static void BuildWindows()
        {
            try
            {
                PlayerSettings.productName = "El Tesoro del Bosque";
                PlayerSettings.companyName = "Agustin";
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
                PlayerSettings.resizableWindow = true;

                Directory.CreateDirectory(OutputDir);
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Game/Scenes/Game.unity" },
                    locationPathName = OutputDir + "/ElTesoroDelBosque.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                };

                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                if (summary.result == BuildResult.Succeeded)
                {
                    string message = $"OK {summary.totalSize / (1024 * 1024)} MB en {OutputDir}";
                    File.WriteAllText(ResultFile, message);
                    Debug.Log("[PlayerBuilder] " + message);
                }
                else
                {
                    File.WriteAllText(ResultFile,
                        $"FAIL {summary.result}: {summary.totalErrors} errores");
                    Debug.LogError("[PlayerBuilder] Build fallida: " + summary.result);
                }
            }
            catch (Exception e)
            {
                File.WriteAllText(ResultFile, "FAIL " + e.Message);
                Debug.LogError("[PlayerBuilder] FALLO: " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
