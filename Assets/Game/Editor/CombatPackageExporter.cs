using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TinyRpg.EditorTools
{
    /// Exporta el sistema de combate + estadisticas como .unitypackage para
    /// reutilizarlo en otro proyecto. La lista es cerrada a proposito: cada
    /// archivo fue verificado como parte de la clausura de dependencias (nada
    /// de aqui referencia GameManager, GameFlow, HUD, input ni mapas).
    public static class CombatPackageExporter
    {
        const string OutputFile = "Build/TinyRpgCombat.unitypackage";

        static readonly string[] Files =
        {
            // Guia de integracion
            "Assets/Game/CombatPackage/LEEME_SistemaCombate.md",

            // Estadisticas y movimiento
            "Assets/Game/Scripts/CharacterStats.cs",
            "Assets/Game/Scripts/CharacterAttributes.cs",
            "Assets/Game/Scripts/CharacterMotor.cs",

            // Combate
            "Assets/Game/Scripts/CharacterCombat.cs",
            "Assets/Game/Scripts/ArcherCombat.cs",
            "Assets/Game/Scripts/MageCombat.cs",
            "Assets/Game/Scripts/MonkCombat.cs",
            "Assets/Game/Scripts/ArrowProjectile.cs",
            "Assets/Game/Scripts/ArrowStrike.cs",
            "Assets/Game/Scripts/MagicCircleBlast.cs",
            "Assets/Game/Scripts/IceSpikeField.cs",

            // Efectos y visual
            "Assets/Game/Scripts/AttackVfx.cs",
            "Assets/Game/Scripts/VfxLibrary.cs",
            "Assets/Game/Scripts/FloatingText.cs",
            "Assets/Game/Scripts/SmoothCameraFollow.cs",
            "Assets/Game/Scripts/YSorter.cs",
            "Assets/Game/Scripts/UnitAnimator.cs",
            "Assets/Game/Scripts/WorldStatusBars.cs",

            // IA de duelo (6 cerebros escritos + neuronal)
            "Assets/Game/Scripts/DuelistAI.cs",
            "Assets/Game/Scripts/AI/NeuralNet.cs",
            "Assets/Game/Scripts/AI/TrainingMode.cs",
        };

        [MenuItem("TinyRpg/Exportar paquete de combate")]
        public static void Export()
        {
            // Fallar ANTES de exportar si la lista se desactualiza: un paquete
            // silenciosamente incompleto es peor que un error aqui.
            var missing = Files.Where(f => !File.Exists(f)).ToArray();
            if (missing.Length > 0)
                throw new FileNotFoundException(
                    "Faltan archivos del paquete: " + string.Join(", ", missing));

            Directory.CreateDirectory(Path.GetDirectoryName(OutputFile));
            AssetDatabase.ExportPackage(Files, OutputFile, ExportPackageOptions.Default);

            long size = new FileInfo(OutputFile).Length;
            Debug.Log($"[CombatPackage] Exportado {OutputFile} ({size / 1024} KB, {Files.Length} archivos)");
        }
    }
}
