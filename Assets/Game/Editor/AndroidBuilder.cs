using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TinyRpg.EditorTools
{
    /// Compila el APK de Android. Requiere el modulo "Android Build Support"
    /// (con SDK/NDK/JDK) instalado desde Unity Hub para esta version del editor.
    public static class AndroidBuilder
    {
        public const string OutputDir = "Builds/Android";
        public const string ResultFile = "Library/tinyrpg_android_result.txt";

        [MenuItem("TinyRpg/Compilar juego (Android APK)")]
        public static void BuildAndroid()
        {
            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(
                        BuildTargetGroup.Android, BuildTarget.Android))
                {
                    const string msg = "FAIL modulo Android no instalado en este editor "
                        + "(Unity Hub > Instalaciones > Anadir modulos > Android Build Support "
                        + "con SDK/NDK y OpenJDK)";
                    File.WriteAllText(ResultFile, msg);
                    Debug.LogError("[AndroidBuilder] " + msg);
                    return;
                }

                ApplyAndroidSettings();

                Directory.CreateDirectory(OutputDir);
                var options = new BuildPlayerOptions
                {
                    // Igual que el build de Windows: SOLO la escena del juego,
                    // nunca la escena de pruebas del coliseo.
                    scenes = new[] { "Assets/Game/Scenes/Game.unity" },
                    locationPathName = OutputDir + "/ElTesoroDelBosque.apk",
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                };

                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                if (summary.result == BuildResult.Succeeded)
                {
                    string message = $"OK {summary.totalSize / (1024 * 1024)} MB en {OutputDir}";
                    File.WriteAllText(ResultFile, message);
                    Debug.Log("[AndroidBuilder] " + message);
                }
                else
                {
                    File.WriteAllText(ResultFile,
                        $"FAIL {summary.result}: {summary.totalErrors} errores");
                    Debug.LogError("[AndroidBuilder] Build fallida: " + summary.result);
                }
            }
            catch (Exception e)
            {
                File.WriteAllText(ResultFile, "FAIL " + e.Message);
                Debug.LogError("[AndroidBuilder] FALLO: " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// Ajustes de plataforma para un movil: apaisado, IL2CPP + ARM64 (lo
        /// exige Google Play), sin pantalla de permisos innecesaria.
        public static void ApplyAndroidSettings()
        {
            PlayerSettings.productName = "El Tesoro del Bosque";
            PlayerSettings.companyName = "Agustin";
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android, "com.agustin.eltesorodelbosque");

            // Solo apaisado: el HUD y la camara estan pensados en horizontal.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.forceInternetPermission = false;

            // El juego no necesita mantener la pantalla a 60 si el movil sufre;
            // Unity ya limita a la tasa de refresco por defecto.
            PlayerSettings.use32BitDisplayBuffer = true;
        }
    }
}
