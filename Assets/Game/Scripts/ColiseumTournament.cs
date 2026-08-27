using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TinyRpg
{
    /// Banco de pruebas del coliseo: encadena duelos de guerreros con distintos
    /// cerebros de combate y apunta los resultados.
    ///  - 6 combates espejo (cada cerebro contra si mismo) para ver el ritmo
    ///    natural de cada estilo.
    ///  - 3 combates cruzados que enfrentan filosofias opuestas.
    /// Ambos duelistas se igualan (150 de vida, estadisticas 5/5/5) para que la
    /// unica variable sea la IA.
    public class ColiseumTournament : MonoBehaviour
    {
        public float fightTimeout = 30f;    // segundos de juego por combate
        public float speedMultiplier = 1f;  // 1 = mirar en directo; >1 para medir rapido
        /// Repeticiones por emparejamiento. Con 1 los resultados son anecdotas:
        /// hay azar en parries, pausas y orbitas, y un mismo duelo se invierte
        /// de una ejecucion a otra. Con 5+ ya se ve la tendencia real.
        public int repetitions = 1;

        public bool Running { get; private set; }

        readonly List<string> log = new List<string>();
        GameObject blue, red;

        static readonly (CombatBrain a, CombatBrain b)[] CrossMatches =
        {
            (CombatBrain.Rusher, CombatBrain.Spacer),     // agresion vs distancia
            (CombatBrain.Counter, CombatBrain.Feinter),   // parry vs engano
            (CombatBrain.Ambusher, CombatBrain.Flanker),  // rafaga vs angulo muerto
        };

        public void StartTournament()
        {
            if (Running) return;
            StartCoroutine(RunAll());
        }

        /// Liga completa: las 30 combinaciones (5 clases x 6 cerebros) todas
        /// contra todas, para medir que clase y que algoritmo rinden mejor.
        public void StartLeague()
        {
            if (Running) return;
            StartCoroutine(RunLeague());
        }

        static readonly string[] ClassNames = { "Guerrero", "Lancero", "Arquero", "Monje", "Mago" };

        IEnumerator RunLeague()
        {
            Running = true;
            log.Clear();

            var brains = (CombatBrain[])System.Enum.GetValues(typeof(CombatBrain));
            var combos = new List<(int cls, CombatBrain brain)>();
            for (int c = 0; c < 5; c++)
                foreach (var b in brains)
                    combos.Add((c, b));

            int n = combos.Count;
            var wins = new int[n];
            var losses = new int[n];
            var draws = new int[n];

            float previousScale = Time.timeScale;
            Time.timeScale = Mathf.Max(0.1f, speedMultiplier);

            Vector2 arenaCenter = LabArena.Instance != null
                ? LabArena.Instance.Center : new Vector2(17f, 13f);
            LabArena.Instance?.BeginSpectator(arenaCenter, 11f);

            int totalFights = n * (n - 1) / 2;
            int fought = 0;

            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    // Alternar bandos para que la posicion inicial no pese.
                    bool iBlue = ((i + j) % 2) == 0;
                    var blueCombo = iBlue ? combos[i] : combos[j];
                    var redCombo = iBlue ? combos[j] : combos[i];

                    fought++;
                    yield return Fight(fought, totalFights, blueCombo.brain, redCombo.brain,
                        0, blueCombo.cls, redCombo.cls);

                    if (lastWinner == 0) { draws[i]++; draws[j]++; }
                    else
                    {
                        bool blueWon = lastWinner == 1;
                        bool iWon = iBlue ? blueWon : !blueWon;
                        if (iWon) { wins[i]++; losses[j]++; }
                        else { wins[j]++; losses[i]++; }
                    }
                }

            Time.timeScale = previousScale;
            LabArena.Instance?.EndSpectator();
            Cleanup();

            // ---- Clasificacion ----
            log.Add($"LIGA DEL COLISEO - {n} combinaciones (5 clases x 6 algoritmos)");
            log.Add($"Todas contra todas: {totalFights} duelos, limite {fightTimeout:F0}s cada uno");
            log.Add("");

            var order = new List<int>();
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((x, y) =>
            {
                int cmp = wins[y].CompareTo(wins[x]);
                return cmp != 0 ? cmp : draws[y].CompareTo(draws[x]);
            });

            log.Add("--- CLASIFICACION (por victorias sobre 29 duelos) ---");
            for (int r = 0; r < order.Count; r++)
            {
                int i = order[r];
                log.Add($"{r + 1,2}. {ClassNames[combos[i].cls],-9} {combos[i].brain,-9}"
                      + $"  {wins[i],2}V {losses[i],2}D {draws[i],2}T");
            }

            log.Add("");
            log.Add("--- POR CLASE (victorias sumadas de sus 6 algoritmos) ---");
            for (int c = 0; c < 5; c++)
            {
                int w = 0, l = 0, d = 0;
                for (int i = 0; i < n; i++)
                    if (combos[i].cls == c) { w += wins[i]; l += losses[i]; d += draws[i]; }
                log.Add($"    {ClassNames[c],-9} {w,3}V {l,3}D {d,3}T");
            }

            log.Add("");
            log.Add("--- POR ALGORITMO (victorias sumadas de sus 5 clases) ---");
            foreach (var b in brains)
            {
                int w = 0, l = 0, d = 0;
                for (int i = 0; i < n; i++)
                    if (combos[i].brain == b) { w += wins[i]; l += losses[i]; d += draws[i]; }
                log.Add($"    {b,-9} {w,3}V {l,3}D {d,3}T");
            }

            WriteLog();
            Running = false;

            var lab2 = LabArena.Instance;
            if (lab2 != null && lab2.titleText != null) lab2.titleText.text = "LIGA TERMINADA";
        }

        IEnumerator RunAll()
        {
            Running = true;
            log.Clear();
            log.Add("TORNEO DEL COLISEO - guerrero vs guerrero, 150 HP, stats 5/5/5");
            log.Add("");

            float previousScale = Time.timeScale;
            Time.timeScale = Mathf.Max(0.1f, speedMultiplier);

            // Espectador: el jugador se aparta y la camara queda libre.
            Vector2 arenaCenter = LabArena.Instance != null
                ? LabArena.Instance.Center : new Vector2(17f, 13f);
            LabArena.Instance?.BeginSpectator(arenaCenter, 10f);

            var brains = (CombatBrain[])Enum.GetValues(typeof(CombatBrain));
            int index = 0;
            int total = brains.Length + CrossMatches.Length;

            log.Add($"Repeticiones por emparejamiento: {repetitions}");
            log.Add("");
            log.Add("--- ESPEJO (mismo cerebro en ambos lados) ---");
            foreach (var brain in brains)
            {
                index++;
                yield return Series(index, total, brain, brain);
            }

            log.Add("");
            log.Add("--- CRUZADOS ---");
            foreach (var (a, b) in CrossMatches)
            {
                index++;
                yield return Series(index, total, a, b);
            }

            Time.timeScale = previousScale;
            LabArena.Instance?.EndSpectator();
            Cleanup();
            WriteLog();
            Running = false;

            var lab = LabArena.Instance;
            if (lab != null && lab.titleText != null)
                lab.titleText.text = "TORNEO TERMINADO";
        }

        /// Una serie de repeticiones del mismo emparejamiento, con el marcador
        /// agregado. Los bandos se INTERCAMBIAN en las repeticiones impares
        /// para que una ventaja de posicion inicial no falsee el resultado.
        IEnumerator Series(int index, int total, CombatBrain a, CombatBrain b)
        {
            int winsA = 0, winsB = 0, draws = 0;
            float totalTime = 0f, hpA = 0f, hpB = 0f;

            for (int rep = 0; rep < Mathf.Max(1, repetitions); rep++)
            {
                bool swap = rep % 2 == 1;
                var blueBrain = swap ? b : a;
                var redBrain = swap ? a : b;

                yield return Fight(index, total, blueBrain, redBrain, rep);

                totalTime += lastDuration;
                // Reasignar el resultado al cerebro, no al color.
                float aHp = swap ? lastRedHp : lastBlueHp;
                float bHp = swap ? lastBlueHp : lastRedHp;
                hpA += aHp;
                hpB += bHp;

                if (lastWinner == 0) draws++;
                else
                {
                    bool blueWon = lastWinner == 1;
                    bool aWon = swap ? !blueWon : blueWon;
                    if (aWon) winsA++; else winsB++;
                }
            }

            int reps = Mathf.Max(1, repetitions);
            string headline = a == b
                ? $"{index}. ESPEJO {a}"
                : $"{index}. {a} vs {b}";
            log.Add(headline);
            log.Add($"    {a} {winsA} - {winsB} {b}   (tablas {draws})"
                  + $"  |  duracion media {totalTime / reps:F1}s"
                  + $"  |  vida final media {hpA / reps:F0} vs {hpB / reps:F0}");
        }

        // Resultado del ultimo combate (lo consume Series).
        int lastWinner;      // 0 tablas/doble KO, 1 gana azul, 2 gana rojo
        float lastDuration, lastBlueHp, lastRedHp;

        IEnumerator Fight(int index, int total, CombatBrain blueBrain, CombatBrain redBrain,
            int rep = 0, int blueClass = 0, int redClass = 0)
        {
            Cleanup();

            var lab = LabArena.Instance;
            Vector2 center = lab != null ? lab.Center : new Vector2(17f, 13f);
            if (lab != null && lab.titleText != null)
                lab.titleText.text = repetitions > 1
                    ? $"{index}/{total}  {blueBrain} vs {redBrain}  ({rep + 1}/{repetitions})"
                    : $"{index}/{total}  {ClassNames[blueClass]} {blueBrain}"
                      + $" vs {ClassNames[redClass]} {redBrain}";

            blue = SpawnDuelist(true, blueBrain, center + new Vector2(-5f, 0f), blueClass);
            red = SpawnDuelist(false, redBrain, center + new Vector2(5f, 0f), redClass);
            if (blue == null || red == null)
            {
                log.Add($"{index}. {blueBrain} vs {redBrain}: FALLO al crear duelistas");
                lastWinner = 0; lastDuration = 0f; lastBlueHp = lastRedHp = 0f;
                yield break;
            }

            blue.GetComponent<DuelistAI>().SetFoe(red.transform);
            red.GetComponent<DuelistAI>().SetFoe(blue.transform);

            var blueStats = blue.GetComponent<CharacterStats>();
            var redStats = red.GetComponent<CharacterStats>();

            float t = 0f;
            while (t < fightTimeout && !blueStats.IsDead && !redStats.IsDead)
            {
                t += Time.deltaTime;
                yield return null;
            }

            lastDuration = t;
            lastBlueHp = Mathf.Max(0f, blueStats.Health);
            lastRedHp = Mathf.Max(0f, redStats.Health);
            if (blueStats.IsDead && redStats.IsDead) lastWinner = 0;
            else if (redStats.IsDead) lastWinner = 1;
            else if (blueStats.IsDead) lastWinner = 2;
            else lastWinner = 0;

            if (repetitions <= 1)
            {
                string outcome = lastWinner == 1 ? $"gana AZUL ({blueBrain})"
                               : lastWinner == 2 ? $"gana ROJO ({redBrain})"
                               : (blueStats.IsDead && redStats.IsDead) ? "doble KO"
                               : "tablas (tiempo agotado)";
                log.Add($"{index}. {blueBrain} (azul) vs {redBrain} (rojo)");
                log.Add($"    {outcome}  |  {t:F1}s  |  "
                      + $"vida azul {lastBlueHp:F0}/150, vida roja {lastRedHp:F0}/150");
            }

            blue.GetComponent<UnitAnimator>()?.SetDeadVisual();
            red.GetComponent<UnitAnimator>()?.SetDeadVisual();
            yield return new WaitForSeconds(0.8f);
        }

        /// Vida de referencia de cada clase (la del prefab de JUGADOR). Los
        /// prefabs enemigos traen menos vida, asi que se normaliza para que la
        /// clase valga lo mismo en los dos bandos.
        static readonly float[] ClassHealth = { 150f, 112f, 75f, 125f, 90f };

        GameObject SpawnDuelist(bool blueSide, CombatBrain brain, Vector2 pos,
            int classIndex = 0)
        {
            GameObject prefab = null;
            if (blueSide)
            {
                var s = ClassSelectScreen.Instance;
                if (s != null)
                    prefab = classIndex == 4 ? s.magePrefab
                           : classIndex == 3 ? s.monkPrefab
                           : classIndex == 2 ? s.archerPrefab
                           : classIndex == 1 ? s.lancerPrefab : s.warriorPrefab;
            }
            else
            {
                var lib = MapLibrary.Instance;
                prefab = lib != null && lib.enemyPrefabs != null
                    && classIndex < lib.enemyPrefabs.Length
                    ? lib.enemyPrefabs[classIndex] : null;
            }
            if (prefab == null) return null;

            // El prefab azul trae PlayerController: no debe registrarse como
            // jugador ni leer el teclado.
            PlayerController.SpawningAlly = true;
            var go = Instantiate(prefab, pos, Quaternion.identity);
            PlayerController.SpawningAlly = false;
            go.name = (blueSide ? "Duelist_Blue_" : "Duelist_Red_") + brain;

            var pc = go.GetComponent<PlayerController>();
            if (pc != null) Destroy(pc);
            var enemyAi = go.GetComponent<EnemyAI>();
            if (enemyAi != null) Destroy(enemyAi);

            var combat = go.GetComponent<CharacterCombat>();
            if (combat != null) combat.isPlayer = false; // sin temblor de camara

            var stats = go.GetComponent<CharacterStats>();
            stats.team = blueSide ? 0 : 1;
            // Normalizar a la vida de la clase (el prefab enemigo trae menos).
            stats.maxHealth = ClassHealth[Mathf.Clamp(classIndex, 0, ClassHealth.Length - 1)];
            stats.Heal(999f);

            var attrs = go.GetComponent<CharacterAttributes>();
            if (attrs == null) attrs = go.AddComponent<CharacterAttributes>();
            attrs.strength = attrs.defense = attrs.speed = 5;
            go.GetComponent<CharacterMotor>()?.RefreshAttributesCache();

            var ai = go.AddComponent<DuelistAI>();
            ai.brain = brain;
            return go;
        }

        void Cleanup()
        {
            if (blue != null) Destroy(blue);
            if (red != null) Destroy(red);
            blue = red = null;
        }

        void WriteLog()
        {
            string text = string.Join("\n", log);
            Debug.Log("[Coliseo]\n" + text);
#if UNITY_EDITOR
            try { File.WriteAllText("Library/tinyrpg_duels.txt", text); }
            catch (Exception e) { Debug.LogWarning("[Coliseo] no se pudo escribir: " + e.Message); }
#endif
        }
    }
}
