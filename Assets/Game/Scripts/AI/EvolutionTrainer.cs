using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TinyRpg.AI
{
    /// Bucle generacional de neuroevolucion. Mantiene una poblacion de redes,
    /// las evalua en duelos contra los cerebros escritos a mano (via
    /// ParallelTrainer) y cria la siguiente generacion con los mejores.
    ///
    /// La poblacion entrena UNA clase fija por tanda ('trainClass'): la red no
    /// recibe su propia clase como entrada, asi que pedirle una politica que
    /// sirva para las cinco a la vez solo emborronaria el aprendizaje.
    ///
    /// Los mejores se guardan en disco como JSON en cuanto baten el record, de
    /// modo que se puede parar el Play en cualquier momento sin perder nada, y
    /// reutilizarlos despues (siguiente entrenamiento, enemigos del juego...).
    public class EvolutionTrainer : MonoBehaviour
    {
        /// Entradas y salidas las fija DuelistAI; la oculta de 16 da holgura
        /// para mezclar combate y sensores de terreno (~490 pesos).
        public static readonly int[] Topology =
            { DuelistAI.ObservationCount, 16, DuelistAI.ActionCount };

        [Header("Poblacion")]
        public int populationSize = 40;    // = arenas: una generacion por tanda
        public int duelsPerIndividual = 3; // duelos por red y generacion
        public int trainClass = 0;         // 0 guerrero ... 4 mago

        [Header("Cria")]
        public int eliteCount = 6;         // pasan intactos
        public int freshCount = 4;         // aleatorios nuevos (diversidad)
        public float mutationRate = 0.12f;
        public float mutationStrength = 0.3f;

        [Header("Guardado")]
        public int milestoneEvery = 10;    // ademas del record, un hito cada N

        public static readonly string[] ClassNames =
            { "Guerrero", "Lancero", "Arquero", "Monje", "Mago" };

        // ---- Estado visible para la UI ----
        public int Generation { get; private set; }
        public float BestFitnessEver { get; private set; } = float.MinValue;
        public int BestGeneration { get; private set; }
        public float LastGenBest { get; private set; }
        public float LastGenAverage { get; private set; }
        public float LastWinRate { get; private set; }
        public float LastTimeoutRate { get; private set; }
        public string RivalPoolLabel { get; private set; } = "";
        public int SavedCount { get; private set; }
        public string LastSavedFile { get; private set; } = "";
        public bool Running { get; private set; }
        public float StartedRealtime { get; private set; }

        NeuralNet[] population;
        float[] fitness;
        ParallelTrainer trainer;
        System.Random rng;
        readonly List<ParallelTrainer.Result> results = new List<ParallelTrainer.Result>();

        /// Carpeta de cerebros guardados. En el editor va dentro de Assets para
        /// tenerlos versionados junto al proyecto; en un build, a datos de
        /// usuario (Assets no existe alli).
        public static string SaveDir
        {
            get
            {
#if UNITY_EDITOR
                return "Assets/Game/NeuralBrains";
#else
                return Path.Combine(Application.persistentDataPath, "NeuralBrains");
#endif
            }
        }

        /// Envoltorio de guardado: el JSON lleva su propio contexto (clase,
        /// generacion, aptitud) para que un archivo suelto se explique solo.
        [System.Serializable]
        public class SavedBrain
        {
            public string clase;
            public int classIndex;
            public int generation;
            public float fitness;
            public string fecha;
            public NeuralNet net;
        }

        public IEnumerator RunForever()
        {
            trainer = GetComponent<ParallelTrainer>();
            if (trainer == null) trainer = gameObject.AddComponent<ParallelTrainer>();
            trainer.arenaCount = populationSize;

            rng = new System.Random(System.Environment.TickCount);
            Running = true;
            StartedRealtime = Time.realtimeSinceStartup;

            InitPopulation();

            while (true)
                yield return RunGeneration();
        }

        void InitPopulation()
        {
            population = new NeuralNet[populationSize];
            fitness = new float[populationSize];
            for (int i = 0; i < populationSize; i++)
                population[i] = NeuralNet.CreateRandom(Topology, rng);

            // Continuar donde se quedo: si hay un campeon guardado de esta
            // clase y su arquitectura encaja, siembra parte de la poblacion.
            var seed = LoadBest(trainClass);
            if (seed != null && SameTopology(seed.layers, Topology))
            {
                population[0] = seed.Clone();
                int seeded = populationSize / 4;
                for (int i = 1; i <= seeded; i++)
                {
                    population[i] = seed.Clone();
                    population[i].Mutate(rng, mutationRate, mutationStrength);
                }
                Debug.Log("[Evolucion] Poblacion sembrada desde el campeon guardado.");
            }
        }

        int currentPhase = -1;

        IEnumerator RunGeneration()
        {
            Generation++;

            // El record solo compara dentro de la misma fase de rivales: la
            // aptitud contra "solo Rusher" esta inflada respecto a la aptitud
            // contra la bolsa completa, y un record de la era facil se volvia
            // imbatible aunque la poblacion siguiera mejorando (paso en la
            // primera tanda: 120 generaciones "sin record" tras la gen 20).
            int phase = PhaseFor(Generation);
            if (phase != currentPhase)
            {
                currentPhase = phase;
                BestFitnessEver = float.MinValue;
            }

            var pool = RivalPool(Generation);
            RivalPoolLabel = string.Join(", ", System.Array.ConvertAll(pool, b => b.ToString()));

            // Orden por rondas: la tanda j enfrenta a TODA la poblacion a la
            // vez (arena i = individuo i), que es lo legible desde fuera.
            var matchups = new List<ParallelTrainer.Matchup>(populationSize * duelsPerIndividual);
            for (int round = 0; round < duelsPerIndividual; round++)
                for (int i = 0; i < populationSize; i++)
                    matchups.Add(new ParallelTrainer.Matchup
                    {
                        net = population[i],
                        netClass = trainClass,
                        rivalBrain = pool[rng.Next(pool.Length)],
                        rivalClass = rng.Next(ClassNames.Length),
                    });

            yield return trainer.RunBatch(matchups, results);

            // ---- Aptitud ----
            System.Array.Clear(fitness, 0, fitness.Length);
            int wins = 0, timeouts = 0;
            for (int k = 0; k < results.Count; k++)
            {
                var r = results[k];
                int who = k % populationSize;
                float f = r.damageDealt - r.damageTaken * 0.5f;
                if (r.won)
                {
                    // Ganar rapido vale mas: premia rematar, no especular.
                    f += 120f + Mathf.Max(0f, trainer.duelTimeout - r.duration) * 4f;
                    wins++;
                }
                else if (r.lost) f -= 30f;
                else if (r.timedOut) { f -= 10f; timeouts++; }
                fitness[who] += f;
            }

            float best = float.MinValue, sum = 0f;
            int bestIdx = 0;
            for (int i = 0; i < populationSize; i++)
            {
                sum += fitness[i];
                if (fitness[i] > best) { best = fitness[i]; bestIdx = i; }
            }
            LastGenBest = best;
            LastGenAverage = sum / populationSize;
            LastWinRate = (float)wins / results.Count;
            LastTimeoutRate = (float)timeouts / results.Count;

            // ---- Guardado ----
            if (best > BestFitnessEver)
            {
                BestFitnessEver = best;
                BestGeneration = Generation;
                Save(population[bestIdx], best, $"record_gen{Generation:D3}");
                Save(population[bestIdx], best, "mejor"); // el campeon vigente
            }
            if (Generation % milestoneEvery == 0)
                Save(population[bestIdx], best, $"hito_gen{Generation:D3}");

            Reproduce();
        }

        /// Fase del plan de estudios (cambia cuando cambia la bolsa de rivales).
        static int PhaseFor(int gen) => gen < 6 ? 0 : gen < 14 ? 1 : gen < 24 ? 2 : 3;

        /// Plan de estudios: primero el rival mas simple, y se van sumando los
        /// demas conforme la poblacion madura. Contra Counter desde el minuto
        /// uno, una red aleatoria solo aprenderia a no acercarse jamas.
        static CombatBrain[] RivalPool(int gen)
        {
            if (gen < 6) return new[] { CombatBrain.Rusher };
            if (gen < 14) return new[] { CombatBrain.Rusher, CombatBrain.Ambusher, CombatBrain.Spacer };
            if (gen < 24) return new[] { CombatBrain.Rusher, CombatBrain.Ambusher,
                CombatBrain.Spacer, CombatBrain.Feinter, CombatBrain.Flanker };
            return new[] { CombatBrain.Rusher, CombatBrain.Ambusher, CombatBrain.Spacer,
                CombatBrain.Feinter, CombatBrain.Flanker, CombatBrain.Counter };
        }

        void Reproduce()
        {
            // Indices ordenados por aptitud, de mejor a peor.
            var order = new int[populationSize];
            for (int i = 0; i < populationSize; i++) order[i] = i;
            System.Array.Sort(order, (a, b) => fitness[b].CompareTo(fitness[a]));

            var next = new NeuralNet[populationSize];
            int n = 0;

            for (int i = 0; i < eliteCount && n < populationSize; i++)
                next[n++] = population[order[i]].Clone();

            for (int i = 0; i < freshCount && n < populationSize; i++)
                next[n++] = NeuralNet.CreateRandom(Topology, rng);

            while (n < populationSize)
            {
                var a = population[Tournament(order)];
                var b = population[Tournament(order)];
                var child = NeuralNet.Crossover(a, b, rng);
                child.Mutate(rng, mutationRate, mutationStrength);
                next[n++] = child;
            }
            population = next;
        }

        /// Torneo de 3: gana el mejor clasificado de tres al azar. Presiona
        /// hacia arriba sin que el campeon monopolice la descendencia.
        int Tournament(int[] order)
        {
            int best = populationSize - 1;
            for (int i = 0; i < 3; i++)
            {
                int rank = rng.Next(populationSize);
                if (rank < best) best = rank;
            }
            return order[best];
        }

        // ----------------------------------------------------------------
        //  Persistencia
        // ----------------------------------------------------------------

        void Save(NeuralNet net, float fit, string name)
        {
            try
            {
                Directory.CreateDirectory(SaveDir);
                var wrapped = new SavedBrain
                {
                    clase = ClassNames[trainClass],
                    classIndex = trainClass,
                    generation = Generation,
                    fitness = fit,
                    fecha = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    net = net,
                };
                string file = Path.Combine(SaveDir, $"{ClassNames[trainClass].ToLower()}_{name}.json");
                File.WriteAllText(file, JsonUtility.ToJson(wrapped, true));
                SavedCount++;
                LastSavedFile = Path.GetFileName(file);

#if UNITY_EDITOR
                // El campeon vigente se publica ademas en Resources: es la
                // copia que un build del juego lleva dentro y la que carga
                // NeuralBrainLibrary cuando no hay carpeta de entrenamiento.
                if (name == "mejor")
                {
                    const string resDir = "Assets/Game/Resources/NeuralBrains";
                    Directory.CreateDirectory(resDir);
                    File.Copy(file, Path.Combine(resDir, Path.GetFileName(file)), true);
                }
#endif
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Evolucion] No se pudo guardar: " + e.Message);
            }
        }

        /// Campeon guardado de una clase, o null si no hay (o no se puede leer).
        public static NeuralNet LoadBest(int classIndex)
        {
            try
            {
                string file = Path.Combine(SaveDir, $"{ClassNames[classIndex].ToLower()}_mejor.json");
                if (!File.Exists(file)) return null;
                var wrapped = JsonUtility.FromJson<SavedBrain>(File.ReadAllText(file));
                var net = wrapped?.net;
                return net != null && net.layers != null && net.weights != null
                    && net.weights.Length == NeuralNet.WeightCountFor(net.layers) ? net : null;
            }
            catch { return null; }
        }

        static bool SameTopology(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
