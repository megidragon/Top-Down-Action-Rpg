using UnityEditor;
using UnityEngine;

namespace TinyRpg.EditorTools
{
    /// Validacion rapida del generador sin construir la escena completa.
    /// Pensada para cazar regresiones de conectividad en CI:
    ///   Unity -batchmode -quit -projectPath . -executeMethod TinyRpg.EditorTools.MapValidation.Run
    /// Sale con codigo 1 si alguna zona transitable queda aislada del spawn.
    public static class MapValidation
    {
        [MenuItem("TinyRpg/Validar conectividad del mapa")]
        public static void Run()
        {
            var map = MapGenerator.Generate();
            var spawn = map.FindWalkableNear(SceneBuilder.PlayerSpawnHint.x, SceneBuilder.PlayerSpawnHint.y);
            bool ok = MapGenerator.ValidateConnectivity(map, spawn);

            Debug.Log($"[MapValidation] Escaleras: tier1={map.stairTiles1.Count / 4}, tier2={map.stairTiles2.Count / 4}. " +
                (ok ? $"OK: toda la rejilla transitable es alcanzable desde {spawn}."
                    : "FALLO: hay zonas transitables aisladas (ver errores)."));

            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
