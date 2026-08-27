using System.IO;
using UnityEngine;

namespace TinyRpg.AI
{
    /// Acceso runtime a los campeones entrenados, con cache. Busca primero en
    /// la carpeta de entrenamiento (editor / datos de usuario) y despues en
    /// Resources, que es lo que viaja dentro de un build.
    ///
    /// Solo devuelve una red si su arquitectura encaja con las entradas y
    /// salidas ACTUALES de DuelistAI: un campeon entrenado con otra version de
    /// la observacion no debe enchufarse jamas (jugaria con sentidos cambiados
    /// de sitio).
    public static class NeuralBrainLibrary
    {
        static readonly NeuralNet[] cache = new NeuralNet[EvolutionTrainer.ClassNames.Length];
        static readonly bool[] resolved = new bool[EvolutionTrainer.ClassNames.Length];

        /// Campeon vigente de la clase, o null si no hay ninguno valido.
        public static NeuralNet Champion(int classIndex)
        {
            if (classIndex < 0 || classIndex >= cache.Length) return null;
            if (resolved[classIndex]) return cache[classIndex];
            resolved[classIndex] = true;

            string name = EvolutionTrainer.ClassNames[classIndex].ToLower() + "_mejor";

            // 1) Archivo suelto: donde guarda el entrenamiento. Permite
            //    actualizar el cerebro sin reconstruir nada.
            NeuralNet net = null;
            try
            {
                string file = Path.Combine(EvolutionTrainer.SaveDir, name + ".json");
                if (File.Exists(file)) net = Parse(File.ReadAllText(file));
            }
            catch { /* sin acceso a disco: probamos Resources */ }

            // 2) Resources: la copia que viaja dentro del build.
            if (net == null)
            {
                var asset = Resources.Load<TextAsset>("NeuralBrains/" + name);
                if (asset != null) net = Parse(asset.text);
            }

            cache[classIndex] = net;
            return net;
        }

        /// Acepta tanto el envoltorio SavedBrain como una red a pelo.
        static NeuralNet Parse(string json)
        {
            NeuralNet net = null;
            try
            {
                var wrapped = JsonUtility.FromJson<EvolutionTrainer.SavedBrain>(json);
                net = wrapped?.net;
                if (net == null || net.layers == null || net.layers.Length == 0)
                    net = NeuralNet.FromJson(json);
            }
            catch { return null; }

            if (net == null || net.layers == null || net.weights == null) return null;
            if (net.weights.Length != NeuralNet.WeightCountFor(net.layers)) return null;
            if (net.InputCount != DuelistAI.ObservationCount
                || net.OutputCount != DuelistAI.ActionCount) return null;
            return net;
        }
    }
}
