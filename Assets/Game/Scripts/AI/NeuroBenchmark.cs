using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TinyRpg.AI
{
    /// Medidor de rendimiento del entrenamiento. Antes de escribir el bucle
    /// evolutivo hay que saber cuantos duelos por segundo da la maquina: de esa
    /// cifra depende que una generacion tarde minutos u horas.
    ///
    /// Compara la misma carga con arenas en paralelo y sin ellas, para ver
    /// cuanto aporta realmente el lote.
    public class NeuroBenchmark : MonoBehaviour
    {
        // Capa oculta de 16: con 23 entradas, 12 se quedaba corta para mezclar
        // combate y terreno. Aun asi son ~490 pesos, nada para evaluar.
        public static readonly int[] Topology = { DuelistAI.ObservationCount, 16, DuelistAI.ActionCount };

        public bool Running { get; private set; }

        readonly List<string> log = new List<string>();

        public void StartBenchmark(int duels = 120)
        {
            if (Running) return;
            StartCoroutine(RunBenchmark(duels));
        }

        IEnumerator RunBenchmark(int duels)
        {
            Running = true;
            log.Clear();

            var rng = new System.Random(20260827);
            var probe = NeuralNet.CreateRandom(Topology, rng);
            log.Add("BANCO DE PRUEBAS DE ENTRENAMIENTO");
            log.Add($"Red: {probe.Describe()}   |   duelos por prueba: {duels}");
            log.Add("");

            var trainer = GetComponent<ParallelTrainer>();
            if (trainer == null) trainer = gameObject.AddComponent<ParallelTrainer>();

            // Encuadre sobre TODAS las arenas con graficos, no solo la primera:
            // estan separadas 40 unidades, asi que enfocando una sola el resto
            // cae fuera de plano y parece que no hay paralelismo ninguno.
            int shown = Mathf.Max(1, trainer.visibleArenas);
            Vector2 first = ParallelTrainer.ArenaCenter(0);
            Vector2 last = ParallelTrainer.ArenaCenter(shown - 1);
            Vector2 mid = (first + last) * 0.5f;

            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.7778f;
            float spanX = Mathf.Abs(last.x - first.x) + ParallelTrainer.ArenaSpacing;
            float spanY = Mathf.Abs(last.y - first.y) + ParallelTrainer.ArenaSpacing;
            // El tamano ortografico es media ALTURA: por eso el ancho se divide
            // ademas por la relacion de aspecto.
            float size = Mathf.Max(spanY, spanX / Mathf.Max(0.1f, aspect)) * 0.55f;
            LabArena.Instance?.BeginSpectator(mid, Mathf.Max(10f, size));

            var results = new List<ParallelTrainer.Result>();

            // (arenas, escala de tiempo, cuantos duelos medir).
            //
            // El tamano de muestra NO es igual para todas: una arena a 1x tarda
            // lo mismo por duelo que el juego real, asi que medirla con 120
            // duelos son 20 minutos mirando un solo combate. Con 10 basta para
            // fijar la linea base, y el ritmo se compara en duelos/segundo.
            //
            // Y van de mas paralelo a menos, para que lo que se ve nada mas
            // empezar sea la rejilla llena y no el caso degenerado.
            var configs = new[]
            {
                (40, 8f, duels),
                (64, 10f, duels),
                (16, 8f, Mathf.Max(16, duels / 3)),
                (1, 8f, 10),
                (1, 1f, 10),
            };

            foreach (var (arenas, scale, batchSize) in configs)
            {
                trainer.arenaCount = arenas;
                trainer.timeScale = scale;
                trainer.duelTimeout = 12f;

                var matchups = BuildMatchups(batchSize, rng);
                yield return trainer.RunBatch(matchups, results);

                float secs = Mathf.Max(0.001f, trainer.LastBatchSeconds);
                float perSecond = batchSize / secs;

                // Cuantos acabaron sin muertos al agotar el tiempo. Es la
                // medida de si el lote da senal util o solo empates.
                int stalled = 0;
                foreach (var r in results) if (r.timedOut) stalled++;

                log.Add($"  {arenas,3} arenas x {scale,4:F0}x  ->  {batchSize,4} duelos en {secs,7:F1}s"
                      + $"   ({perSecond,6:F1} duelos/s)"
                      + $"   1000 duelos en {1000f / perSecond / 60f,5:F1} min"
                      + $"   | por tiempo: {stalled * 100f / Mathf.Max(1, results.Count),4:F0}%");

                yield return new WaitForSecondsRealtime(0.3f);
            }

            log.Add("");
            // Cuanto costaria una evolucion realista con la mejor cifra.
            log.Add("Con 50 individuos x 4 duelos = 200 duelos por generacion.");

            LabArena.Instance?.EndSpectator();
            WriteLog();
            Running = false;
        }

        /// Emparejamientos de prueba: redes aleatorias contra los cerebros
        /// escritos a mano, repartiendo clases.
        static List<ParallelTrainer.Matchup> BuildMatchups(int count, System.Random rng)
        {
            var brains = (CombatBrain[])System.Enum.GetValues(typeof(CombatBrain));
            var list = new List<ParallelTrainer.Matchup>(count);
            for (int i = 0; i < count; i++)
            {
                CombatBrain rival;
                do { rival = brains[rng.Next(brains.Length)]; }
                while (rival == CombatBrain.Neural); // el rival es siempre escrito a mano

                list.Add(new ParallelTrainer.Matchup
                {
                    net = NeuralNet.CreateRandom(Topology, rng),
                    netClass = rng.Next(5),
                    rivalBrain = rival,
                    rivalClass = rng.Next(5),
                });
            }
            return list;
        }

        void WriteLog()
        {
            string text = string.Join("\n", log);
            Debug.Log("[Neuro]\n" + text);
#if UNITY_EDITOR
            try { File.WriteAllText("Library/tinyrpg_neuro.txt", text); }
            catch (System.Exception e) { Debug.LogWarning("[Neuro] " + e.Message); }
#endif
        }
    }
}
