using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TinyRpg.EditorTools
{
    /// Permite disparar la construccion (o la verificacion en Play) desde fuera del
    /// editor dejando un archivo-senal en Temp/. Util cuando el proyecto ya esta
    /// abierto en un editor interactivo y no se puede usar batchmode.
    [InitializeOnLoad]
    public static class AutoBuild
    {
        // En Library/ y no en Temp/: Unity vacia Temp/ en cada arranque del editor
        // y las senales armadas durante un reinicio se perderian.
        const string BuildRequest = "Library/tinyrpg_build_request.txt";
        const string VerifyRequest = "Library/tinyrpg_verify_request.txt";
        const string PlayerRequest = "Library/tinyrpg_player_request.txt";
        const string ResultFile = "Library/tinyrpg_build_result.txt";

        static double nextCheck;

        static AutoBuild()
        {
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 2.0;

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
                return;

            if (File.Exists(BuildRequest))
            {
                // Importar y compilar cualquier script cambiado en disco ANTES de
                // construir; si dispara una compilacion, reintentar en el siguiente
                // tick ya con los ensamblados frescos.
                AssetDatabase.Refresh();
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                File.Delete(BuildRequest);
                try
                {
                    SceneBuilder.BuildAll();
                    File.WriteAllText(ResultFile, "OK " + DateTime.Now.ToString("HH:mm:ss"));
                }
                catch (Exception e)
                {
                    File.WriteAllText(ResultFile, "FAIL " + e);
                }
                return;
            }

            if (File.Exists(PlayerRequest))
            {
                AssetDatabase.Refresh();
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                File.Delete(PlayerRequest);
                PlayerBuilder.BuildWindows();
                return;
            }

            if (File.Exists(VerifyRequest))
            {
                AssetDatabase.Refresh();
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                // VerifyCaptureRunner leera y borrara la senal al arrancar el Play.
                var scenePath = SceneBuilder2.ScenePathPublic;
                if (!File.Exists(scenePath))
                {
                    File.Delete(VerifyRequest);
                    File.WriteAllText(ResultFile, "FAIL escena no construida");
                    return;
                }
                EditorSceneManager.OpenScene(scenePath);
                EditorApplication.EnterPlaymode();
            }
        }
    }
}
